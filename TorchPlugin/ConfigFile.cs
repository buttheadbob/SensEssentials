using System;
using System.IO;
using System.Threading;
using System.Xml.Serialization;

namespace SensEssentials.TorchHost;

/// <summary>
/// Loads the config and saves it a second after each change. This replaces Torch's Persistent, whose save timer has no
/// error handling, so a failed save takes the server down, and which writes straight over the only copy of the file.
/// </summary>
internal sealed class ConfigFile : IDisposable
{
    private static readonly XmlSerializer Serializer = new(typeof(EssentialsConfig));

    private readonly string _path;
    private readonly object _sync = new();
    private readonly Timer _saveTimer;

    public EssentialsConfig Data { get; }

    public ConfigFile(string path)
    {
        _path = path;
        Data = Load(path);
        _saveTimer = new Timer(_ => SaveQuietly());
        Data.PropertyChanged += (_, _) => _saveTimer.Change(1000, Timeout.Infinite);

        if (!File.Exists(path))
            SaveQuietly();
    }

    private static EssentialsConfig Load(string path)
    {
        if (!File.Exists(path))
            return new EssentialsConfig();

        try
        {
            using var stream = File.OpenRead(path);
            return (EssentialsConfig)Serializer.Deserialize(stream);
        }
        catch (Exception e)
        {
            EssentialsCore.KeepBadConfig(path, e);
            return new EssentialsConfig();
        }
    }

    public void Save()
    {
        var temp = _path + ".tmp";
        lock (_sync)
        {
            using (var stream = File.Create(temp))
                Serializer.Serialize(stream, Data);

            // Replace swaps the files in one step, so a crash never leaves a half written config.
            if (File.Exists(_path))
                File.Replace(temp, _path, null);
            else
                File.Move(temp, _path);
        }
    }

    private void SaveQuietly()
    {
        try
        {
            Save();
        }
        catch (Exception e)
        {
            Log.Error(e, $"Could not save {_path}");
        }
    }

    public void Dispose()
    {
        _saveTimer.Dispose();
        SaveQuietly();
    }
}
