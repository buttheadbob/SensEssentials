using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quasar.Plugin.Abstractions.Companion;
using SensEssentials.Quasar.Model;
using SensEssentials.QuasarContracts;

namespace SensEssentials.Quasar.Services;

/// <summary>What Quasar last heard from one server.</summary>
public sealed class ServerState
{
    public string ServerId { get; init; } = "";
    public string DisplayName { get; set; } = "";
    public bool Online { get; set; }

    /// <summary>Cluster nodes are configured through the cluster's own plugin configuration, not shared configs.</summary>
    public bool IsCluster { get; set; }

    public SharedConfigStatus? Status { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? CheckedUtc { get; set; }
}

/// <summary>
/// Keeps servers on their shared config. Every 30 seconds, and right after a config or assignment changes, it asks
/// each connected server which config it follows and sends the config to servers that are behind. Servers that were
/// offline or restarted are therefore caught up as soon as they connect. Servers that follow a config they are no
/// longer assigned to are released, so they can be edited on their own again.
/// </summary>
public sealed class SharedConfigSync : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IQuasarCompanionChannel _channel;
    private readonly SharedConfigStore _store;
    private readonly ILogger<SharedConfigSync> _log;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly Lock _sync = new();
    private readonly Dictionary<string, ServerState> _servers = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public SharedConfigSync(IQuasarCompanionChannel channel, SharedConfigStore store, ILogger<SharedConfigSync> log)
    {
        _channel = channel;
        _store = store;
        _log = log;
        _store.Changed += SyncNow;
    }

    /// <summary>Connected servers, then assigned servers that are offline.</summary>
    public IReadOnlyList<ServerState> GetServers()
    {
        var assignments = _store.GetAssignments();
        lock (_sync)
        {
            var result = _servers.Values.Where(s => s.Online).Select(Copy).ToList();
            foreach (var (serverId, assignment) in assignments)
            {
                if (result.Any(s => string.Equals(s.ServerId, serverId, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var known = _servers.GetValueOrDefault(serverId);
                result.Add(known != null ? Copy(known) : new ServerState { ServerId = serverId, DisplayName = assignment.DisplayName });
            }
            return result.OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }

    public void SyncNow()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already scheduled.
        }
    }

    /// <summary>A server's current Sens Essentials settings, to start a shared config from.</summary>
    public async Task<SharedConfigValues> ExportAsync(string serverId, CancellationToken cancellationToken)
    {
        var export = await _channel.SendAsync<EmptyPayload, ServerConfigExport>(serverId, SharedConfigOperations.CompanionId,
            SharedConfigOperations.Export, new EmptyPayload(), cancellationToken);
        if (export == null || string.IsNullOrWhiteSpace(export.ValuesJson))
            throw new InvalidOperationException("The server sent no settings.");
        return SharedConfigValues.FromJson(export.ValuesJson);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                _log.LogError(e, "Sens Essentials shared config sync failed");
            }

            try
            {
                await _wake.WaitAsync(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        // Without the store file, every server would look unassigned and be released.
        if (_store.LoadError != null)
            return;

        var connected = _channel.GetConnectedServers();
        lock (_sync)
        {
            foreach (var state in _servers.Values)
                state.Online = false;
            foreach (var server in connected)
            {
                if (!_servers.TryGetValue(server.ServerId, out var state))
                    _servers[server.ServerId] = state = new ServerState { ServerId = server.ServerId };
                state.DisplayName = server.DisplayName;
                state.Online = true;
                state.IsCluster = !string.IsNullOrEmpty(server.ClusterId);
            }
        }
        Changed?.Invoke();

        await Task.WhenAll(connected.Where(s => string.IsNullOrEmpty(s.ClusterId)).Select(s => SyncServerAsync(s, cancellationToken)));
        Changed?.Invoke();
    }

    private async Task SyncServerAsync(QuasarCompanionServer server, CancellationToken cancellationToken)
    {
        SharedConfigStatus? status = null;
        string? error = null;
        try
        {
            status = await SendAsync(server.ServerId, SharedConfigOperations.Status, new EmptyPayload(), cancellationToken);
            if (status.Installed)
            {
                var assignment = _store.GetAssignments().GetValueOrDefault(server.ServerId);
                var config = assignment == null ? null : _store.GetConfig(assignment.ConfigId);
                if (assignment != null)
                    _store.RememberName(server.ServerId, server.DisplayName);

                if (config != null && (status.ConfigId != config.Id || status.Revision != config.Revision))
                {
                    status = await SendAsync(server.ServerId, SharedConfigOperations.Apply, new ApplySharedConfigRequest
                    {
                        ConfigId = config.Id,
                        Name = config.Name,
                        Revision = config.Revision,
                        ValuesJson = config.Values.ToJson()
                    }, cancellationToken);
                    _log.LogInformation("Sent Sens Essentials shared config '{Config}' revision {Revision} to {Server}", config.Name, config.Revision, server.DisplayName);
                }
                else if (config == null && !string.IsNullOrEmpty(status.ConfigId))
                {
                    status = await SendAsync(server.ServerId, SharedConfigOperations.Release, new EmptyPayload(), cancellationToken);
                    _log.LogInformation("{Server} no longer follows a Sens Essentials shared config", server.DisplayName);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            error = e.Message;
        }

        lock (_sync)
        {
            if (_servers.TryGetValue(server.ServerId, out var state))
            {
                if (status != null)
                    state.Status = status;
                state.Error = error;
                state.CheckedUtc = DateTimeOffset.UtcNow;
            }
        }
    }

    private Task<SharedConfigStatus> SendAsync<TRequest>(string serverId, string operation, TRequest request, CancellationToken cancellationToken) =>
        _channel.SendAsync<TRequest, SharedConfigStatus>(serverId, SharedConfigOperations.CompanionId, operation, request, cancellationToken);

    private static ServerState Copy(ServerState state) => new()
    {
        ServerId = state.ServerId,
        DisplayName = state.DisplayName,
        Online = state.Online,
        IsCluster = state.IsCluster,
        Status = state.Status,
        Error = state.Error,
        CheckedUtc = state.CheckedUtc
    };
}
