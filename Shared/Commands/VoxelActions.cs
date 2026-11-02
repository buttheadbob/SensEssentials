using SensEssentials.Utils;
using Sandbox.Engine.Voxels;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World.Generator;
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
    private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FieldInfo? AsteroidsModuleField = typeof(MyProceduralWorldGenerator).GetField("m_asteroidsModule", AnyInstance);
    private static readonly FieldInfo? IsClosingEntitiesField = typeof(MyProceduralAsteroidCellGenerator).GetField("m_isClosingEntities", AnyInstance);

    // Looked up each time: the generator belongs to the session, so a cached one goes stale after a world reload.
    private static MyProceduralAsteroidCellGenerator? GeneratorInstance =>
        MyProceduralWorldGenerator.Static == null ? null : AsteroidsModuleField?.GetValue(MyProceduralWorldGenerator.Static) as MyProceduralAsteroidCellGenerator;

    public void ResetAll(bool deleteStorage)
    {
        var voxelMaps = MyEntities.GetEntities().OfType<MyVoxelBase>().ToList();

        var resetIds = new List<long>();
        int count = 0;

        foreach (var map in voxelMaps)
        {
            try
            {
                if (map.StorageName == null || map.Storage.DataProvider == null)
                    continue;

                count++;

                long id = map.EntityId;

                if (deleteStorage && map is MyVoxelMap)
                {
                    using (PinDelete())
                        map.Close();
                }
                else
                {
                    map.Storage.Reset(MyStorageDataTypeFlags.All);
                    resetIds.Add(id);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Could not reset voxel map {map.StorageName}");
            }
        }

        EssentialsCore.Host.NotifyVoxelsReset(resetIds.ToArray());

        Context.Respond($"Reset {count} voxel maps.");
    }

    public void CleanupAsteroids(bool deleteStorage)
    {
        var voxelMaps = MyEntities.GetEntities().OfType<MyVoxelMap>().ToList();

        var resetIds = new List<long>();
        int count = 0;

        foreach (var map in voxelMaps)
        {
            try
            {
                if (map.StorageName == null || map.Storage.DataProvider == null)
                    continue;

                var s = map.PositionComp.WorldVolume;
                var nearEntities = new List<MyEntity>();

                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref s, nearEntities);

                if (nearEntities.Any(e => e is MyCubeGrid || e is MyCharacter))
                    continue;

                count++;

                long id = map.EntityId;

                if (deleteStorage)
                {
                    using (PinDelete())
                        map.Close();
                }
                else
                {
                    map.Storage.Reset(MyStorageDataTypeFlags.All);
                    resetIds.Add(id);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Could not reset voxel map {map.StorageName}");
            }
        }

        EssentialsCore.Host.NotifyVoxelsReset(resetIds.ToArray());

        Context.Respond($"Reset {count} voxel maps.");
    }

    public void CleanupAsteroidsDistant(double distance, bool deleteStorage)
    {

        var voxelMaps = MyEntities.GetEntities().OfType<MyVoxelMap>().ToList();

        var resetIds = new List<long>();
        int count = 0;

        foreach (var map in voxelMaps)
        {
            try
            {
                if (map.StorageName == null || map.Storage.DataProvider == null)
                    continue;

                var s = new BoundingSphereD(map.PositionComp.GetPosition(), distance);
                var nearEntities = new List<MyEntity>();

                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref s, nearEntities);

                if (nearEntities.Any(e => e is MyCubeGrid || e is MyCharacter))
                    continue;

                count++;

                long id = map.EntityId;

                if (deleteStorage)
                {
                    using (PinDelete())
                        map.Close();
                }
                else
                {
                    map.Storage.Reset(MyStorageDataTypeFlags.All);
                    resetIds.Add(id);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Could not reset voxel map {map.StorageName}");
            }
        }

        EssentialsCore.Host.NotifyVoxelsReset(resetIds.ToArray());

        Context.Respond($"Reset {count} voxel maps.");
    }

    public void ResetPlanets()
    {
        var voxelMaps = MyEntities.GetEntities().OfType<MyPlanet>();

        var resetIds = new List<long>();

        foreach (var map in voxelMaps)
        {
            try
            {
                if (map.StorageName == null || map.Storage.DataProvider == null)
                    continue;

                map.Storage.Reset(MyStorageDataTypeFlags.All);
                resetIds.Add(map.EntityId);
            }
            catch (Exception e)
            {
                Log.Error(e, $"Could not reset voxel map {map.StorageName}");
            }
        }

        EssentialsCore.Host.NotifyVoxelsReset(resetIds.ToArray());
        Context.Respond($"Reset {resetIds.Count} voxel maps.");
    }

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
                Context.Respond($"Reset planet {map.Name}");
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

        Context.Respond(ResetVoxelInArea(entity.PositionComp.GetPosition(), radius)
            ? "Voxel reset complete!" 
            : "Couldn't reset voxels! Check log for more information!");
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

        if (!double.TryParse(args[args.Count - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var radius) || radius <= 0)
        {
            Context.Respond("Invalid radius!");
            return;
        }

        if (!TryParsePosition(string.Join(" ", args.Take(args.Count - 1)), out var resetTarget))
        {
            Context.Respond($"Invalid coordinates! {usage}");
            return;
        }

        Context.Respond(ResetVoxelInArea(resetTarget, (float)radius)
            ? "Voxel reset complete!"
            : "Couldn't reset voxels! Check log for more information!");
    }

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
            parts = new[] { gpsParts[2], gpsParts[3], gpsParts[4] };
        }
        else
        {
            parts = text.Split(new[] { ':', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
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

    public static bool ResetVoxelInArea(Vector3D center, float radius, bool updatePlayers = true)
    {
        try
        {
            BoundingSphereD Sphere = new BoundingSphereD(center, radius);
            // Use our own list: MyEntities.GetEntitiesInSphere returns SE's shared list, which callers must clear.
            var found = new List<MyEntity>();
            MyGamePruningStructure.GetAllEntitiesInSphere(ref Sphere, found);
            List<MyVoxelBase> Maps = found.OfType<MyVoxelBase>().ToList();
            if (Maps.Count == 0)
                return true;

            var m = typeof(MyVoxelBase).GetMethod("PerformVoxelOperationSphere_Implementation", BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var voxelMap in Maps)
            {
                if (voxelMap.MarkedForClose)
                    continue;

                MyShapeSphere shape = new()
                {
                    Center = center,
                    Radius = radius
                };

                MyVoxelGenerator.RevertShape(voxelMap, shape);

                if (updatePlayers && m != null)
                    Events.RaiseEvent<MyVoxelBase, Vector3D, float, byte, OperationType>(voxelMap.RootVoxel, m, shape.Center, shape.Radius, 0, MyVoxelBase.OperationType.Revert);

            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Voxel reset failed!");
            return false;
        }
    }

    private static LockToken? PinDelete()
    {
        /*
         * If SEWorldGenerator Plugin is used or Procedural Seed is 0 there
         * will be no GeneratorInstance. In this case this Method will NRE.
         *
         * However we still would like to delete stuff. So just return null.
         * Because when there is no GeneratorInstance we dont have to tell it
         * entities are being deleted.
         */
        var generator = GeneratorInstance;
        return generator == null ? null : new LockToken(generator);
    }

    private class LockToken : IDisposable
    {
        private readonly MyProceduralAsteroidCellGenerator _generator;

        public LockToken(MyProceduralAsteroidCellGenerator generator)
        {
            _generator = generator;
            IsClosingEntitiesField?.SetValue(_generator, true);
        }

        public void Dispose()
        {
            IsClosingEntitiesField?.SetValue(_generator, false);
        }
    }
}