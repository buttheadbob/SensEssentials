using System;
using System.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;
using SensEssentials.Discord;

namespace SensEssentials.TorchHost;

/// <summary>
/// NLog target that hands every log entry (game, Torch and plugins) to the shared Discord logging.
/// </summary>
[Target(TargetName)]
public sealed class DiscordLogTarget : Target
{
    private const string TargetName = "SensEssentialsDiscord";
    private static LoggingRule? _rule;
    private static EssentialsLogLevel? _level;

    /// <summary>
    /// Keeps the log rule in step with the settings. A rule makes NLog build every entry at its level for all of
    /// Torch and the plugins, so it only exists while a handler wants those entries, and at the lowest level any uses.
    /// </summary>
    public static void Update(EssentialsConfig config)
    {
        var handlers = config.DiscordLoggingEnabled ? config.DiscordLogHandlers.Where(h => h.Enabled).ToList() : [];
        EssentialsLogLevel? wanted = handlers.Count > 0 ? handlers.Min(h => h.MinLevel) : null;
        if (wanted == _level)
            return;

        Uninstall();
        _level = wanted;
        if (wanted != null)
            Install(NLog.LogLevel.FromOrdinal((int)wanted.Value));
    }

    private static void Install(NLog.LogLevel level)
    {
        // Assigning the configuration again makes NLog initialise the new target.
        var nlog = LogManager.Configuration ?? new LoggingConfiguration();
        var target = new DiscordLogTarget { Name = TargetName };
        nlog.AddTarget(TargetName, target);
        _rule = new LoggingRule("*", level, target);
        // First, so rules marked final further down do not hide entries from Discord.
        nlog.LoggingRules.Insert(0, _rule);
        LogManager.Configuration = nlog;
    }

    public static void Uninstall()
    {
        if (_rule == null)
            return;

        var nlog = LogManager.Configuration;
        if (nlog != null)
        {
            nlog.LoggingRules.Remove(_rule);
            nlog.RemoveTarget(TargetName);
            LogManager.Configuration = nlog;
        }

        _rule = null;
    }

    protected override void Write(LogEventInfo logEvent)
    {
        try
        {
            DiscordLogging.Submit(new DiscordLogEntry(
                (EssentialsLogLevel)Math.Min(Math.Max(logEvent.Level.Ordinal, 0), 5),
                logEvent.LoggerName,
                logEvent.FormattedMessage,
                logEvent.Exception,
                logEvent.TimeStamp));
        }
        catch (Exception)
        {
            // Logging from here could recurse into this target.
        }
    }
}
