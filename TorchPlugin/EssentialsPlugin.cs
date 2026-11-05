using System;
using System.IO;
using System.Windows.Controls;
using NLog;
using Sandbox;
using SensEssentials.Discord;
using SensEssentials.TorchHost.Commands;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Commands;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Server;
using Torch.Session;
using VRage.Game;
using VRageMath;

namespace SensEssentials.TorchHost;

/// <summary>
/// Torch host: config file, WPF tab and Torch commands. Everything else is in the shared code.
/// </summary>
public class EssentialsPlugin : TorchPluginBase, IWpfPlugin, IEssentialsHost
{
    public EssentialsConfig Config => _config?.Data ?? throw new InvalidOperationException("Used before the config was loaded.");

    private TorchSessionManager? _sessionManager;

    private UserControl? _control;
    private ConfigFile? _config;
    private static readonly Logger Log = LogManager.GetLogger("SensEssentials");

    // The command manager the permission hook was attached to, so it is detached from the same instance.
    private CommandManager? _cmdMan;

    private static EssentialsPlugin? _instance;

    public static EssentialsPlugin Instance => _instance ?? throw new InvalidOperationException("Used before the plugin started.");

    /// <inheritdoc />
    public UserControl GetControl() => _control ??= new EssentialsControl(this);

    public void Save()
    {
        _config?.Save();
    }

    /// <inheritdoc />
    public override void Init(ITorchBase torch)
    {
        base.Init(torch);
        _instance = this;
        EssentialsCore.Host = this;

        string path = Path.Combine(StoragePath, "SensEssentials.cfg");
        CopyLegacyFile("Essentials.cfg", path);
        Log.Info($"Attempting to load config from {path}");
        _config = new ConfigFile(path);

        if (LegacyUtilitiesImport.TryImport(StoragePath, Config))
        {
            try
            {
                _config.Save();
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to save the imported Senxs Utilities settings");
            }
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(torch.Config.InstanceName))
                DiscordLogging.ServerName = torch.Config.InstanceName;
            DiscordLogging.Start(Config);
            DiscordLogTarget.Update(Config);
            Config.PropertyChanged += OnConfigChanged;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to start Discord logging");
        }

        CopyLegacyFile("Essentials.KnownSteamIds.txt", Path.Combine(StoragePath, Motd.KnownIdsFileName));
        Motd.Init(StoragePath);

        _sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
        if (_sessionManager != null)
            _sessionManager.SessionStateChanged += SessionChanged;
        else
            Log.Warn("No session manager.  MOTD won't work");

        torch.GameStateChanged += TorchOnGameStateChanged;
    }

    private void OnConfigChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        try
        {
            DiscordLogTarget.Update(Config);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update Discord logging");
        }
    }

    /// <summary>
    /// First run after the rename: copies the file the plugin used while it was called Essentials. The old file
    /// is copied rather than moved, so it is left for the original Essentials plugin if that is still installed.
    /// </summary>
    private void CopyLegacyFile(string legacyName, string path)
    {
        try
        {
            var legacyPath = Path.Combine(StoragePath, legacyName);
            if (File.Exists(path) || !File.Exists(legacyPath))
                return;

            File.Copy(legacyPath, path);
            Log.Info($"Copied {legacyPath} to {path}");
        }
        catch (Exception e)
        {
            Log.Error(e, $"Failed to copy {legacyName}; starting with a new file");
        }
    }

    private void TorchOnGameStateChanged(MySandboxGame game, TorchGameState newstate)
    {
        if (newstate == TorchGameState.Loading)
            EssentialsSession.GameLoading();
    }

    private void SessionChanged(ITorchSession session, TorchSessionState state)
    {
        // Torch rethrows anything thrown here, which stops the session from loading or unloading.
        try
        {
            switch (state)
            {
                case TorchSessionState.Loaded:
                    OnSessionLoaded(session);
                    break;

                case TorchSessionState.Unloading:
                    OnSessionUnloading();
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, $"Error handling session state {state}");
        }
    }

    private void OnSessionLoaded(ITorchSession session)
    {
        _cmdMan = session.Managers.GetManager<CommandManager>();
        _cmdMan?.OnCommandExecuting += CommandPermissionHook.OnCommandExecuting;

        InfoModule.Init();
        EssentialsSession.Loaded();
    }

    private void OnSessionUnloading()
    {
        _cmdMan?.OnCommandExecuting -= CommandPermissionHook.OnCommandExecuting;
        _cmdMan = null;

        EssentialsSession.Unloading();
    }

    public override void Update()
    {
        EssentialsSession.Update();
        AdminModule.UpdateMutes();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        EssentialsSession.Dispose();
        AdminModule.ClearMutes();
        DiscordLogTarget.Uninstall();
        if (_config != null)
        {
            Config.PropertyChanged -= OnConfigChanged;
            _config.Dispose();
        }
        Torch.GameStateChanged -= TorchOnGameStateChanged;
        _sessionManager?.SessionStateChanged -= SessionChanged;
        _sessionManager = null;
    }

    #region IEssentialsHost

    string IEssentialsHost.StorageDir => StoragePath;

    bool IEssentialsHost.IsServerRunning => ((TorchServer)Torch).State == ServerState.Running;

    void IEssentialsHost.Log(EssentialsLogLevel level, string channel, string message, Exception? exception)
    {
        LogManager.GetLogger(channel).Log(NLog.LogLevel.FromOrdinal((int)level), exception, message);
    }

    bool IEssentialsHost.RunCommand(string commandLine)
    {
        var manager = Torch.CurrentSession?.Managers?.GetManager<CommandManager>();
        // The overload without a callback collects replies in a list nobody reads.
        return manager != null && manager.HandleCommandFromServer(commandLine, reply => Log.Info($"{commandLine}: {reply.Message}"));
    }

    bool IEssentialsHost.ShowDialog(ulong steamId, string title, string? subtitle, string content)
    {
        ModCommunication.SendMessageTo(new DialogMessage(title, subtitle, content), steamId);
        return true;
    }

    void IEssentialsHost.SendChat(string? author, string message, Color? color, ulong targetSteamId)
    {
        var chat = Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>();
        if (chat == null)
            return;

        if (author == null && color == null)
        {
            if (targetSteamId == 0)
                chat.SendMessageAsSelf(message);
            else
                chat.SendMessageAsOther(null, message, targetSteamId: targetSteamId);
            return;
        }

        chat.SendMessageAsOther(author, message, color ?? Color.White, targetSteamId, MyFontEnum.White);
    }

    // Torch keeps backslashes as they are, and has no way to put a quote inside a quoted word.
    string IEssentialsHost.QuoteArgument(string text) => $"\"{text.Replace("\"", "")}\"";

    bool IEssentialsHost.CanReloadVoxels => true;

    void IEssentialsHost.NotifyVoxelsReset(long[] entityIds)
    {
        ModCommunication.SendMessageToClients(new VoxelResetMessage(entityIds));
    }

    #endregion
}
