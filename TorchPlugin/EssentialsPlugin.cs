using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using NLog;
using Sandbox;
using SensEssentials.Discord;
using SensEssentials.Patches;
using SensEssentials.TorchHost.Commands;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Commands;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Server;
using Torch.Session;
using VRage.Game;
using VRageMath;

namespace SensEssentials.TorchHost
{
    /// <summary>
    /// Torch host: config file, WPF tab, Torch commands and patches. Everything else is in the shared code.
    /// </summary>
    public class EssentialsPlugin : TorchPluginBase, IWpfPlugin, IEssentialsHost
    {
        public EssentialsConfig Config => _config?.Data!;

        private TorchSessionManager _sessionManager = null!;

        private UserControl _control = null!;
        private Persistent<EssentialsConfig> _config = null!;
        private static readonly Logger Log = LogManager.GetLogger("SensEssentials");
        private PatchManager _pm = null!;
        private PatchContext _context = null!;

        // The command manager the permission hook was attached to, so it is detached from the same instance.
        private CommandManager? _cmdMan;

        public static EssentialsPlugin Instance { get; private set; } = null!;

        /// <inheritdoc />
        public UserControl GetControl() => _control ??= new EssentialsControl(this);

        public void Save()
        {
            _config.Save();
        }

        /// <inheritdoc />
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;
            EssentialsCore.Host = this;

            string path = Path.Combine(StoragePath, "SensEssentials.cfg");
            CopyLegacyFile("Essentials.cfg", path);
            Log.Info($"Attempting to load config from {path}");
            _config = Persistent<EssentialsConfig>.Load(path);

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
                DiscordLogTarget.Install();
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

            _pm = torch.Managers.GetManager<PatchManager>();
            _context = _pm.AcquireContext();

            try
            {
                _context.GetPattern(SessionDownloadPatch.Target).Suffixes.Add(SessionDownloadPatch.Suffix);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to patch the client world download. Client world data will not be trimmed.");
            }

            if (Config.CutGameTags)
            {
                try
                {
                    _context.GetPattern(GameTagsPatch.Target).Prefixes.Add(GameTagsPatch.Prefix);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to patch the server browser game tags.");
                }
            }

            torch.GameStateChanged += TorchOnGameStateChanged;
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
            if (_cmdMan != null)
                _cmdMan.OnCommandExecuting += CommandPermissionHook.OnCommandExecuting;

            _control?.Dispatcher.Invoke(() =>
            {
                _control.IsEnabled = true;
                _control.DataContext = Config;
            });

            InfoModule.Init();
            EssentialsSession.Loaded();
        }

        private void OnSessionUnloading()
        {
            if (_cmdMan != null)
            {
                _cmdMan.OnCommandExecuting -= CommandPermissionHook.OnCommandExecuting;
                _cmdMan = null;
            }

            EssentialsSession.Unloading();
        }

        public static void InsertDiscordID(ulong steamID, string discordID, string discordName, Dictionary<ulong,string> RoleData) {
            //removed for now
        }

        public override void Update()
        {
            EssentialsSession.Update();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            EssentialsSession.Dispose();
            AdminModule.StopMuteTimer();
            DiscordLogTarget.Uninstall();
            Torch.GameStateChanged -= TorchOnGameStateChanged;
            if (_sessionManager != null)
                _sessionManager.SessionStateChanged -= SessionChanged;
            _sessionManager = null!;
        }

        #region IEssentialsHost

        string IEssentialsHost.StorageDir => StoragePath;

        bool IEssentialsHost.IsServerRunning => ((TorchServer)Torch).State == ServerState.Running;

        void IEssentialsHost.SaveConfig() => Save();

        void IEssentialsHost.Log(EssentialsLogLevel level, string channel, string message, Exception? exception)
        {
            LogManager.GetLogger(channel).Log(NLog.LogLevel.FromOrdinal((int)level), exception, message);
        }

        bool IEssentialsHost.RunCommand(string commandLine)
        {
            var manager = Torch.CurrentSession?.Managers?.GetManager<CommandManager>();
            return manager?.HandleCommandFromServer(commandLine) != null;
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

        void IEssentialsHost.NotifyVoxelsReset(long[] entityIds)
        {
            ModCommunication.SendMessageToClients(new VoxelResetMessage(entityIds));
        }

        #endregion
    }
}
