using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sandbox.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using Vector3D = VRageMath.Vector3D;

namespace SensEssentials.Commands;

/// <summary>
/// Conditions are evaluated per mechanical group (a grid plus everything attached by rotors, hinges and pistons).
/// Methods taking IReadOnlyList&lt;MyCubeGrid&gt; decide for the whole group. Methods taking a single MyCubeGrid
/// match when any grid in the group matches; their inverted form matches when no grid in the group matches.
/// </summary>
[ConditionModule]
public static class ConditionsImplementations
{
    [Condition("name", helpText: "Finds grids whose main grid name matches. Accepts regex format.", Prepare = nameof(PrepareName))]
    public static bool NameMatches(IReadOnlyList<MyCubeGrid> group, Regex pattern)
    {
        var name = GridFinder.GetMainGrid(group).DisplayName;
        return !string.IsNullOrEmpty(name) && pattern.IsMatch(name);
    }

    // A pattern runs on the game thread, so a bad one must not be able to hang the server.
    private static object PrepareName(string value) => new Regex(value, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    [Condition("blockslessthan", helpText: "Finds grids with less than the given number of blocks (whole group).")]
    public static bool BlocksLessThan(IReadOnlyList<MyCubeGrid> group, int count)
    {
        return group.Sum(g => g.BlocksCount) < count;
    }

    [Condition("pcugreaterthan", helpText: "Finds grids with more than the given number of PCU (whole group).")]
    public static bool PCUGreaterThan(IReadOnlyList<MyCubeGrid> group, int pcu)
    {
        return group.Sum(g => g.BlocksPCU) > pcu;
    }

    [Condition("pculessthan", helpText: "Finds grids with less than the given number of PCU (whole group).")]
    public static bool PCULessThan(IReadOnlyList<MyCubeGrid> group, int pcu)
    {
        return group.Sum(g => g.BlocksPCU) < pcu;
    }

    [Condition("hasgridtype", helpText: "Finds grids whose main grid is the specified type (large | small | ship | static).", Prepare = nameof(PrepareGridType))]
    public static bool HasGridType(IReadOnlyList<MyCubeGrid> group, string gridType)
    {
        if (string.IsNullOrEmpty(gridType))
            return false;

        var grid = GridFinder.GetMainGrid(group);

        if (string.Compare(gridType, "static", StringComparison.InvariantCultureIgnoreCase) == 0)
            return grid.IsStatic;

        if (string.Compare(gridType, "ship", StringComparison.InvariantCultureIgnoreCase) == 0)
            return !grid.IsStatic;

        if (string.Compare(gridType, "large", StringComparison.InvariantCultureIgnoreCase) == 0)
            return grid.GridSizeEnum == VRage.Game.MyCubeSize.Large;

        if (string.Compare(gridType, "small", StringComparison.InvariantCultureIgnoreCase) == 0)
            return grid.GridSizeEnum == VRage.Game.MyCubeSize.Small;

        // In all other cases, just return false.
        return false;
    }

    private static object PrepareGridType(string value) => value.Trim().ToLowerInvariant() is var type and ("large" or "small" or "ship" or "static")
        ? type
        : throw new ArgumentException($"Unknown grid type '{value}'. Use large, small, ship or static.");

    [Condition("hasownertype", helpText: "Finds grids whose MAJORITY owner is the given type (npc | nobody | player | pirates). nobody also covers owners whose identity was deleted.", Prepare = nameof(PrepareOwnerType))]
    public static bool HasOwnerType(IReadOnlyList<MyCubeGrid> group, string ownerType)
    {
        if (ownerType == "nobody")
            return Utils.Ownership.GetOwnerType(group) == Utils.Ownership.OwnerType.Nobody;

        var owner = Utils.Ownership.GetOwner(group);
        return owner != 0 && IsOwnerType(owner, ownerType);
    }

    private static object PrepareOwnerType(string value) =>
        OwnerKeyword(value) ?? throw new ArgumentException($"Unknown owner type '{value}'. Use npc, nobody, player or pirates.");

    private static string? OwnerKeyword(string value) => value.Trim().ToLowerInvariant() switch
    {
        "npc" or "npcs" => "npc",
        "player" or "players" => "player",
        "nobody" => "nobody",
        "pirates" => "pirates",
        _ => null
    };

    private static bool IsOwnerType(long owner, string type) => type switch
    {
        "npc" => Utils.Ownership.IsNpcIdentity(owner),
        "player" => MySession.Static.Players.HasIdentity(owner) && !Utils.Ownership.IsNpcIdentity(owner),
        "pirates" => Utils.Ownership.IsPirate(owner),
        _ => false
    };

    [Condition("blocksgreaterthan", helpText: "Finds grids with more than the given number of blocks (whole group).")]
    public static bool BlocksGreaterThan(IReadOnlyList<MyCubeGrid> group, int count)
    {
        return group.Sum(g => g.BlocksCount) > count;
    }

    [Condition("haspower", "nopower", "Finds grids with, or without power.")]
    public static bool HasPower(MyCubeGrid grid)
    {
        foreach (var b in grid.GetFatBlocks())
        {
            var c = b.Components?.Get<MyResourceSourceComponent>();
            if (c == null)
                continue;

            // Not every source produces electricity, and asking one about a resource it lacks throws.
            if (!c.ResourceTypes.Contains(MyResourceDistributorComponent.ElectricityId))
                continue;

            if (c.HasCapacityRemainingByType(MyResourceDistributorComponent.ElectricityId) && c.ProductionEnabledByType(MyResourceDistributorComponent.ElectricityId))
                return true;
        }

        return false;
    }

    [Condition("insideplanet", helpText: "Finds grids that are trapped inside planets.")]
    public static bool InsidePlanet(MyCubeGrid grid)
    {
        var s = grid.PositionComp.WorldVolume;
        var voxels = new List<MyVoxelBase>();
        MyGamePruningStructure.GetAllVoxelMapsInSphere(ref s, voxels);

        if (!voxels.Any())
            return false;

        foreach (var v in voxels)
        {
            if (v is not MyPlanet planet)
                continue;

            var dist2center = Vector3D.DistanceSquared(s.Center, planet.PositionComp.WorldVolume.Center);
            if (dist2center <= (planet.MaximumRadius * planet.MaximumRadius) / 2)
                return true;
        }

        return false;
    }

    [Condition("playerdistancelessthan", "playerdistancegreaterthan", "Finds grids that are closer (or further) than the given distance from players who are alive in the world.")]
    public static bool PlayerDistanceLessThan(MyCubeGrid grid, double dist)
    {
        dist *= dist;
        foreach (var player in MySession.Static.Players.GetOnlinePlayers())
        {
            if (!TryGetLivePosition(player, out var position))
                continue;

            if (Vector3D.DistanceSquared(position, grid.PositionComp.GetPosition()) < dist)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The grids with power, worked out once for a scan instead of for every group.
    /// </summary>
    public sealed class PoweredGrids(double distanceSquared, List<(long Id, Vector3D Position)> grids)
    {
        public double DistanceSquared { get; } = distanceSquared;
        public List<(long Id, Vector3D Position)> Grids { get; } = grids;
    }

    [Condition("poweredgriddistancegreaterthan", helpText: "Finds grids that are farther than the given distance from other grids that are powered.", Prepare = nameof(PreparePoweredGrids))]
    public static bool PoweredGridDistanceGreaterThan(IReadOnlyList<MyCubeGrid> group, PoweredGrids powered)
    {
        // True when no powered grid outside this group is within the given distance of any grid in this group.
        var own = new HashSet<long>(group.Select(g => g.EntityId));

        foreach (var (id, position) in powered.Grids)
        {
            if (!own.Contains(id) && group.Any(g => Vector3D.DistanceSquared(position, g.PositionComp.GetPosition()) < powered.DistanceSquared))
                return false;
        }

        return true;
    }

    private static object PreparePoweredGrids(string value)
    {
        if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var distance)
            || double.IsNaN(distance) || double.IsInfinity(distance) || distance < 0)
            throw new ArgumentException($"Condition 'poweredgriddistancegreaterthan' needs a positive number, not '{value}'.");

        return new PoweredGrids(distance * distance,
            [.. MyEntities.GetEntities().OfType<MyCubeGrid>().Where(IsPowered).Select(g => (g.EntityId, g.PositionComp.GetPosition()))]);
    }

    // Power state lives on the grid's resource distributor, which is null for grids with no power systems.
    private static bool IsPowered(MyCubeGrid grid)
    {
        var distributor = grid.GridSystems?.ResourceDistributor;
        if (distributor == null)
            return false;

        var state = distributor.ResourceState;
        return state is not VRage.MyResourceStateEnum.NoPower
            and not VRage.MyResourceStateEnum.Disconnected;
    }

    [Condition("centerdistancelessthan", "centerdistancegreaterthan", "Finds grids that are closer (or further) than the given distance from center.")]
    public static bool CenterDistanceLessThan(MyCubeGrid grid, double dist)
    {
        dist *= dist;

        return Vector3D.DistanceSquared(Vector3D.Zero, grid.PositionComp.GetPosition()) < dist;
    }

    /// <summary>
    /// What ownedby looks for: an owner type, or one identity.
    /// </summary>
    public sealed class OwnerFilter(string? keyword, long identityId)
    {
        public string? Keyword { get; } = keyword;
        public long IdentityId { get; } = identityId;
    }

    [Condition("ownedby", helpText: "Finds grids where ANY block is owned by the given player name, IdentityId, or type (npc | player | pirates). nobody is the exception: it means no block has an owner at all.", Prepare = nameof(PrepareOwner))]
    public static bool OwnedBy(IReadOnlyList<MyCubeGrid> group, OwnerFilter owner)
    {
        var allOwners = Utils.Ownership.GetAllOwnerIds(group);

        return owner.Keyword switch
        {
            null => allOwners.Contains(owner.IdentityId),
            "nobody" => allOwners.Count == 0,
            _ => allOwners.Any(id => IsOwnerType(id, owner.Keyword))
        };
    }

    private static object PrepareOwner(string value)
    {
        if (OwnerKeyword(value) is { } keyword)
            return new OwnerFilter(keyword, 0);

        if (Utilities.GetIdentityByNameOrIds(value) is { } identity)
            return new OwnerFilter(null, identity.IdentityId);

        // The owner of a block can be an identity that no longer exists.
        return long.TryParse(value, out var identityId)
            ? new OwnerFilter(null, identityId)
            : throw new ArgumentException($"No player or identity called '{value}'.");
    }

    [Condition("hastype", "notype", "Finds grids containing blocks of the given types, separated by commas. The name must be a block in this world.", Prepare = nameof(PrepareTypes))]
    [Condition("hastype-fast", "notype-fast", "The same as hastype.", Prepare = nameof(PrepareTypes))]
    public static bool BlockType(MyCubeGrid grid, HashSet<MyObjectBuilderType> types)
    {
        foreach (var block in grid.GetFatBlocks())
        {
            if (types.Contains(block.BlockDefinition.Id.TypeId))
                return true;
        }

        return false;
    }

    private static object PrepareTypes(string value) => BlockLookup.ParseTypes(value);

    [Condition("hassubtype", "nosubtype", "Finds grids containing blocks of the given subtypes, separated by commas. The name must be a block in this world.", Prepare = nameof(PrepareSubtypes))]
    [Condition("hassubtype-fast", "nosubtype-fast", "The same as hassubtype.", Prepare = nameof(PrepareSubtypes))]
    public static bool BlockSubType(MyCubeGrid grid, HashSet<MyStringHash> subtypes)
    {
        foreach (var block in grid.GetFatBlocks())
        {
            if (subtypes.Contains(block.BlockDefinition.Id.SubtypeId))
                return true;
        }

        return false;
    }

    private static object PrepareSubtypes(string value) => BlockLookup.ParseSubtypes(value);

    [Condition("haspilot", helpText: "Finds grids with pilots")]
    public static bool Piloted(MyCubeGrid grid)
    {
        return grid.GetFatBlocks().OfType<MyShipController>().Any(b => b.Pilot != null);
    }

    /// <summary>
    /// Position of a player who is alive in the world. Players who are dead or on the respawn screen have
    /// no position (SE reports them at 0,0,0).
    /// </summary>
    public static bool TryGetLivePosition(MyPlayer player, out Vector3D position)
    {
        position = default;

        var character = player.Character;
        var entity = player.Controller?.ControlledEntity?.Entity;
        if (character == null || character.IsDead || character.MarkedForClose || entity == null)
            return false;

        position = entity.PositionComp.GetPosition();
        return true;
    }
}
