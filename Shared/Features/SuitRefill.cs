using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.World;
using VRage.Game.ModAPI;

namespace SensEssentials;

/// <summary>
/// Keeps suits topped up (oxygen, hydrogen, energy, health, food, radiation) for admins, for players given a
/// timed refill with !admin refill, or for everyone in lobby mode. Runs from the plugin's Update on the game thread.
/// </summary>
public static class SuitRefill
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    // Steam ID -> when the timed refill ends. Only touched on the game thread.
    private static readonly Dictionary<ulong, DateTime> Timed = [];
    private static DateTime _nextRun = DateTime.MinValue;

    /// <summary>
    /// Refills the player for the given minutes, replacing any earlier time. Zero or less stops it.
    /// Returns the text to show the admin.
    /// </summary>
    public static string SetTimedRefill(ulong steamId, string playerName, int minutes)
    {
        if (minutes <= 0)
        {
            return Timed.Remove(steamId)
                ? $"Stopped refilling {playerName}."
                : $"{playerName} has no timed refill.";
        }

        var until = DateTime.Now + TimeSpan.FromMinutes(minutes);
        Timed[steamId] = until;
        _nextRun = DateTime.MinValue;

        var text = $"Refilling {playerName} until {until:HH:mm} ({minutes} minute{(minutes == 1 ? "" : "s")}).";
        if (!EssentialsCore.Config.SuitRefillEnabled)
            text += " Suit Refill is turned off on the Admin Tools tab, so nothing happens until it is turned on.";
        return text;
    }

    public static void Clear() => Timed.Clear();

    public static void Update()
    {
        var now = DateTime.Now;
        if (now < _nextRun)
            return;
        _nextRun = now + Interval;

        foreach (var expired in Timed.Where(t => t.Value <= now).Select(t => t.Key).ToList())
            Timed.Remove(expired);

        var config = EssentialsCore.Host?.Config;
        if (config == null || !config.SuitRefillEnabled || MySession.Static?.Ready != true)
            return;

        foreach (var player in MySession.Static.Players.GetOnlinePlayers().ToList())
        {
            try
            {
                if (ShouldRefill(config, player))
                    Refill(config, player);
            }
            catch (Exception e)
            {
                Log.Error(e, $"Suit refill failed for {player.DisplayName}");
            }
        }
    }

    private static bool ShouldRefill(EssentialsConfig config, MyPlayer player)
    {
        if (config.SuitRefillEveryone || Timed.ContainsKey(player.Id.SteamId))
            return true;

        return config.SuitRefillAdmins && MySession.Static.GetUserPromoteLevel(player.Id.SteamId) >= MyPromoteLevel.Admin;
    }

    private static void Refill(EssentialsConfig config, MyPlayer player)
    {
        var character = player.Character;
        var identityId = player.Identity?.IdentityId ?? 0;
        // Dead players and players on the respawn screen have nothing to refill.
        if (character == null || character.IsDead || character.MarkedForClose || identityId == 0)
            return;

        if (config.SuitRefillOxygen)
            MyVisualScriptLogicProvider.SetPlayersOxygenLevel(identityId, 1f);
        if (config.SuitRefillHydrogen)
            MyVisualScriptLogicProvider.SetPlayersHydrogenLevel(identityId, 1f);
        if (config.SuitRefillEnergy)
            MyVisualScriptLogicProvider.SetPlayersEnergyLevel(identityId, 1f);

        var stats = character.StatComp;
        if (stats == null)
            return;

        // A stat is null when the world does not use it (for example food without the survival update).
        if (config.SuitRefillHealth && stats.Health is { } health && health.Value < health.MaxValue)
            MyVisualScriptLogicProvider.SetPlayersHealth(identityId, health.MaxValue);
        if (config.SuitRefillFood && stats.Food is { } food)
            food.Value = food.MaxValue;
        if (config.SuitRefillRadiationImmunity && stats.RadiationImmunity is { } immunity)
            immunity.Value = immunity.MaxValue;
        // Kept just above zero rather than cleared, so the radiation panel does not keep disappearing and
        // reappearing while the admin works in a radiated area.
        if (config.SuitResetRadiation && stats.Radiation is { } radiation && radiation.Value > 5f)
            radiation.Value = 2f;
    }
}
