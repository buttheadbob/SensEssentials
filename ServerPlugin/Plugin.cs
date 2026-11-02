using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using HarmonyLib;
using PluginSdk;
using PluginSdk.Commands;
using PluginSdk.Config;
using PluginSdk.Logging;
using Sandbox.Game;
using Sandbox.Game.World;
using SensEssentials.Discord;
using SensEssentials.Patches;
using VRage.FileSystem;
using VRage.Game;
using VRage.Plugins;
using VRageMath;

namespace SensEssentials.MagnetarHost
{
    /// <summary>
    /// Magnetar host: Quasar config, PluginSdk commands and Harmony patches. Everything else is in the shared code.
    /// </summary>
    // ReSharper disable once UnusedType.Global
    public class Plugin : IPlugin, IEssentialsHost
    {
        public const string Name = "SensEssentials";
        private static readonly Logger PluginLog = Logger.Create(Name);
        private static readonly ConcurrentDictionary<string, Logger> Loggers = new();

        private MagnetarConfig _config = null!;
        private string _configPath = null!;
        private readonly EssentialsConfig _shared = new();

        // Shared config from the Sens Essentials Quasar plugin, or null when this server is configured on its own.
        private SharedConfigState? _sharedConfig;
        private string _sharedConfigPath = null!;
        private bool _applyingSharedConfig;
        private DateTime _lastUndoWarning;
        private string? _lastRefusedSharedConfig;

        // Quasar finds the config through this public PluginConfig-typed property and renders its editor.
        public MagnetarConfig PluginConfig => _config;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Init(object gameInstance)
        {
            PluginLog.Info("Loading");
            EssentialsCore.Host = this;

            Directory.CreateDirectory(StorageDir);
            _configPath = Path.Combine(StorageDir, Name + ".cfg");
            try
            {
                _config = ConfigStorage.LoadXml<MagnetarConfig>(_configPath);
            }
            catch (Exception e)
            {
                PluginLog.Error("Could not read the config, using defaults", e);
                _config = new MagnetarConfig();
            }
            LoadSharedConfig();
            ApplyConfig();
            // Edits made in Quasar arrive through the config's setters: apply and persist them.
            _config.PropertyChanged += OnConfigChanged;

            Motd.Init(StorageDir);

            try
            {
                DiscordLogging.ServerName = Name;
                DiscordLogging.Start(_shared);
                Logger.EntryEmitted += OnLogEntry;
            }
            catch (Exception e)
            {
                PluginLog.Error("Failed to start Discord logging", e);
            }

            ApplyPatches();

            MySession.OnLoading += OnSessionLoading;
            MySession.AfterLoading += OnSessionLoaded;
            MySession.OnUnloading += OnSessionUnloading;

            ServerCommands.Register(Assembly.GetExecutingAssembly());
            SharedConfigBridge.Plugin = this;
            PluginLog.Info("Loaded (Magnetar)");
        }

        public void Update()
        {
            EssentialsSession.Update();
        }

        public void Dispose()
        {
            // IMPORTANT: Do NOT call harmony.UnpatchAll() here, it may break other plugins.
            SharedConfigBridge.Plugin = null;
            MySession.OnLoading -= OnSessionLoading;
            MySession.AfterLoading -= OnSessionLoaded;
            MySession.OnUnloading -= OnSessionUnloading;
            Logger.EntryEmitted -= OnLogEntry;
            EssentialsSession.Dispose();

            if (_config != null)
                _config.PropertyChanged -= OnConfigChanged;
        }

        private void ApplyPatches()
        {
            var harmony = new Harmony(Name);
            try
            {
                harmony.Patch(SessionDownloadPatch.Target, postfix: new HarmonyMethod(SessionDownloadPatch.Suffix));
            }
            catch (Exception e)
            {
                PluginLog.Error("Failed to patch the client world download. Client world data will not be trimmed.", e);
            }

            if (_shared.CutGameTags)
            {
                try
                {
                    harmony.Patch(GameTagsPatch.Target, prefix: new HarmonyMethod(GameTagsPatch.Prefix));
                }
                catch (Exception e)
                {
                    PluginLog.Error("Failed to patch the server browser game tags.", e);
                }
            }
        }

        private static void OnSessionLoading() => EssentialsSession.GameLoading();

        private static void OnSessionLoaded() => EssentialsSession.Loaded();

        private static void OnSessionUnloading() => EssentialsSession.Unloading();

        private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_applyingSharedConfig)
                return;

            if (_sharedConfig != null)
            {
                // This server follows a shared config: undo edits made here, they belong in the shared config.
                UndoLocalEdit();
                return;
            }

            ApplyConfig();
            SaveConfig();
        }

        #region Shared config

        private void LoadSharedConfig()
        {
            _sharedConfigPath = Path.Combine(StorageDir, Name + ".shared.xml");
            try
            {
                _sharedConfig = SharedConfigState.Load(_sharedConfigPath);
            }
            catch (Exception e)
            {
                PluginLog.Error("Could not read the shared config state, this server is configured on its own until Quasar sends it again", e);
                _sharedConfig = null;
            }

            if (_sharedConfig == null)
                return;

            // The config file was saved with these values, but put them back in case it was edited by hand.
            var error = CopySharedValues(_sharedConfig.ValuesJson, _sharedConfig.Name);
            if (error != null)
            {
                // Not following it lets Quasar see the server as behind and send the config again.
                PluginLog.Error($"Could not use shared config '{_sharedConfig.Name}', this server is configured on its own until Quasar sends it again: {error}");
                _sharedConfig = null;
            }
            else
                PluginLog.Info($"Following shared config '{_sharedConfig.Name}' (revision {_sharedConfig.Revision})");
        }

        internal string[] GetSharedStatus()
        {
            var version = typeof(Plugin).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            var state = _sharedConfig;
            return state == null
                ? ["", "", "0", version]
                : [state.ConfigId, state.Name, state.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), version];
        }

        internal string? ApplySharedConfig(string configId, string name, long revision, string valuesJson)
        {
            if (string.IsNullOrWhiteSpace(configId))
                return "The shared config has no id.";

            var error = CopySharedValues(valuesJson, name);
            if (error != null)
            {
                // Quasar retries every 30 seconds; log each refused revision once, not every retry.
                var refused = configId + "/" + revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (refused != _lastRefusedSharedConfig)
                {
                    _lastRefusedSharedConfig = refused;
                    PluginLog.Error($"Refused shared config '{name}' (revision {revision}): {error}");
                }
                return error;
            }

            _sharedConfig = new SharedConfigState { ConfigId = configId, Name = name ?? "", Revision = revision, ValuesJson = valuesJson };
            try
            {
                _sharedConfig.Save(_sharedConfigPath);
            }
            catch (Exception e)
            {
                PluginLog.Error("Could not save the shared config state, it is used until the server restarts", e);
            }

            ApplyConfig();
            SaveConfig();
            PluginLog.Info($"Applied shared config '{name}' (revision {revision})");
            return null;
        }

        internal void ReleaseSharedConfig()
        {
            if (_sharedConfig == null)
                return;

            var name = _sharedConfig.Name;
            _sharedConfig = null;
            try
            {
                if (File.Exists(_sharedConfigPath))
                    File.Delete(_sharedConfigPath);
            }
            catch (Exception e)
            {
                PluginLog.Error("Could not delete the shared config state", e);
            }

            _applyingSharedConfig = true;
            try
            {
                _config.SharedConfig = "";
            }
            finally
            {
                _applyingSharedConfig = false;
            }
            SaveConfig();
            PluginLog.Info($"No longer following shared config '{name}', settings can be edited here again");
        }

        private void UndoLocalEdit()
        {
            var state = _sharedConfig!;
            CopySharedValues(state.ValuesJson, state.Name);

            // The agent sets every option one by one, so warn once per edit instead of once per option.
            if (DateTime.UtcNow - _lastUndoWarning > TimeSpan.FromSeconds(10))
            {
                _lastUndoWarning = DateTime.UtcNow;
                PluginLog.Warning($"Undid a change made on this server: it follows shared config '{state.Name}'. Edit the shared config in Quasar instead.");
            }
        }

        /// <summary>
        /// Copies every option from a <see cref="MagnetarConfig"/> values JSON onto the live config, without treating
        /// it as a local edit. Returns null when done, otherwise why the JSON could not be used.
        /// </summary>
        private string? CopySharedValues(string valuesJson, string name)
        {
            MagnetarConfig values;
            try
            {
                values = ConfigStorage.LoadJson<MagnetarConfig>(valuesJson);
            }
            catch (Exception e)
            {
                return "The config could not be read: " + e.Message;
            }

            _applyingSharedConfig = true;
            try
            {
                foreach (var property in typeof(MagnetarConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!property.CanRead || !property.CanWrite || property.Name == nameof(MagnetarConfig.SharedConfig)
                        || property.GetCustomAttribute<ConfigOptionAttribute>(inherit: true) == null)
                        continue;
                    property.SetValue(_config, property.GetValue(values));
                }
                _config.SharedConfig = name ?? "";
            }
            finally
            {
                _applyingSharedConfig = false;
            }
            return null;
        }

        #endregion

        private void ApplyConfig()
        {
            try
            {
                _config.ApplyTo(_shared);
            }
            catch (Exception e)
            {
                PluginLog.Error("Could not apply the config", e);
            }
        }

        private static void OnLogEntry(LogEntry entry)
        {
            DiscordLogging.Submit(new DiscordLogEntry(
                ToLevel(entry.Level),
                entry.PluginName,
                entry.Message,
                entry.Exception,
                entry.UtcTimestamp.ToLocalTime()));
        }

        private static EssentialsLogLevel ToLevel(LogLevel level) => level switch
        {
            LogLevel.Debug => EssentialsLogLevel.Debug,
            LogLevel.Info => EssentialsLogLevel.Info,
            LogLevel.Warning => EssentialsLogLevel.Warn,
            LogLevel.Error => EssentialsLogLevel.Error,
            _ => EssentialsLogLevel.Fatal
        };

        #region IEssentialsHost

        public EssentialsConfig Config => _shared;

        public string StorageDir => Path.Combine(MyFileSystem.UserDataPath, Name);

        public bool IsServerRunning => MySession.Static?.Ready == true;

        public void SaveConfig()
        {
            try
            {
                ConfigStorage.SaveXml(_config, _configPath);
            }
            catch (Exception e)
            {
                PluginLog.Error("Could not save the config", e);
            }
        }

        public void Log(EssentialsLogLevel level, string channel, string message, Exception? exception)
        {
            var logger = Loggers.GetOrAdd(channel, Logger.Create);
            switch (level)
            {
                case EssentialsLogLevel.Trace:
                case EssentialsLogLevel.Debug:
                    logger.Debug(exception == null ? message : $"{message}: {exception}");
                    break;
                case EssentialsLogLevel.Info:
                    logger.Info(exception == null ? message : $"{message}: {exception}");
                    break;
                case EssentialsLogLevel.Warn:
                    logger.Warning(exception == null ? message : $"{message}: {exception}");
                    break;
                case EssentialsLogLevel.Error:
                    logger.Error(message, exception);
                    break;
                default:
                    logger.Critical(message, exception);
                    break;
            }
        }

        public bool RunCommand(string commandLine)
        {
            var caller = new CommandCaller(0, 0, "Server", VRage.Game.ModAPI.MyPromoteLevel.Owner, isConsole: true);
            return ServerCommands.Execute(commandLine, caller, LogResponder.Instance);
        }

        public bool ShowDialog(ulong steamId, string title, string? subtitle, string content)
        {
            return MissionScreens.IsHostSenderAvailable && MissionScreens.ShowToSteam(steamId, title, null, subtitle ?? "", content);
        }

        public void SendChat(string? author, string message, Color? color, ulong targetSteamId)
        {
            long identityId = 0;
            if (targetSteamId != 0)
            {
                identityId = MySession.Static?.Players.TryGetIdentityId(targetSteamId) ?? 0;
                // A player without an identity cannot receive a private message; never broadcast it instead.
                if (identityId == 0)
                    return;
            }

            MyVisualScriptLogicProvider.SendChatMessageColored(message, color ?? Color.White, author ?? "Server", identityId, MyFontEnum.White);
        }

        public void NotifyVoxelsReset(long[] entityIds)
        {
            // Magnetar has no client mod that reloads voxel maps; players see the reset after reconnecting.
        }

        #endregion

        /// <summary>
        /// Replies to commands run by auto commands and presets go to the log, since there is no console to show them.
        /// </summary>
        private sealed class LogResponder : ICommandResponder
        {
            public static readonly LogResponder Instance = new();

            public void Send(in CommandReply reply, in CommandCaller caller)
            {
                if (reply.HasContent)
                    PluginLog.Info(reply.Text);
            }
        }
    }
}
