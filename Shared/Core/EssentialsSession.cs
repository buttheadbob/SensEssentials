using System;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using SensEssentials.Commands;
using SensEssentials.Discord;
using VRage.GameServices;
using VRageMath;

namespace SensEssentials;

/// <summary>
/// Session lifecycle shared by both hosts. The host calls these from its own load, unload and update events.
/// Every call catches its own errors, since an exception escaping into the loader can stop the world loading.
/// </summary>
public static class EssentialsSession
{
    private static MyMultiplayerBase? _multiplayer;

    /// <summary>
    /// The game is loading a world; called before the session exists.
    /// </summary>
    public static void GameLoading()
    {
        Run(ConditionsChecker.Init, "load cleanup conditions");
    }

    /// <summary>
    /// The world is loaded and the server is about to accept players.
    /// </summary>
    public static void Loaded()
    {
        Run(() =>
        {
            _multiplayer = MyMultiplayer.Static;
            if (_multiplayer != null)
            {
                _multiplayer.ClientJoined += OnClientJoined;
                _multiplayer.ClientLeft += OnClientLeft;
            }
        }, "hook player events");

        Run(() => MyEntities.OnEntityAdd += Backpacks.OnEntityAdded, "hook backpacks");

        if (EssentialsCore.Config.StopShipsOnStart)
            Run(StopShips, "stop ships");

        Run(AutoCommands.Instance.Start, "start auto commands");
        Run(SimSpeedEvents.Instance.Start, "start sim speed events");
        DiscordLogging.ServerOnline = true;
    }

    /// <summary>
    /// The world is about to unload.
    /// </summary>
    public static void Unloading()
    {
        Run(() =>
        {
            if (_multiplayer != null)
            {
                _multiplayer.ClientJoined -= OnClientJoined;
                _multiplayer.ClientLeft -= OnClientLeft;
                _multiplayer = null;
            }
        }, "unhook player events");

        Run(() => MyEntities.OnEntityAdd -= Backpacks.OnEntityAdded, "unhook backpacks");
        Run(AutoCommands.Instance.Stop, "stop auto commands");
        Run(SimSpeedEvents.Instance.Stop, "stop sim speed events");
        Run(VotingActions.VoteEnd, "end vote");

        SuitRefill.Clear();
        Backpacks.Clear();
        Motd.Clear();
        DiscordLogging.ServerOnline = false;
    }

    /// <summary>
    /// Every game tick, on the game thread.
    /// </summary>
    public static void Update()
    {
        Backpacks.Update();
        Run(SuitRefill.Update, "refill suits");
    }

    /// <summary>
    /// The plugin is being unloaded.
    /// </summary>
    public static void Dispose()
    {
        Run(AutoCommands.Instance.Dispose, "dispose auto commands");
        Run(SimSpeedEvents.Instance.Dispose, "dispose sim speed events");
        Run(DiscordLogging.Stop, "stop Discord logging");
    }

    private static void OnClientJoined(ulong steamId, string name) => Run(() => Motd.OnPlayerJoined(steamId), "start the MOTD");

    private static void OnClientLeft(ulong steamId, MyChatMemberStateChangeEnum change) => Motd.OnPlayerLeft(steamId);

    private static void StopShips()
    {
        foreach (var e in MyEntities.GetEntities())
            e.Physics?.SetSpeeds(Vector3.Zero, Vector3.Zero);
    }

    private static void Run(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Error(e, $"Failed to {what}");
        }
    }
}
