using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using SpaceEngineers.Game.World;

namespace SensEssentials;

/// <summary>
/// Message of the day: shown once per connection when the player first spawns, and on !motd.
/// </summary>
public static class Motd
{
    public const string KnownIdsFileName = "SensEssentials.KnownSteamIds.txt";

    private static readonly TimeSpan WaitLimit = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    // Everything below is only touched on the game thread.
    // Players who already got the MOTD this connection.
    private static readonly HashSet<ulong> ShownThisConnection = [];
    // Players still waiting for a character, and when to give up on them.
    private static readonly Dictionary<ulong, DateTime> Waiting = [];
    private static DateTime _nextCheck;
    private static KnownIdsStorage? _knownIds;

    public static void Init(string storageDir)
    {
        _knownIds = new KnownIdsStorage(Path.Combine(storageDir, KnownIdsFileName));
        _knownIds.Read();
    }

    public static void Clear()
    {
        ShownThisConnection.Clear();
        Waiting.Clear();
    }

    public static void OnPlayerLeft(ulong steamId)
    {
        ShownThisConnection.Remove(steamId);
        Waiting.Remove(steamId);
    }

    public static void OnPlayerJoined(ulong steamId)
    {
        // Update checks once a second for the character. It gives up after five minutes.
        Waiting[steamId] = DateTime.UtcNow + WaitLimit;
    }

    /// <summary>
    /// Every game tick. Once a second, shows the MOTD to any waiting player who now has a character.
    /// </summary>
    public static void Update()
    {
        if (Waiting.Count == 0 || DateTime.UtcNow < _nextCheck)
            return;

        _nextCheck = DateTime.UtcNow + CheckInterval;
        var players = MySession.Static?.Players;
        if (players == null)
            return;

        foreach (var pair in Waiting.ToArray())
        {
            if (!players.TryGetPlayerById(new MyPlayer.PlayerId(pair.Key, 0), out MyPlayer p) || p.Character == null)
            {
                if (DateTime.UtcNow > pair.Value)
                    Waiting.Remove(pair.Key);
                continue;
            }

            Waiting.Remove(pair.Key);
            if (ShownThisConnection.Add(pair.Key))
                Send(p, true);
        }
    }

    public static void Send(MyPlayer player, bool onSessionChanged)
    {
        var config = EssentialsCore.Config;
        var identity = player.Identity;
        if (identity == null || player.Client == null)
            return;

        long playerId = identity.IdentityId;
        var id = player.Client.SteamUserId;
        if (id <= 0) return;

        // First, so a player who only ever sees the URL is still known as seen.
        bool isNewUser = _knownIds != null && !_knownIds.Contains(id);
        if (isNewUser)
            _knownIds!.Add(id);

        if (!string.IsNullOrEmpty(config.MotdUrl) && !config.NewUserMotdUrl)
        {
            MyVisualScriptLogicProvider.OpenSteamOverlay(MakeUrl(config.MotdUrl), playerId);
            return;
        }

        string name = identity.DisplayName ?? "player";

        if (!string.IsNullOrEmpty(config.MotdUrl) && isNewUser && config.NewUserMotdUrl)
        {
            MyVisualScriptLogicProvider.OpenSteamOverlay(MakeUrl(config.MotdUrl), playerId);
            return;
        }

        if (isNewUser && !string.IsNullOrEmpty(config.NewUserMotd))
        {
            Show(id, "New User Message Of The Day", config.NewUserMotd.Replace("%player%", name));
            return;
        }

        if (!string.IsNullOrEmpty(config.Motd))
        {
            Show(id, "Message Of The Day", config.Motd.Replace("%player%", name));
            return;
        }

        if (!onSessionChanged) // otherwise default motd shows up twice on connection
            Show(id, "Message Of The Day", GetDefaultMotdText());
    }

    private static void Show(ulong steamId, string subtitle, string text)
    {
        if (!EssentialsCore.Host.ShowDialog(steamId, MySession.Static.Name, subtitle, text))
            EssentialsCore.Host.SendChat(null, text, null, steamId);
    }

    /// <summary>
    /// Whitelisted URLs open directly; anything else goes through Steam's link filter.
    /// </summary>
    public static string MakeUrl(string url)
    {
        if (MyGuiSandbox.IsUrlWhitelisted(url)) return url;
        return $"https://steamcommunity.com/linkfilter/?url={Uri.EscapeDataString(url)}";
    }

    private static string GetDefaultMotdText()
    {
        try
        {
            return MySpaceRespawnComponent.MOTDData.Construct().Message ?? "";
        }
        catch (Exception e)
        {
            var tag = $"#{new Random().NextDouble() * 1000:0}";
            Log.Error(e, $"Failed to load MotD. Tag: {tag}.");
            return $"Failed to load MotD. Contact admin.\nAdmin: text search in log: {tag}";
        }
    }
}
