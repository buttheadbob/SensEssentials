using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SensEssentials.Discord;

/// <summary>
/// One log entry handed over by the host's logging (NLog on Torch, the PluginSdk logger on Magnetar).
/// </summary>
public readonly struct DiscordLogEntry(EssentialsLogLevel level, string loggerName, string message, Exception? exception, DateTime timestamp)
{
    public EssentialsLogLevel Level { get; } = level;
    public string LoggerName { get; } = loggerName ?? "";
    public string Message { get; } = message ?? "";
    public Exception? Exception { get; } = exception;
    public DateTime Timestamp { get; } = timestamp;
}

/// <summary>
/// Sends log entries to Discord webhooks. The host hands over every log entry with <see cref="Submit"/>; the ones
/// a handler wants are queued and a background loop posts them, so logging never waits on Discord.
/// </summary>
public static class DiscordLogging
{
    // Entries from this logger are never sent, otherwise a failing webhook would report its own failure forever.
    public const string LoggerName = "SensEssentials Discord";

    private const int MaxQueued = 500;
    private const int MessageLimit = 1500;
    private const int ExceptionMessageLimit = 500;
    private static readonly TimeSpan GonePause = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(3);

    /// <summary>
    /// One webhook's queue. Each has its own sender, so a slow or failing webhook cannot hold up the others.
    /// </summary>
    private sealed class Webhook(string url)
    {
        public string Url { get; } = url;
        public ConcurrentQueue<DiscordMessage> Queue { get; } = new();
        public SemaphoreSlim Signal { get; } = new(0);
        public DateTime PausedUntil { get; set; }
        public Task? Sender { get; set; }
    }

    private static readonly Dictionary<string, Webhook> Webhooks = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, RateWindow> Windows = new(StringComparer.Ordinal);
    private static readonly object WindowLock = new();

    private static EssentialsConfig? _config;
    private static DiscordLogHandler[] _handlers = [];
    private static CancellationTokenSource? _cts;
    private static int _queued;
    private static int _queueFullDrops;
    private static volatile bool _stopping;

    /// <summary>
    /// Shown as the sender name in Discord, so several servers can share one channel.
    /// </summary>
    public static string ServerName { get; set; } = "Sens Essentials";

    /// <summary>
    /// Set while a world is loaded, for handlers that only send while the server is online.
    /// </summary>
    public static bool ServerOnline { get; set; }

    /// <summary>
    /// Starts the send loop. The host then passes every log entry to <see cref="Submit"/>.
    /// </summary>
    public static void Start(EssentialsConfig config)
    {
        if (_cts != null)
            return;

        _config = config;
        config.PropertyChanged += OnConfigChanged;
        RefreshHandlers();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        Task.Run(() => NoticeLoop(token), token);
    }

    /// <summary>
    /// Stops taking entries and gives what is queued a moment to go out, since the last entries before a shutdown
    /// are often the ones that matter.
    /// </summary>
    public static void Stop()
    {
        var cts = _cts;
        if (cts == null)
            return;

        _config?.PropertyChanged -= OnConfigChanged;
        _config = null;
        _handlers = [];

        _stopping = true;
        List<Webhook> webhooks;
        lock (WindowLock)
            webhooks = [.. Webhooks.Values];

        foreach (var webhook in webhooks)
            webhook.Signal.Release();

        try
        {
            Task.WaitAll([.. webhooks.Where(w => w.Sender != null).Select(w => w.Sender!)], StopWait);
        }
        catch (AggregateException)
        {
            // A sender ended on an error it has already reported.
        }

        cts.Cancel();
        _cts = null;
        lock (WindowLock)
            Webhooks.Clear();
        Interlocked.Exchange(ref _queued, 0);
        _stopping = false;
    }

    private static void LogWarn(string message, Exception? exception = null) =>
        Log.Write(EssentialsLogLevel.Warn, LoggerName, message, exception);

    /// <summary>
    /// Posts a test message straight to the handler's webhook, ignoring its filters. Returns a result to show the admin.
    /// </summary>
    public static async Task<string> SendTestAsync(DiscordLogHandler handler)
    {
        if (handler.Problem is { } problem)
            return problem;

        var message = new DiscordMessage
        {
            Username = ServerName,
            Title = "Test message",
            Description = $"Sens Essentials can post to this channel.\nHandler: **{Escape(handler.ToString())}**\nLevels: {handler.MinLevel} to {handler.MaxLevel}",
            Color = LevelColor(2),
            Timestamp = DateTime.Now,
            Content = PingText(handler),
            RoleIds = handler.RoleIds,
            UserIds = handler.UserIds
        };

        try
        {
            var (result, detail) = await DiscordWebhook.SendAsync(handler.WebhookUrl, message, CancellationToken.None).ConfigureAwait(false);
            return result == SendResult.Sent ? "Test message sent." : $"Test message failed: {detail}";
        }
        catch (Exception e)
        {
            return $"Test message failed: {e.GetBaseException().Message}";
        }
    }

    private static void OnConfigChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshHandlers();

    private static void RefreshHandlers()
    {
        try
        {
            var config = _config;
            _handlers = config == null || !config.DiscordLoggingEnabled
                ? []
                : [.. config.DiscordLogHandlers];
        }
        catch (InvalidOperationException)
        {
            // The list changed while being copied; that change raises another refresh.
        }
    }

    /// <summary>
    /// Called by the host for every log entry, from any thread. Quick, and never throws.
    /// </summary>
    public static void Submit(in DiscordLogEntry logEvent)
    {
        try
        {
            var handlers = _handlers;
            if (handlers.Length == 0)
                return;

            var loggerName = logEvent.LoggerName;
            if (loggerName.StartsWith(LoggerName, StringComparison.Ordinal))
                return;

            int level = (int)logEvent.Level;
            DiscordMessage? message = null;

            // Every matching handler gets the entry, so one handler can not hide entries from another.
            foreach (var handler in handlers)
            {
                if (!handler.CanSend || (handler.OnlyWhenServerOnline && !ServerOnline) || !handler.Matches(level, loggerName))
                    continue;

                if (!TryTakeSlot(handler))
                    continue;

                message ??= Format(logEvent);
                Enqueue(handler.WebhookUrl, WithPings(message, handler));
            }
        }
        catch (Exception)
        {
            // Logging from here could recurse into this target.
        }
    }

    private static void Enqueue(string url, DiscordMessage message)
    {
        var token = _cts?.Token;
        if (token == null)
            return;

        Webhook? webhook;
        lock (WindowLock)
        {
            if (!Webhooks.TryGetValue(url, out webhook))
            {
                var created = webhook = new Webhook(url);
                Webhooks[url] = created;
                created.Sender = Task.Run(() => SendLoop(created, token.Value));
            }
        }

        if (DateTime.UtcNow < webhook.PausedUntil)
            return;

        if (Interlocked.Increment(ref _queued) > MaxQueued)
        {
            Interlocked.Decrement(ref _queued);
            Interlocked.Increment(ref _queueFullDrops);
            return;
        }

        webhook.Queue.Enqueue(message);
        webhook.Signal.Release();
    }

    private static async Task NoticeLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, token).ConfigureAwait(false);
                QueueDropNotices();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                LogWarn("Could not report dropped Discord entries", e);
            }
        }
    }

    private static async Task SendLoop(Webhook webhook, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await webhook.Signal.WaitAsync(1000, token).ConfigureAwait(false);

                while (webhook.Queue.TryDequeue(out var message))
                {
                    Interlocked.Decrement(ref _queued);
                    var (result, detail) = await DiscordWebhook.SendAsync(webhook.Url, message, token).ConfigureAwait(false);
                    if (result == SendResult.Gone)
                    {
                        Pause(webhook, detail);
                        break;
                    }

                    if (result != SendResult.Sent)
                        LogWarn($"Could not post a log entry to Discord: {detail}");
                }

                if (_stopping)
                    return;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                LogWarn("Could not post a log entry to Discord", e);
            }
        }
    }

    /// <summary>
    /// A deleted webhook fails every time, so it is left alone for a while instead of being tried for every entry.
    /// </summary>
    private static void Pause(Webhook webhook, string detail)
    {
        webhook.PausedUntil = DateTime.UtcNow + GonePause;
        while (webhook.Queue.TryDequeue(out _))
            Interlocked.Decrement(ref _queued);

        LogWarn($"A Discord webhook no longer works, so it is skipped for {GonePause.TotalMinutes:0} minutes. Check its URL. {detail}");
    }

    #region Rate limit

    private sealed class RateWindow
    {
        public DateTime Start;
        public int Sent;
        public int Dropped;
        public DiscordLogHandler? LastHandler;
    }

    /// <summary>
    /// Per webhook, allows the configured number of entries a second and counts the rest as dropped.
    /// </summary>
    private static bool TryTakeSlot(DiscordLogHandler handler)
    {
        int limit = Math.Min(Math.Max(_config?.DiscordRateLimitPerSecond ?? 3, 1), 5);
        var now = DateTime.UtcNow;

        lock (WindowLock)
        {
            if (!Windows.TryGetValue(handler.WebhookUrl, out var window))
                Windows[handler.WebhookUrl] = window = new RateWindow { Start = now };

            window.LastHandler = handler;
            if (now - window.Start >= TimeSpan.FromSeconds(1))
            {
                window.Start = now;
                window.Sent = 0;
            }

            if (window.Sent >= limit)
            {
                window.Dropped++;
                return false;
            }

            window.Sent++;
            return true;
        }
    }

    /// <summary>
    /// Tells each channel how many entries were dropped by the rate limit or because the queue was full.
    /// </summary>
    private static void QueueDropNotices()
    {
        var notices = new List<(string Url, DiscordLogHandler Handler, int Dropped)>();
        var now = DateTime.UtcNow;

        lock (WindowLock)
        {
            foreach (var pair in Windows)
            {
                var window = pair.Value;
                if (window.Dropped == 0 || now - window.Start < TimeSpan.FromSeconds(1) || window.LastHandler == null)
                    continue;

                notices.Add((pair.Key, window.LastHandler, window.Dropped));
                window.Dropped = 0;
            }
        }

        foreach (var (url, handler, dropped) in notices)
            Enqueue(url, Notice($"Rate limit reached for handler **{Escape(handler.ToString())}**. Dropped {dropped} log entr{(dropped == 1 ? "y" : "ies")}."));

        int queueDrops = Interlocked.Exchange(ref _queueFullDrops, 0);
        if (queueDrops > 0)
            LogWarn($"Discord queue was full; dropped {queueDrops} log entries");
    }

    private static DiscordMessage Notice(string text) => new()
    {
        Username = ServerName,
        Description = text,
        Color = 0xFFA500
    };

    #endregion

    #region Formatting

    private static DiscordMessage Format(in DiscordLogEntry logEvent)
    {
        int stackLimit = Math.Min(Math.Max(_config?.DiscordStackTraceEmbedLimit ?? 2000, 0), 2500);
        var exception = logEvent.Exception;

        var sb = new StringBuilder();
        sb.AppendLine(CodeBlock(DiscordWebhook.Shorten(logEvent.Message, MessageLimit), "No message"));

        var cut = logEvent.Message.Length > MessageLimit;
        if (exception != null)
        {
            sb.AppendLine($"**{Escape(exception.GetType().FullName ?? "Exception")}**");
            sb.AppendLine(CodeBlock(DiscordWebhook.Shorten(exception.Message ?? "", ExceptionMessageLimit), "No exception message"));

            var inner = exception.GetBaseException();
            if (inner != exception)
                sb.AppendLine($"Caused by **{Escape(inner.GetType().FullName ?? "")}**: {Escape(DiscordWebhook.Shorten(inner.Message ?? "", 300))}");

            var stack = exception.StackTrace ?? "";
            if (stack.Length > 0 && stack.Length <= stackLimit)
                sb.AppendLine(CodeBlock(stack, ""));
            else if (stack.Length > 0)
                sb.AppendLine("Stack trace attached.");

            cut |= stack.Length > stackLimit || inner != exception || (exception.Message?.Length ?? 0) > ExceptionMessageLimit;
        }

        var description = sb.ToString();
        if (description.Length > DiscordWebhook.MaxDescription)
        {
            cut = true;
            description = DiscordWebhook.Shorten(description, DiscordWebhook.MaxDescription - 40) + "\nFull entry attached.";
        }

        // The whole entry, including inner exceptions, whenever any part had to be cut.
        var attachment = cut ? $"{logEvent.Message}\n\n{exception}".Trim() : null;

        return new DiscordMessage
        {
            Username = ServerName,
            Title = $"{logEvent.Level} · {logEvent.LoggerName}",
            Description = description,
            Color = LevelColor((int)logEvent.Level),
            Timestamp = logEvent.Timestamp,
            AttachmentName = attachment == null ? null : "log.txt",
            AttachmentText = attachment
        };
    }

    /// <summary>
    /// Pings go in the message text, since Discord does not notify for mentions inside an embed.
    /// </summary>
    private static DiscordMessage WithPings(DiscordMessage message, DiscordLogHandler handler)
    {
        var pings = PingText(handler);
        if (pings == null)
            return message;

        return new DiscordMessage
        {
            Username = message.Username,
            Title = message.Title,
            Description = message.Description,
            Color = message.Color,
            Timestamp = message.Timestamp,
            AttachmentName = message.AttachmentName,
            AttachmentText = message.AttachmentText,
            Content = pings,
            RoleIds = handler.RoleIds,
            UserIds = handler.UserIds
        };
    }

    private static string? PingText(DiscordLogHandler handler)
    {
        var pings = handler.RoleIds.Select(id => $"<@&{id}>").Concat(handler.UserIds.Select(id => $"<@{id}>")).ToList();
        return pings.Count == 0 ? null : string.Join(" ", pings);
    }

    private static string CodeBlock(string text, string empty)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.IsNullOrEmpty(empty) ? "" : $"*{empty}*";

        // A ``` inside the text would end the block early; a zero width space breaks it up.
        return $"```\n{text.Replace("```", "`​``")}\n```";
    }

    private static string Escape(string text) => text.Replace("*", "\\*").Replace("_", "\\_").Replace("`", "\\`");

    private static int LevelColor(int ordinal) => ordinal switch
    {
        5 => 0x8B0000, // Fatal
        4 => 0xE53935, // Error
        3 => 0xFFC107, // Warn
        2 => 0x43A047, // Info
        _ => 0x9E9E9E  // Debug, Trace
    };

    #endregion
}
