using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRageMath;

namespace SensEssentials;

public class GridFinder
{
    /// <summary>
    /// Returns every grid connected to the given grid, including the grid itself.
    /// Grids without connections are returned on their own.
    /// </summary>
    public static List<MyCubeGrid> GetGroup(MyCubeGrid grid, GridLinkTypeEnum linkType = GridLinkTypeEnum.Mechanical)
    {
        var nodes = new List<MyCubeGrid>();
        MyCubeGridGroups.Static.GetGroups(linkType).GetGroupNodes(grid, nodes);
        return [.. nodes.Where(g => g.Physics != null && !g.MarkedForClose)];
    }

    /// <summary>
    /// Main grid of a group: the grid with the most blocks.
    /// </summary>
    public static MyCubeGrid GetMainGrid(IReadOnlyList<MyCubeGrid> group)
    {
        return group.OrderByDescending(g => g.BlocksCount).First();
    }

    /// <summary>
    /// Mechanical groups containing a grid with the given name or entity id.
    /// </summary>
    public static List<List<MyCubeGrid>> FindGroupsByName(string gridName)
    {
        return [.. GetAllGrids().Values.Where(group => group.Any(g => g.DisplayName == gridName || g.EntityId.ToString() == gridName))];
    }

    /// <summary>
    /// The mechanical group the character is looking at (crosshair, or the free look direction), up to 5 km away.
    /// Returns null when nothing is in sight.
    /// </summary>
    public static List<MyCubeGrid>? FindLookAtGroup(IMyCharacter character)
    {
        const float range = 5000;
        MatrixD head = character.GetHeadMatrix(true, true, false);
        var start = head.Translation + head.Forward * 0.5;
        var end = head.Translation + head.Forward * (range + 0.5);
        var ray = new RayD(start, head.Forward);

        List<MyCubeGrid>? closest = null;
        double closestDistance = double.MaxValue;
        foreach (var group in GetAllGrids().Values)
        {
            foreach (IMyCubeGrid grid in group)
            {
                // Cheap box test first, then the block raycast.
                if (!ray.Intersects(grid.WorldAABB).HasValue)
                    continue;

                var hit = grid.RayCastBlocks(start, end);
                if (!hit.HasValue)
                    continue;

                double distance = (start - grid.GridIntegerToWorld(hit.Value)).Length();
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = group;
                }
            }
        }

        return closest;
    }

    /// <summary>
    /// All grids with physics, grouped by mechanical connection and keyed by the main grid's entity id.
    /// </summary>
    public static Dictionary<long, List<MyCubeGrid>> GetAllGrids()
    {
        HashSet<long> processedGridIds = [];
        Dictionary<long, List<MyCubeGrid>> grids = [];

        foreach (MyCubeGrid grid in MyEntities.GetEntities().OfType<MyCubeGrid>().ToList())
        {
            if (grid.Physics == null || grid.MarkedForClose) continue;
            if (processedGridIds.Contains(grid.EntityId)) continue;

            var group = GetGroup(grid);
            if (group.Count == 0)
                continue;

            foreach (var member in group)
                processedGridIds.Add(member.EntityId);

            grids[GetMainGrid(group).EntityId] = group;
        }

        return grids;
    }
}
