using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SensEssentials.Discord;

/// <summary>
/// One Discord webhook post: an optional ping line, one embed and an optional text attachment.
/// </summary>
internal sealed class DiscordMessage
{
    public string? Username;
    public string? Content;
    public string? Title;
    public string Description = "";
    public int Color;
    public DateTime? Timestamp;
    public IReadOnlyList<ulong> RoleIds = [];
    public IReadOnlyList<ulong> UserIds = [];
    public string? AttachmentName;
    public string? AttachmentText;
}

internal enum SendResult
{
    Sent,
    RateLimited,
    Failed
}

/// <summary>
/// Posts messages to Discord webhooks. Never logs the webhook URL, since it works as a password for the channel.
/// </summary>
internal static class DiscordWebhook
{
    // Discord's documented limits.
    public const int MaxDescription = 4096;
    public const int MaxTitle = 256;
    private const int MaxUsername = 80;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>
    /// Sends one message. When Discord answers 429, waits the time it asks for and tries once more.
    /// </summary>
    public static async Task<(SendResult Result, string Detail)> SendAsync(string url, DiscordMessage message, CancellationToken token)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = BuildContent(message) };
            using var response = await Http.SendAsync(request, token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return (SendResult.Sent, "");

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if ((int)response.StatusCode != 429)
                return (SendResult.Failed, $"{(int)response.StatusCode} {response.StatusCode}: {Shorten(body, 300)}");

            if (attempt == 1)
                return (SendResult.RateLimited, "Discord is still rate limiting this webhook.");

            await Task.Delay(GetRetryAfter(response, body), token).ConfigureAwait(false);
        }

        return (SendResult.Failed, "");
    }

    private static HttpContent BuildContent(DiscordMessage message)
    {
        var embed = new JObject
        {
            ["description"] = Shorten(message.Description, MaxDescription),
            ["color"] = message.Color
        };
        if (!string.IsNullOrEmpty(message.Title))
            embed["title"] = Shorten(message.Title!, MaxTitle);
        if (message.Timestamp != null)
            embed["timestamp"] = message.Timestamp.Value.ToUniversalTime().ToString("o");

        var payload = new JObject
        {
            ["embeds"] = new JArray(embed),
            // Only the listed roles and users can be pinged, never @everyone or anything typed in a log message.
            ["allowed_mentions"] = new JObject
            {
                ["parse"] = new JArray(),
                ["roles"] = new JArray(message.RoleIds.Select(id => id.ToString())),
                ["users"] = new JArray(message.UserIds.Select(id => id.ToString()))
            }
        };

        var username = CleanUsername(message.Username);
        if (username != null)
            payload["username"] = username;
        if (!string.IsNullOrEmpty(message.Content))
            payload["content"] = message.Content;

        var json = payload.ToString(Formatting.None);
        if (string.IsNullOrEmpty(message.AttachmentText))
            return new StringContent(json, Encoding.UTF8, "application/json");

        var form = new MultipartFormDataContent
        {
            { new StringContent(json, Encoding.UTF8, "application/json"), "payload_json" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(message.AttachmentText)), "files[0]", message.AttachmentName ?? "log.txt" }
        };
        return form;
    }

    /// <summary>
    /// Discord rejects usernames containing "discord" or "clyde", and longer than 80 characters.
    /// </summary>
    private static string? CleanUsername(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        foreach (var banned in new[] { "discord", "clyde" })
        {
            int i;
            while ((i = name!.IndexOf(banned, StringComparison.OrdinalIgnoreCase)) >= 0)
                name = name.Remove(i, banned.Length);
        }

        name = name!.Trim();
        return name.Length == 0 ? null : Shorten(name, MaxUsername);
    }

    private static TimeSpan GetRetryAfter(HttpResponseMessage response, string body)
    {
        double seconds = 1;
        try
        {
            if (response.Headers.RetryAfter?.Delta is { } delta)
                seconds = delta.TotalSeconds;
            else if (JObject.Parse(body)["retry_after"] is { } retry)
                seconds = retry.Value<double>();
        }
        catch (Exception)
        {
            // Unreadable answer: fall back to one second.
        }

        return TimeSpan.FromSeconds(Math.Min(Math.Max(seconds, 0.5), 30));
    }

    public static string Shorten(string text, int max)
    {
        if (text.Length <= max)
            return text;

        return max <= 1 ? text.Substring(0, max) : text.Substring(0, max - 1) + "…";
    }
}
