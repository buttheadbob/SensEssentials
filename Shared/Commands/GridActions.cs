using System;
using System.IO;
using System.Linq;
using System.Text;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Game;
using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.ObjectBuilders.Private;

namespace SensEssentials.Commands;

/// <summary>
/// The !grids commands.
/// </summary>
public sealed class GridActions(ICommandContext context) : CommandActions(context)
{
    public void SetOwner(string gridName, string playerName)
    {
        Utilities.TryGetEntityByNameOrId(gridName, out IMyEntity? entity);

        if (entity is not IMyCubeGrid grid)
        {
            Context.Respond($"Grid {gridName} not found.");
            return;
        }

        long identityId;
        if (!long.TryParse(playerName, out identityId))
        {
            var player = Utilities.GetIdentityByNameOrIds(playerName);
            if (player == null)
            {
                Context.Respond($"Player {playerName} not found.");
                return;
            }
            identityId = player.IdentityId;
        }

        grid.ChangeGridOwnership(identityId, MyOwnershipShareModeEnum.Faction);
        Context.Respond($"Transferred ownership of {grid.DisplayName} to {identityId}");
    }

    public void Eject(string? gridName)
    {
        int ejectedPlayersCount = 0;

        // Snapshot entity list for thread safety
        List<MyEntity> entities = MyEntities.GetEntities().ToList();

        List<MyCubeGrid> grids = entities
            .OfType<MyCubeGrid>()
            .Where(g => g.Physics != null && (gridName == null || string.Equals(g.DisplayName, gridName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(gridName))
        {
            if (grids.Count == 0)
            {
                Context.Respond($"No grids found with name '{gridName}'.");
                return;
            }
                
            if (grids.Count > 1)
            {
                Context.Respond($"{grids.Count} grids found with name '{gridName}'.  Aborting.");
                return;
            }
                
            // Exactly one grid matches
            MyCubeGrid? grid = grids.FirstOrDefault();
            if (grid == null)
            {
                // safety check
                Context.Respond($"No grids found with name '{gridName}'.");
                return;   
            }

            List<MyCockpit> cockpits = grid.GetFatBlocks<MyCockpit>()
                .Where(c => c.Pilot != null)
                .ToList();

            foreach (MyCockpit cockpit in cockpits)
            {
                cockpit.RemovePilot();
                ejectedPlayersCount++;
            }
        }
        else
        {
            // No grid name given – eject everyone from all cockpits
            List<MyCockpit> cockpits = grids
                .SelectMany(g => g.GetFatBlocks<MyCockpit>())
                .Where(c => c.Pilot != null)
                .ToList();

            foreach (MyCockpit cockpit in cockpits)
            {
                cockpit.RemovePilot();
                ejectedPlayersCount++;
            }
        }

        Context.Respond($"Ejected {ejectedPlayersCount} player(s){(gridName != null ? $" from '{gridName}'" : "")}.");
    }

    public void StaticLarge()
    {
        int count = 0;
        foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>().Where(g => g.GridSizeEnum == MyCubeSize.Large && g.Projector == null && !g.IsStatic).ToList())
        {
            grid.OnConvertedToStationRequest(); //Keen why do you do this to me?
            count++;
        }

        Context.Respond($"Converted {count} large grids to stations.");
    }

    public void StopAll()
    {
        int count = 0;
        foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>().Where(x => x.Projector == null).ToList())
        {
            if (grid.Physics == null)
                continue;

            grid.Physics.ClearSpeed();
            count++;
        }

        Context.Respond($"Stopped {count} grids.");
    }

    private const string GridListGpsTag = "[Grids List]";

    public void List()
    {
        if (Context.Player == null)
        {
            Context.Respond("This command can only be used in-game.");
            return;
        }

        var id = Context.Player.IdentityId;
        var config = EssentialsCore.Config;
        var gpsApi = MyAPIGateway.Session?.GPS;
        StringBuilder sb = new StringBuilder();

        // Replace markers from the previous run instead of adding duplicates.
        if (config.MarkerShowPosition && gpsApi != null)
        {
            foreach (var old in gpsApi.GetGpsList(id).Where(g => g.Description?.StartsWith(GridListGpsTag) == true).ToList())
                gpsApi.RemoveGps(id, old);
        }

        foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>().ToList())
        {
            if (grid.Projector != null || !grid.BigOwners.Contains(id))
                continue;

            sb.AppendLine($"{grid.DisplayName} - {grid.GridSizeEnum} - {grid.BlocksCount} blocks - Position {(config.UtilityShowPosition ? grid.PositionComp.GetPosition().ToString() : "Unknown")}");
            if (config.MarkerShowPosition && gpsApi != null)
            {
                IMyGps gridGPS = gpsApi.Create(grid.DisplayName, $"{GridListGpsTag} {grid.DisplayName} - {grid.GridSizeEnum} - {grid.BlocksCount} blocks", grid.PositionComp.GetPosition(), true);
                gpsApi.AddGps(id, gridGPS);
            }
        }

        if (sb.Length == 0)
            sb.AppendLine("You don't own any grids.");

        Show("Grids List", $"Ships/Stations owned by {Context.Player.DisplayName}", sb.ToString());
    }

    /// <summary>
    /// Exports live in the plugin's storage folder (Torch: the instance folder), not the server's working folder,
    /// which depends on how the server was started.
    /// </summary>
    private static string ExportFolder => Path.Combine(EssentialsCore.Host.StorageDir, "ExportedGrids");

    /// <summary>
    /// Path of an export file. Returns null when the name could point outside the export folder.
    /// </summary>
    private static string? GetExportPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(".."))
            return null;

        return Path.Combine(ExportFolder, name + ".xml");
    }

    public void Export(string gridName, string exportName)
    {
        var path = GetExportPath(exportName);
        if (path == null)
        {
            Context.Respond("Invalid export name. Use a plain file name without a folder or extension.");
            return;
        }

        Directory.CreateDirectory(ExportFolder);
        if (!Utilities.TryGetEntityByNameOrId(gridName, out var ent) || ent is not IMyCubeGrid)
        {
            Context.Respond("Grid not found.");
            return;
        }

        if (File.Exists(path))
        {
            Context.Respond("Export file already exists.");
            return;
        }

        MyObjectBuilderSerializerKeen.SerializeXML(path, false, ent.GetObjectBuilder());
        Context.Respond($"Grid saved as {exportName}");
    }
        
    public void Import(string gridName, string? targetName)
    {
        var path = GetExportPath(gridName);
        if (path == null)
        {
            Context.Respond("Invalid file name. Use the name the grid was exported with.");
            return;
        }

        Directory.CreateDirectory(ExportFolder);
        if (targetName == null && Context.Player == null)
        {
            Context.Respond("Target entity must be specified.");
            return;
        }

        IMyEntity? ent;
        if (targetName == null)
        {
            ent = Context.Player!.Controller?.ControlledEntity?.Entity;
            if (ent == null || Context.Player.Character == null || Context.Player.Character.IsDead)
            {
                Context.Respond("You must be alive in the world to spawn a grid next to you.");
                return;
            }
        }
        else if (!Utilities.TryGetEntityByNameOrId(targetName, out ent))
        {
            Context.Respond("Target entity not found.");
            return;
        }

        if (!File.Exists(path))
        {
            Context.Respond("File does not exist.");
            return;
        }

        if (MyObjectBuilderSerializerKeen.DeserializeXML(path, out MyObjectBuilder_CubeGrid grid))
        {
            Context.Respond($"Importing grid {gridName}");
            MyEntities.RemapObjectBuilder(grid);
            var pos = MyEntities.FindFreePlaceCustom(ent!.GetPosition(), grid.CalculateBoundingSphere().Radius);
            if (pos == null)
            {
                Context.Respond("No free place.");
                return;
            }

            var x = grid.PositionAndOrientation ?? new MyPositionAndOrientation();
            x.Position = pos.Value;
            grid.PositionAndOrientation = x;
            MyEntities.CreateFromObjectBuilderParallel(grid, true);
        }
    }
}