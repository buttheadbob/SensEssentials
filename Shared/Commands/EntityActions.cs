using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Game;
using VRage.Network;
using Sandbox.Game;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using VRage.Game.ModAPI.Interfaces;

namespace SensEssentials.Commands;

/// <summary>
/// The !entities commands.
/// </summary>
public sealed class EntityActions(ICommandContext context) : CommandActions(context)
{
    // Replication internals used by refresh. Looked up once; refresh reports when a game update renamed them.
    private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Type? ClientType = typeof(MyReplicationServer).Assembly.GetType("VRage.Network.MyClient");
    private static readonly FieldInfo? ClientStatesField = typeof(MyReplicationServer).GetField("m_clientStates", AnyInstance);
    private static readonly FieldInfo? ReplicablesField = ClientType?.GetField("Replicables", AnyInstance);
    private static readonly MethodInfo? RemoveForClientMethod = ClientType == null ? null
        : typeof(MyReplicationServer).GetMethod("RemoveForClient", AnyInstance, null, [typeof(IMyReplicable), ClientType, typeof(bool)], null);
    private static readonly MethodInfo? ForceReplicableMethod =
        typeof(MyReplicationServer).GetMethod("ForceReplicable", AnyInstance, null, [typeof(IMyReplicable), typeof(Endpoint)], null);

    private static Dictionary<ulong, DateTime> _commandtimeout = [];

    public void Refresh()
    {
        if (Context.Player == null)
        {
            Context.Respond("This command can only be used in-game.");
            return;
        }

        var steamid = Context.Player.SteamUserId;
        if (_commandtimeout.TryGetValue(steamid, out DateTime lastcommand))
        {
            TimeSpan remaining = TimeSpan.FromMinutes(1) - (DateTime.UtcNow - lastcommand);
            if (remaining > TimeSpan.Zero)
            {
                Context.Respond($"Cooldown active. You can use this command again in {Math.Ceiling(remaining.TotalSeconds):N0} seconds");
                return;
            }
                
            _commandtimeout[steamid] = DateTime.UtcNow;
        }
        else
        {
            _commandtimeout.Add(steamid, DateTime.UtcNow);
        }

        if (ClientStatesField == null || ReplicablesField == null || RemoveForClientMethod == null || ForceReplicableMethod == null)
        {
            Context.Respond("Refresh is not available with this game version.");
            Log.Warn("!entities refresh: the game's replication members were not found");
            return;
        }

        var playerEndpoint = new Endpoint(Context.Player.SteamUserId, 0);
        var replicationServer = (MyReplicationServer)MyMultiplayer.ReplicationLayer;
        if (ClientStatesField.GetValue(replicationServer) is not IDictionary clientDataDict || !clientDataDict.Contains(playerEndpoint)
            || ReplicablesField.GetValue(clientDataDict[playerEndpoint]) is not IEnumerable clientReplicables)
        {
            Context.Respond("Could not find your connection on the server.");
            return;
        }

        var clientData = clientDataDict[playerEndpoint];

        // Copied first, since removing a replicable changes the client's collection.
        var replicableList = new List<IMyReplicable>();
        foreach (var pair in clientReplicables)
        {
            if (pair.GetType().GetProperty("Key")?.GetValue(pair) is IMyReplicable replicable)
                replicableList.Add(replicable);
        }

        foreach (var replicable in replicableList)
        {
            RemoveForClientMethod.Invoke(replicationServer, [replicable, clientData, true]);
            ForceReplicableMethod.Invoke(replicationServer, [replicable, playerEndpoint]);
        }

        Context.Respond($"Forced replication of {replicableList.Count} entities.");
    }

    public void Stop(string entityName)
    {
        if (!Utilities.TryGetEntityByNameOrId(entityName, out IMyEntity? entity))
        {
            Context.Respond($"Entity '{entityName}' not found.");
            return;
        }

        var physics = entity!.Physics;
        if (physics == null)
        {
            Context.Respond($"Entity '{entity.DisplayName}' has no physics to stop.");
            return;
        }

        physics.ClearSpeed();
        Context.Respond($"Entity '{entity.DisplayName}' stopped");
    }

    public void Delete(string entityName)
    {
        var name = entityName;
        if (string.IsNullOrEmpty(name))
            return;

        if (!Utilities.TryGetEntityByNameOrId(name, out IMyEntity? entity))
        {
            Context.Respond($"Entity '{name}' not found.");
            return;
        }

        if (entity is IMyCharacter)
        {
            Context.Respond("You cannot delete characters.");
            return;
        }

        if (entity is MyCubeBlock or MyVoxelBase)
        {
            Context.Respond("Use !blocks remove for blocks and !voxels for planets and asteroids.");
            return;
        }

        if (entity is MyCubeGrid grid)
            Utilities.EjectPilots(grid);

        entity!.Close();
        Context.Respond($"Entity '{entity.DisplayName}' deleted");
    }

    public void Kill(string playerName)
    {
        var player = Utilities.GetPlayerByNameOrId(playerName);
        if (player != null)
        {
            if (!CanActOn(player.SteamUserId))
                return;

            if (player.Character == null)
            {
                Context.Respond($"{player.DisplayName} has no character right now.");
                return;
            }

            MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId, 0);

            EssentialsCore.Host.SendChat(null, $"{player.DisplayName} was killed by an admin [{Context.Player?.DisplayName ?? "Console Command"}]");

            return;
        }

        // An offline player's character. Seated characters are not in the entity list, so ask the identity first.
        var identity = Utilities.GetIdentityByNameOrIds(playerName);
        IMyCharacter? character = identity == null ? null : MySession.Static.Players.TryGetIdentity(identity.IdentityId)?.Character;
        if (character == null && Utilities.TryGetEntityByNameOrId(playerName, out IMyEntity? entity))
            character = entity as IMyCharacter;

        if (character == null)
        {
            Context.Respond($"No character found for '{playerName}'.");
            return;
        }

        if (identity != null && MySession.Static.Players.TryGetSteamId(identity.IdentityId) is var steamId and not 0 && !CanActOn(steamId))
            return;

        ((IMyDestroyableObject)character).DoDamage(0, MyDamageType.Suicide, true);
        EssentialsCore.Host.SendChat(null, $"{character.DisplayName} was killed by an admin");
    }

    public void Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            return;

        var sb = new StringBuilder("Found entities:\n");
        foreach (var entity in MyEntities.GetEntities())
        {
            if (entity is IMyVoxelBase voxel && (voxel.StorageName?.Contains(name, StringComparison.CurrentCultureIgnoreCase) ?? false))
                sb.AppendLine($"{voxel.StorageName} ({entity.EntityId})");
            else if (entity?.DisplayName?.Contains(name, StringComparison.CurrentCultureIgnoreCase) ?? false)
                // DisplayName can be null.
                sb.AppendLine($"{entity.DisplayName} ({entity.EntityId})");
        }

        Context.Respond(sb.ToString());
    }

    public void PowerOff(string name, bool includeConnectorDocked)
    {
        SetPower(name, false, includeConnectorDocked);
    }

    public void PowerOn(string name, bool includeConnectorDocked)
    {
        SetPower(name, true, includeConnectorDocked);
    }

    private void SetPower(string name, bool enabled, bool includeConnectorDocked)
    {
        if (string.IsNullOrEmpty(name))
            return;

        if (!Utilities.TryGetEntityByNameOrId(name, out IMyEntity? entity))
        {
            Context.Respond($"Entity '{name}' not found.");
            return;
        }

        if (entity is not MyCubeGrid grid)
        {
            Context.Respond($"'{entity!.DisplayName}' is not a grid.");
            return;
        }

        var grids = GridFinder.GetGroup(grid, includeConnectorDocked ? GridLinkTypeEnum.Physical : GridLinkTypeEnum.Mechanical);
        int changed = 0;
        foreach (var block in grids.SelectMany(g => g.GetFatBlocks<MyFunctionalBlock>()))
        {
            if (!Utilities.ProducesPower(block) || block.Enabled == enabled)
                continue;

            block.Enabled = enabled;
            changed++;
        }

        Context.Respond($"Turned {(enabled ? "on" : "off")} {changed} power block(s) on {grids.Count} grid(s) of '{grid.DisplayName}'.");
    }

    public void Eject(string playerName) 
    {

        if (playerName.ToLower() == "all") 
            EjectAllPlayers();
        else 
            EjectSinglePlayer(playerName);
    }

    private void EjectAllPlayers() 
    {

        int ejectedPlayersCount = 0;

        foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>().ToList()) 
        foreach (var controller in grid.GetFatBlocks<MyShipController>()) 
            if (controller.Pilot != null) 
            {
                controller.Use();
                ejectedPlayersCount++;
            }

        Context.Respond($"Ejected '{ejectedPlayersCount}' players from their seats.");
    }

    private void EjectSinglePlayer(string playerName) 
    {

        /* We check first if the player is among the online players before looping over all grids for nothing. */
        var player = Utilities.GetPlayerByNameOrId(playerName);
        if (player != null) 
        {
            /* If he is online we check if he is currently seated. If he is eject him. */
            if (player.Controller?.ControlledEntity is MyCockpit controller) 
            {
                controller.Use();
                Context.Respond($"Player '{playerName}' ejected.");
            } 
            else 
                Context.Respond("Player not seated.");

            return;
        }

        foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>().ToList()) 
        {
            foreach (var controller in grid.GetFatBlocks<MyShipController>()) 
            {
                var pilot = controller.Pilot;

                if (pilot != null && pilot.DisplayName == playerName) 
                {
                    controller.Use();

                    Context.Respond($"Player '{playerName}' ejected.");

                    /* We found our player. so no need to continue looking */
                    return;
                }
            }
        }

        Context.Respond("Offline player not found or seated.");
    }
}