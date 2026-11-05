using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRage.ObjectBuilders;

namespace SensEssentials;

public static class Utilities
{
    private const string TypePrefix = "MyObjectBuilder_";

    /// <summary>
    /// Strips the "MyObjectBuilder_" prefix so "Reactor" and "MyObjectBuilder_Reactor" compare equal.
    /// </summary>
    public static string NormalizeTypeName(string typeName)
    {
        typeName = typeName.Trim();
        return typeName.StartsWith(TypePrefix, StringComparison.OrdinalIgnoreCase)
            ? typeName.Substring(TypePrefix.Length)
            : typeName;
    }

    public static bool IsBlockType(MyObjectBuilderType typeId, string typeName)
    {
        return string.Equals(NormalizeTypeName(typeId.ToString()), NormalizeTypeName(typeName), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Any block that produces electricity, so modded and newer power blocks are included.
    /// </summary>
    public static bool ProducesPower(MyFunctionalBlock block)
    {
        var source = block.Components.Get<MyResourceSourceComponent>();
        return source != null && source.ResourceTypes.Contains(MyResourceDistributorComponent.ElectricityId);
    }

    /// <summary>
    /// Removes pilots from a grid before it is deleted, so their characters are not deleted with it.
    /// </summary>
    public static void EjectPilots(MyCubeGrid grid)
    {
        foreach (var cockpit in grid.GetFatBlocks<MyCockpit>().ToList())
        {
            if (cockpit.Pilot != null)
                cockpit.RemovePilot();
        }
    }

    public static bool TryGetEntityByNameOrId(string nameOrId, out IMyEntity? entity)
    {
        if (long.TryParse(nameOrId, out long id))
            return MyAPIGateway.Entities.TryGetEntityById(id, out entity);

        foreach (var ent in MyEntities.GetEntities())
        {
            if (ent.DisplayName == nameOrId)
            {
                entity = ent;
                return true;
            }
        }

        entity = null;
        return false;
    }

    public static IMyIdentity? GetIdentityByNameOrIds(string playerNameOrIds) 
    {
        foreach (var identity in MySession.Static.Players.GetAllIdentities()) 
        {
            if (identity.DisplayName == playerNameOrIds)
                return identity;

            if (long.TryParse(playerNameOrIds, out long identityId)) 
                if (identity.IdentityId == identityId)
                    return identity;

            if (ulong.TryParse(playerNameOrIds, out ulong steamId) && steamId != 0)
            {
                ulong id = GetSteamId(identity.IdentityId);
                if (id == steamId)
                    return identity;
            }
        }

        return null;
    }

    public static IMyPlayer? GetPlayerByNameOrId(string nameOrPlayerId)
    {
        if (!long.TryParse(nameOrPlayerId, out long id))
        {
            foreach (var identity in MySession.Static.Players.GetAllIdentities())
            {
                if (identity.DisplayName == nameOrPlayerId)
                {
                    id = identity.IdentityId;
                }
            }
        }

        if (MySession.Static.Players.TryGetPlayerId(id, out MyPlayer.PlayerId playerId))
        {
            if (MySession.Static.Players.TryGetPlayerById(playerId, out MyPlayer player))
            {
                return player;
            }
        }

        return null;
    }

    public static ulong GetSteamId(long identityId) 
    {
        return MySession.Static.Players.TryGetSteamId(identityId);
    }

    public static int GetOnlinePlayerCount()
    {
        var result = 0;

        result =  MySession.Static.Players.GetOnlinePlayers()
            .Count(x => x.IsRealPlayer && !string.IsNullOrEmpty(x.DisplayName));

        return result;
    }

    public static List<MyPlayer> GetOnlinePlayers()
    {
        var result = new List<MyPlayer>(MySession.Static.Players.GetOnlinePlayers()
            .Where(x => x.IsRealPlayer && !string.IsNullOrEmpty(x.DisplayName)));
        return result;
    }
}
