using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SensEssentials;

/// <summary>
/// Sends log entries that match its filters to one Discord webhook.
/// The list settings are comma separated text, parsed once whenever they change rather than for every log entry.
/// </summary>
public class DiscordLogHandler : ObservableObject
{
    private string[] _loggers = [];
    private string[] _ignoredLoggers = [];
    private ulong[] _roleIds = [];
    private ulong[] _userIds = [];
    private bool _badRoleIds;
    private bool _badUserIds;
    private bool _validUrl;

    public string Name { get; set => SetValue(ref field, value ?? ""); } = "";

    public bool Enabled { get; set => SetValue(ref field, value); } = true;

    public string WebhookUrl
    {
        get;
        set
        {
            _validUrl = IsWebhookUrl(value?.Trim() ?? "");
            SetValue(ref field, value?.Trim() ?? "");
        }
    } = "";

    public EssentialsLogLevel MinLevel { get; set => SetValue(ref field, value); } = EssentialsLogLevel.Warn;

    public EssentialsLogLevel MaxLevel { get; set => SetValue(ref field, value); } = EssentialsLogLevel.Fatal;

    /// <summary>
    /// Only these loggers are sent. Empty sends every logger. A trailing * matches by prefix.
    /// </summary>
    public string LoggerNames
    {
        get;
        set
        {
            SetValue(ref field, value ?? "");
            _loggers = SplitNames(field);
        }
    } = "";

    /// <summary>
    /// These loggers are never sent. A trailing * matches by prefix.
    /// </summary>
    public string IgnoredLoggerNames
    {
        get;
        set
        {
            SetValue(ref field, value ?? "");
            _ignoredLoggers = SplitNames(field);
        }
    } = "";

    public string RolePings
    {
        get;
        set
        {
            _badRoleIds = !TryParseIds(value ?? "", out _roleIds);
            SetValue(ref field, value ?? "");
        }
    } = "";

    public string UserPings
    {
        get;
        set
        {
            _badUserIds = !TryParseIds(value ?? "", out _userIds);
            SetValue(ref field, value ?? "");
        }
    } = "";

    public bool OnlyWhenServerOnline { get; set => SetValue(ref field, value); }

    internal IReadOnlyList<ulong> RoleIds => _roleIds;

    internal IReadOnlyList<ulong> UserIds => _userIds;

    internal bool CanSend => Enabled && _validUrl;

    /// <summary>
    /// True when an entry with this level and logger name should be sent by this handler.
    /// </summary>
    internal bool Matches(int levelOrdinal, string loggerName)
    {
        if (levelOrdinal < (int)MinLevel || levelOrdinal > (int)MaxLevel)
            return false;

        if (_loggers.Length > 0 && !_loggers.Any(n => NameMatches(n, loggerName)))
            return false;

        return !_ignoredLoggers.Any(n => NameMatches(n, loggerName));
    }

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? "(unnamed)" : Name;

    /// <summary>
    /// Problems that stop this handler from sending, or null when it is ready.
    /// </summary>
    public string? Problem
    {
        get
        {
            if (string.IsNullOrWhiteSpace(WebhookUrl))
                return "Enter the Discord webhook URL.";
            if (!IsWebhookUrl(WebhookUrl))
                return "The webhook URL must look like https://discord.com/api/webhooks/...";
            if (MinLevel > MaxLevel)
                return "Min Level is higher than Max Level, so nothing is sent.";
            if (_badRoleIds)
                return "Role Pings must be numeric role IDs separated by commas.";
            if (_badUserIds)
                return "User Pings must be numeric user IDs separated by commas.";
            return null;
        }
    }

    protected override void OnPropertyChanged([CallerMemberName] string propName = "")
    {
        base.OnPropertyChanged(propName);
        if (propName != nameof(Problem))
            base.OnPropertyChanged(nameof(Problem));
    }

    /// <summary>
    /// Only Discord's own hosts, so a typo or a bad config never sends the server's logs anywhere else.
    /// </summary>
    internal static bool IsWebhookUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
               && WebhookHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
               && uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.OrdinalIgnoreCase);
    }

    internal static readonly string[] WebhookHosts =
        ["discord.com", "ptb.discord.com", "canary.discord.com", "discordapp.com", "ptb.discordapp.com", "canary.discordapp.com"];

    private static bool NameMatches(string pattern, string loggerName)
    {
        return pattern.EndsWith("*")
            ? loggerName.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.OrdinalIgnoreCase)
            : string.Equals(pattern, loggerName, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] SplitNames(string text)
    {
        return [.. text.Split([','], StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)];
    }

    private static bool TryParseIds(string text, out ulong[] ids)
    {
        var result = new List<ulong>();
        bool ok = true;
        foreach (var part in text.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (ulong.TryParse(part.Trim(), out var id))
                result.Add(id);
            else
                ok = false;
        }

        ids = [.. result];
        return ok;
    }
}
