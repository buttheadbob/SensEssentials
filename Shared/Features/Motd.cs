using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

    // Players who already got the MOTD this connection. Only touched on the game thread.
    private static readonly HashSet<ulong> ShownThisConnection = [];
    private static KnownIdsStorage? _knownIds;

    public static void Init(string storageDir)
    {
        _knownIds = new KnownIdsStorage(Path.Combine(storageDir, KnownIdsFileName));
        _knownIds.Read();
    }

    public static void Clear() => ShownThisConnection.Clear();

    public static void OnPlayerLeft(ulong steamId) => ShownThisConnection.Remove(steamId);

    /// <summary>
    /// Waits in the background until the player has a character, then shows the MOTD on the game thread.
    /// </summary>
    public static void OnPlayerJoined(ulong steamId)
    {
        //TODO: there is no player spawned event, so this polls. It gives up after five minutes.
        Task.Run(() =>
        {
            try
            {
                var start = DateTime.Now;
                var timeout = TimeSpan.FromMinutes(5);
                var pid = new MyPlayer.PlayerId(steamId, 0);
                while (DateTime.Now - start <= timeout)
                {
                    var players = MySession.Static?.Players;
                    if (players == null)
                        return;

                    if (!players.TryGetPlayerById(pid, out MyPlayer p) || p.Character == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    GameThread.InvokeSafe(() =>
                    {
                        if (!ShownThisConnection.Add(steamId))
                            return;

                        Send(p, true);
                    });
                    break;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to send the MOTD to {steamId}");
            }
        });
    }

    public static void Send(MyPlayer player, bool onSessionChanged)
    {
        var config = EssentialsCore.Config;
        var identity = player.Identity;
        if (identity == null || player.Client == null)
            return;

        long playerId = identity.IdentityId;

        if (!string.IsNullOrEmpty(config.MotdUrl) && !config.NewUserMotdUrl)
        {
            MyVisualScriptLogicProvider.OpenSteamOverlay(MakeUrl(config.MotdUrl), playerId);
            return;
        }

        var id = player.Client.SteamUserId;
        if (id <= 0) return;

        string name = identity.DisplayName ?? "player";

        bool isNewUser = _knownIds != null && !_knownIds.Contains(id);
        if (isNewUser)
            _knownIds!.Add(id);

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
        return $"https://steamcommunity.com/linkfilter/?url={url}";
    }

    private static string GetDefaultMotdText()
    {
        try
        {
            var motdDataStruct = typeof(MySpaceRespawnComponent).GetNestedType("MOTDData", BindingFlags.NonPublic)!;
            var motdConstructFunc = motdDataStruct.GetMethod("Construct", BindingFlags.Public | BindingFlags.Static)!;
            var motdMessageField = motdDataStruct.GetField("Message", BindingFlags.Public | BindingFlags.Instance)!;
            var motd = motdConstructFunc.Invoke(null, []);
            return motdMessageField.GetValue(motd) as string ?? "";
        }
        catch (Exception e)
        {
            var tag = $"#{new Random().NextDouble() * 1000:0}";
            Log.Error(e, $"Failed to load MotD. Tag: {tag}.");
            return $"Failed to load MotD. Contact admin.\nAdmin: text search in log: {tag}";
        }
    }
}
