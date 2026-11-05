using SensEssentials.Utils;
using Sandbox.Engine.Voxels;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using VRage.Game.Entity;
using VRage.Voxels;
using VRageMath;
using static Sandbox.Game.Entities.MyVoxelBase;

namespace SensEssentials.Commands;

/// <summary>
/// The !voxels commands.
/// </summary>
public sealed class VoxelActions(ICommandContext context) : CommandActions(context)
{
    public void ResetAll(bool deleteStorage) => ResetMaps(MyEntities.GetEntities().OfType<MyVoxelBase>(), deleteStorage);

    public void CleanupAsteroids(bool deleteStorage) =>
        ResetMaps(MyEntities.GetEntities().OfType<MyVoxelMap>(), deleteStorage, map => map.PositionComp.WorldVolume);

    public void CleanupAsteroidsDistant(double distance, bool deleteStorage)
    {
        if (!(distance >= 0))
        {
            Context.Respond("Invalid distance!");
            return;
        }

        ResetMaps(MyEntities.GetEntities().OfType<MyVoxelMap>(), deleteStorage, map => new BoundingSphereD(map.PositionComp.GetPosition(), distance));
    }

    public void ResetPlanets() => ResetMaps(MyEntities.GetEntities().OfType<MyPlanet>(), false);

    /// <summary>
    /// Resets the maps, or deletes the asteroids among them. With keepClear, a map with a grid or character in that area is left alone.
    /// </summary>
    private void ResetMaps(IEnumerable<MyVoxelBase> maps, bool deleteStorage, Func<MyVoxelBase, BoundingSphereD>? keepClear = null)
    {
        var resetIds = new List<long>();
        int count = 0;

        foreach (var map in maps.ToList())
        {
            try
            {
                if (map.StorageName == null || map.Storage.DataProvider == null)
                    continue;

                if (keepClear != null)
                {
                    var area = keepClear(map);
                    var nearEntities = new List<MyEntity>();
                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref area, nearEntities);
                    if (nearEntities.Any(e => e is MyCubeGrid or MyCharacter))
                        continue;
                }

                if (deleteStorage && map is MyVoxelMap)
                {
                    DeleteMap(map);
                }
                else
                {
                    map.Storage.Reset(MyStorageDataTypeFlags.All);
                    resetIds.Add(map.EntityId);
                }

                count++;
            }
            catch (Exception e)
            {
                Log.Error(e, $"Could not reset voxel map {map.StorageName}");
            }
        }

        EssentialsCore.Host.NotifyVoxelsReset([.. resetIds]);
        Context.Respond(resetIds.Count > 0 ? WithReloadNote($"Reset {count} voxel maps.") : $"Reset {count} voxel maps.");
    }

    /// <summary>
    /// The game's close request also removes an untouched asteroid from clients, which Close alone does not. The
    /// asteroid generator hooks the request to stop that asteroid ever coming back, so that hook is cleared first.
    /// </summary>
    private static void DeleteMap(MyVoxelBase map)
    {
        map.OnEntityCloseRequest = null;
        MyEntities.SendCloseRequest(map);
        if (!map.MarkedForClose)
            map.Close();
    }

    private static string WithReloadNote(string text) =>
        EssentialsCore.Host.CanReloadVoxels ? text : text + " Players see it after they reconnect.";

    public void ResetPlanet(string planetName)
    {
        var maps = new List<MyPlanet>(2);
        foreach (MyPlanet map in MyEntities.GetEntities().OfType<MyPlanet>())
        {
            if (map.StorageName?.Contains(planetName, StringComparison.CurrentCultureIgnoreCase) == true)
                maps.Add(map);
        }

        switch (maps.Count)
        {
            case 0:
                Context.Respond($"Couldn't find planet with name {planetName}");
                return;
            case 1:
                var map = maps[0];
                map.Storage.Reset(MyStorageDataTypeFlags.All);
                EssentialsCore.Host.NotifyVoxelsReset([map.EntityId]);
                Context.Respond(WithReloadNote($"Reset planet {map.Name}"));
                return;
            default:
                Context.Respond($"Found {maps.Count} planets matching '{planetName}'. Please select from list:");
                Context.Respond(string.Join("\r\n", maps.Select(m => m.StorageName)));
                return;
        }
    }

    public void ResetVoxelArea(float radius)
    {
        if (Context.Player == null)
        {
            Context.Respond("This command must be used in-game. From the console use: !voxels reset gps <x y z> <radius>");
            return;
        }

        var entity = Context.Player.Controller?.ControlledEntity?.Entity;
        if (entity == null || Context.Player.Character == null || Context.Player.Character.IsDead)
        {
            Context.Respond("You must be alive in the world to use this command.");
            return;
        }

        if (radius <= 0)
        {
            Context.Respond("Invalid radius!");
            return;
        }

        ReportArea(ResetVoxelInArea(entity.PositionComp.GetPosition(), radius));
    }

    public void ResetVoxelGps()
    {
        // Args are parsed manually so coordinates can be given as "x y z", "x:y:z" or a pasted GPS string
        const string usage = "Usage: !voxels reset gps <x y z | x:y:z | GPS:Name:x:y:z:> <radius>";
        var args = Context.Args;

        if (args.Count < 2)
        {
            Context.Respond(usage);
            return;
        }

        if (!double.TryParse(args[args.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var radius) || !(radius > 0) || double.IsInfinity(radius))
        {
            Context.Respond("Invalid radius!");
            return;
        }

        if (!TryParsePosition(string.Join(" ", args.Take(args.Count - 1)), out var resetTarget))
        {
            Context.Respond($"Invalid coordinates! {usage}");
            return;
        }

        ReportArea(ResetVoxelInArea(resetTarget, (float)radius));
    }

    private void ReportArea(int? maps) => Context.Respond(maps switch
    {
        null => "Couldn't reset voxels! Check log for more information!",
        0 => "No voxels found in that area.",
        _ => "Voxel reset complete!"
    });

    private static bool TryParsePosition(string text, out Vector3D position)
    {
        position = Vector3D.Zero;
        text = text.Trim();

        string[] parts;
        if (text.StartsWith("GPS:", StringComparison.OrdinalIgnoreCase))
        {
            // GPS:Name:X:Y:Z:#Color:
            var gpsParts = text.Split(':');
            if (gpsParts.Length < 5)
                return false;
            parts = [gpsParts[2], gpsParts[3], gpsParts[4]];
        }
        else
        {
            parts = text.Split([':', ' ', ','], StringSplitOptions.RemoveEmptyEntries);
        }

        if (parts.Length != 3)
            return false;

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            return false;

        position = new Vector3D(x, y, z);
        return true;
    }

    /// <summary>
    /// Reverts voxels in a sphere. Returns how many voxel maps were reset, or null when it failed.
    /// </summary>
    private static int? ResetVoxelInArea(Vector3D center, float radius)
    {
        try
        {
            BoundingSphereD sphere = new(center, radius);
            // Use our own list: MyEntities.GetEntitiesInSphere returns SE's shared list, which callers must clear.
            var found = new List<MyEntity>();
            MyGamePruningStructure.GetAllEntitiesInSphere(ref sphere, found);
            // A planet shows up as many physics chunks that share one root.
            var maps = found.OfType<MyVoxelBase>().Select(v => v.RootVoxel).Where(v => !v.MarkedForClose).Distinct().ToList();

            // The public request refuses a revert around characters and grids, so the event is raised directly.
            var revert = typeof(MyVoxelBase).GetMethod("PerformVoxelOperationSphere_Implementation", BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var voxelMap in maps)
            {
                MyShapeSphere shape = new()
                {
                    Center = center,
                    Radius = radius
                };

                MyVoxelGenerator.RevertShape(voxelMap, shape);

                if (revert != null)
                    Events.RaiseEvent<MyVoxelBase, Vector3D, float, byte, OperationType>(voxelMap, revert, shape.Center, shape.Radius, 0, MyVoxelBase.OperationType.Revert);
            }

            return maps.Count;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Voxel reset failed!");
            return null;
        }
    }
}
