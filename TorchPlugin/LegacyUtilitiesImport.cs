using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NLog;

namespace SensEssentials.TorchHost;

/// <summary>
/// Imports the Discord log handlers and suit refill settings from the Senxs Utilities plugin, which was merged
/// into this one. Reads its XML directly, so that plugin does not need to be installed.
/// </summary>
internal static class LegacyUtilitiesImport
{
    private static readonly Logger Log = LogManager.GetLogger("SensEssentials");
    private const string FileName = "Senxs_UtilitiesConfig.cfg";

    /// <summary>
    /// Imports once. Returns true when the config was changed and should be saved.
    /// </summary>
    public static bool TryImport(string storagePath, EssentialsConfig config)
    {
        if (config.SenxsUtilitiesImported)
            return false;

        var path = Path.Combine(storagePath, FileName);
        if (!File.Exists(path))
            return false;

        try
        {
            var root = XDocument.Load(path).Root;
            if (root == null)
                return false;

            // The old plugin had a master switch that turned every feature off.
            bool master = Bool(root, "MasterSwitch", true);

            config.DiscordLoggingEnabled = master && Bool(root, "Log2Discord", true);
            config.DiscordRateLimitPerSecond = Int(root, "Log2DiscordRateLimitPerSecond", config.DiscordRateLimitPerSecond);
            config.DiscordStackTraceEmbedLimit = Int(root, "Log2DiscordStackTraceEmbedLimit", config.DiscordStackTraceEmbedLimit);

            // Added last: the config saves itself, so a failure partway must not leave handlers to be imported twice.
            var handlers = new List<DiscordLogHandler>();
            foreach (var h in Child(root, "LogHandlers")?.Elements().Where(e => e.Name.LocalName == "LogHandler") ?? [])
            {
                handlers.Add(new DiscordLogHandler
                {
                    Name = Text(h, "Name"),
                    WebhookUrl = Text(h, "DiscordWebHook"),
                    MinLevel = Level(Int(h, "MinLogLevel", 0)),
                    MaxLevel = Level(Int(h, "MaxLogLevel", 5)),
                    LoggerNames = List(h, "LogNames"),
                    IgnoredLoggerNames = List(h, "LogNames_Ignore"),
                    RolePings = List(h, "RolesToPing"),
                    UserPings = List(h, "MembersToPing"),
                    OnlyWhenServerOnline = Bool(h, "OnlyWhenServerOnline", false)
                });
            }

            config.SuitRefillEnabled = master && Bool(root, "AdminToolsEnable", false);
            config.SuitRefillEveryone = Bool(root, "IsLobby", false);
            config.SuitRefillOxygen = Bool(root, "AdminRefillOxygen", true);
            config.SuitRefillHydrogen = Bool(root, "AdminRefillHydrogen", true);
            config.SuitRefillEnergy = Bool(root, "AdminRefillEnergy", true);
            config.SuitRefillHealth = Bool(root, "AdminRefillHealth", true);
            config.SuitRefillFood = Bool(root, "AdminRefillFood", true);
            config.SuitRefillRadiationImmunity = Bool(root, "AdminResetRadiationImmunity", true);
            config.SuitResetRadiation = Bool(root, "AdminResetRadiation", true);

            foreach (var handler in handlers)
                config.DiscordLogHandlers.Add(handler);

            config.SenxsUtilitiesImported = true;
            Log.Warn($"Imported {handlers.Count} Discord log handler(s) and the suit refill settings from {path}. " +
                     "Remove the Senxs Utilities plugin, otherwise every log entry is posted to Discord twice.");
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, $"Could not import the Senxs Utilities settings from {path}");
            return false;
        }
    }

    private static XElement? Child(XElement parent, string name) => parent.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static string Text(XElement parent, string name) => Child(parent, name)?.Value.Trim() ?? "";

    private static bool Bool(XElement parent, string name, bool fallback) => bool.TryParse(Text(parent, name), out var v) ? v : fallback;

    private static int Int(XElement parent, string name, int fallback) => int.TryParse(Text(parent, name), out var v) ? v : fallback;

    // Lists were serialized as one element per value, for example <LogNames><string>Torch</string></LogNames>.
    private static string List(XElement parent, string name) =>
        string.Join(",", Child(parent, name)?.Elements().Select(e => e.Value.Trim()).Where(v => v.Length > 0) ?? []);

    private static EssentialsLogLevel Level(int ordinal) => (EssentialsLogLevel)Math.Min(Math.Max(ordinal, 0), 5);
}
