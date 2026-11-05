using System.Text.Json;
using SensEssentials.Quasar.Model;

namespace SensEssentials.Quasar.Services;

/// <summary>A named Sens Essentials config that servers can follow.</summary>
public sealed class SharedConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Goes up on every save. Servers with an older revision are sent the config again.</summary>
    public long Revision { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }
    public SharedConfigValues Values { get; set; } = new();
}

/// <summary>Which shared config a server follows. Servers without an assignment are configured on their own.</summary>
public sealed class ServerAssignment
{
    public string ConfigId { get; set; } = "";

    /// <summary>Last known name, shown while the server is offline.</summary>
    public string DisplayName { get; set; } = "";
}

/// <summary>
/// Shared configs and server assignments, kept in {Quasar install}/SensEssentials/shared-configs.json.
/// Everything handed out is a copy; changes go through the methods here.
/// </summary>
public sealed class SharedConfigStore
{
    private sealed class StoreFile
    {
        public List<SharedConfig> Configs { get; set; } = [];
        public Dictionary<string, ServerAssignment> Servers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions FileJson = new(SharedConfigValues.JsonOptions) { WriteIndented = true };

    private readonly string _path;
    private readonly Lock _sync = new();
    private StoreFile _file;

    public event Action? Changed;

    public SharedConfigStore(string directory)
    {
        _path = Path.Combine(directory, "shared-configs.json");
        try
        {
            _file = Load(_path);
        }
        catch (Exception e)
        {
            // Never stop Quasar from starting. Saving and syncing stay off: an empty store would release every server
            // and the next save would overwrite the file that could still be repaired.
            _file = new StoreFile();
            LoadError = $"{_path} could not be read, so shared configs are paused: {e.Message} Fix or remove the file and restart Quasar.";
        }
    }

    /// <summary>Why the store file could not be read. While set, nothing is saved and servers are not synced.</summary>
    public string? LoadError { get; }

    public IReadOnlyList<SharedConfig> GetConfigs()
    {
        lock (_sync)
            return _file.Configs.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).Select(Copy).ToList();
    }

    public SharedConfig? GetConfig(string id)
    {
        lock (_sync)
        {
            var config = _file.Configs.FirstOrDefault(c => c.Id == id);
            return config == null ? null : Copy(config);
        }
    }

    public IReadOnlyDictionary<string, ServerAssignment> GetAssignments()
    {
        lock (_sync)
            return _file.Servers.ToDictionary(p => p.Key, p => new ServerAssignment { ConfigId = p.Value.ConfigId, DisplayName = p.Value.DisplayName },
                StringComparer.OrdinalIgnoreCase);
    }

    public SharedConfig Create(string name, SharedConfigValues? values = null)
    {
        var config = new SharedConfig
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            Revision = 1,
            UpdatedUtc = DateTimeOffset.UtcNow,
            Values = values?.Clone() ?? new SharedConfigValues()
        };
        Update(file => file.Configs.Add(config));
        return Copy(config);
    }

    /// <summary>
    /// Saves new values and raises the revision, so every server following it gets them. Refused when the config is no
    /// longer at the revision the values were edited from, so two admins cannot overwrite each other unseen.
    /// </summary>
    public SharedConfig Save(string id, SharedConfigValues values, long expectedRevision)
    {
        SharedConfig? saved = null;
        Update(file =>
        {
            var config = file.Configs.FirstOrDefault(c => c.Id == id) ?? throw new InvalidOperationException("The shared config no longer exists.");
            if (config.Revision != expectedRevision)
                throw new InvalidOperationException("Someone else saved this config first. Check it, then save again.");
            config.Values = values.Clone();
            config.Revision++;
            config.UpdatedUtc = DateTimeOffset.UtcNow;
            saved = Copy(config);
        });
        return saved!;
    }

    /// <summary>The name is shown on the servers too, so renaming sends the config again.</summary>
    public void Rename(string id, string name)
    {
        Update(file =>
        {
            var config = file.Configs.FirstOrDefault(c => c.Id == id) ?? throw new InvalidOperationException("The shared config no longer exists.");
            config.Name = name.Trim();
            config.Revision++;
            config.UpdatedUtc = DateTimeOffset.UtcNow;
        });
    }

    /// <summary>Deletes the config. Its servers go back to being configured on their own and keep the settings.</summary>
    public void Delete(string id)
    {
        Update(file =>
        {
            file.Configs.RemoveAll(c => c.Id == id);
            foreach (var server in file.Servers.Where(s => s.Value.ConfigId == id).Select(s => s.Key).ToList())
                file.Servers.Remove(server);
        });
    }

    /// <summary>Makes a server follow a config, or stop following one with a null id.</summary>
    public void Assign(string serverId, string displayName, string? configId)
    {
        Update(file =>
        {
            if (string.IsNullOrEmpty(configId))
                file.Servers.Remove(serverId);
            else if (file.Configs.Any(c => c.Id == configId))
                file.Servers[serverId] = new ServerAssignment { ConfigId = configId, DisplayName = displayName };
            else
                throw new InvalidOperationException("The shared config no longer exists.");
        });
    }

    /// <summary>Keeps the offline name of an assigned server current.</summary>
    public void RememberName(string serverId, string displayName)
    {
        if (LoadError != null)
            return;
        lock (_sync)
        {
            if (!_file.Servers.TryGetValue(serverId, out var assignment) || assignment.DisplayName == displayName)
                return;
        }
        Update(file =>
        {
            if (file.Servers.TryGetValue(serverId, out var assignment))
                assignment.DisplayName = displayName;
        });
    }

    private void Update(Action<StoreFile> change)
    {
        if (LoadError != null)
            throw new InvalidOperationException(LoadError);
        lock (_sync)
        {
            // Work on a copy so a failed write leaves memory and disk the same.
            var file = JsonSerializer.Deserialize<StoreFile>(JsonSerializer.Serialize(_file, FileJson), FileJson)!;
            file.Servers = new Dictionary<string, ServerAssignment>(file.Servers, StringComparer.OrdinalIgnoreCase);
            change(file);
            Write(_path, file);
            _file = file;
        }
        Changed?.Invoke();
    }

    private static StoreFile Load(string path)
    {
        if (!File.Exists(path))
            return new StoreFile();
        var file = JsonSerializer.Deserialize<StoreFile>(File.ReadAllText(path), FileJson) ?? new StoreFile();
        file.Configs ??= [];
        file.Servers = (file.Servers ?? []).Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var config in file.Configs)
            config.Values = SharedConfigValues.FromJson((config.Values ?? new SharedConfigValues()).ToJson());
        return file;
    }

    private static void Write(string path, StoreFile file)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, file, FileJson);
            stream.Flush(flushToDisk: true);
        }

        // The file holds Discord webhook URLs, so only Quasar's own user may read it, as with Quasar's own configs.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(temp, path, overwrite: true);
    }

    private static SharedConfig Copy(SharedConfig config) => new()
    {
        Id = config.Id,
        Name = config.Name,
        Revision = config.Revision,
        UpdatedUtc = config.UpdatedUtc,
        Values = config.Values.Clone()
    };
}
