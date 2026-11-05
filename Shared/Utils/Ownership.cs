using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.World;

namespace SensEssentials.Utils;

public class Ownership
{
    public static long GetOwner(MyCubeGrid grid) => GetOwner([grid]);

    /// <summary>
    /// The identity owning the most blocks across all given grids, or 0 when no block is owned.
    /// </summary>
    public static long GetOwner(IEnumerable<MyCubeGrid> grids)
    {
        Dictionary<long, int> ownerCounts = [];

        foreach (var block in grids.SelectMany(g => g.GetFatBlocks()))
        {
            if (block.IDModule is null) continue;
            long ownerId = block.IDModule.Owner;
            if (ownerId == 0) continue;

            ownerCounts.TryGetValue(ownerId, out int count);
            ownerCounts[ownerId] = count + 1;
        }

        if (ownerCounts.Count == 0) return 0L;

        long bestOwner = 0;
        int bestCount = 0;
        foreach (var kvp in ownerCounts)
        {
            if (kvp.Value > bestCount)
            {
                bestCount = kvp.Value;
                bestOwner = kvp.Key;
            }
        }
        return bestOwner;
    }

    public static HashSet<long> GetAllOwnerIds(MyCubeGrid grid) => GetAllOwnerIds([grid]);

    public static HashSet<long> GetAllOwnerIds(IEnumerable<MyCubeGrid> grids)
    {
        HashSet<long> owners = [];

        foreach (var block in grids.SelectMany(g => g.GetFatBlocks()))
        {
            if (block.IDModule is null) continue;
            long ownerId = block.IDModule.Owner;
            if (ownerId != 0)
                owners.Add(ownerId);
        }

        return owners;
    }

    /// <summary>
    /// Owner type of the majority owner. An owner whose identity no longer exists counts as Nobody.
    /// </summary>
    public static OwnerType GetOwnerType(IEnumerable<MyCubeGrid> grids)
    {
        var ownerId = GetOwner(grids);
        if (ownerId == 0L || !MySession.Static.Players.HasIdentity(ownerId))
            return OwnerType.Nobody;

        return IsNpcIdentity(ownerId) ? OwnerType.Npc : OwnerType.Player;
    }

    /// <summary>
    /// The pirate identity, or a member of its faction, however many members that has.
    /// </summary>
    public static bool IsPirate(long identityId)
    {
        var pirates = MyPirateAntennas.GetPiratesId();
        return identityId == pirates
            || MySession.Static.Factions.GetPlayerFaction(pirates)?.Members.ContainsKey(identityId) == true;
    }

    /// <summary>
    /// An existing identity without a steam id. Identities that no longer exist are not NPCs.
    /// </summary>
    public static bool IsNpcIdentity(long identityId)
    {
        return MySession.Static.Players.HasIdentity(identityId)
               && MySession.Static.Players.TryGetSteamId(identityId) == 0;
    }

    public enum OwnerType
    {
        Nobody,
        Npc,
        Player
    }
}
