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

        // 0 gives the grid to nobody. Anything else must be a real identity, so a mistyped id cannot own a grid.
        long identityId = 0;
        if (playerName != "0")
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

    /// <summary>
    /// Ejects every pilot from one ship and everything attached to it by rotors, hinges and pistons: the ship with
    /// the given name or entity id, or without a name the ship the caller is looking at.
    /// </summary>
    public void Eject(string? gridName)
    {
        List<MyCubeGrid> group;
        if (string.IsNullOrWhiteSpace(gridName))
        {
            if (Context.Player == null)
            {
                Context.Respond("The console always has to give a grid name.");
                return;
            }

            if (Context.Player.Character is not { IsDead: false } character)
            {
                Context.Respond("You need to be spawned in to use this without a grid name.");
                return;
            }

            var lookAt = GridFinder.FindLookAtGroup(character);
            if (lookAt == null)
            {
                Context.Respond("No grid in your line of sight. Spectator view does not count.");
                return;
            }
            group = lookAt;
        }
        else
        {
            var groups = GridFinder.FindGroupsByName(gridName!);
            if (groups.Count == 0)
            {
                Context.Respond($"Grid with name '{gridName}' was not found.");
                return;
            }

            // Several ships share the name: refuse rather than guess which one was meant.
            if (groups.Count > 1)
            {
                Context.Respond($"{groups.Count} grids are named '{gridName}'. Nothing was done; use the grid's entity id.");
                return;
            }
            group = groups[0];
        }

        var cockpits = group.SelectMany(g => g.GetFatBlocks<MyCockpit>()).Where(c => c.Pilot != null).ToList();
        foreach (var cockpit in cockpits)
            cockpit.RemovePilot();

        Context.Respond($"Ejected {cockpits.Count} player(s) from '{GridFinder.GetMainGrid(group).DisplayName}'.");
    }

    public void StaticLarge()
    {
        int count = 0;
        foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>().Where(g => g.GridSizeEnum == MyCubeSize.Large && g.Projector == null && !g.IsStatic).ToList())
        {
            // The game skips a grid that is still moving.
            grid.Physics?.ClearSpeed();
            grid.OnConvertedToStationRequest();
            if (grid.IsStatic)
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
        StringBuilder sb = new();

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

        Context.Respond(MyObjectBuilderSerializerKeen.SerializeXML(path, false, ent.GetObjectBuilder())
            ? $"Grid saved as {exportName}. Only this grid is saved, not what is attached to it."
            : $"Could not save the grid as {exportName}. Check the log.");
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

        if (!MyObjectBuilderSerializerKeen.DeserializeXML(path, out MyObjectBuilder_CubeGrid grid))
        {
            Context.Respond($"Could not read {gridName}. The file is damaged or is not a grid.");
            return;
        }

        Context.Respond($"Importing grid {gridName}");
        MyEntities.RemapObjectBuilder(grid);
        var pos = MyEntities.FindFreePlaceCustom(ent!.GetPosition(), grid.CalculateBoundingSphere().Radius);
        if (pos == null)
        {
            Context.Respond("No free place.");
            return;
        }

        var x = grid.PositionAndOrientation ?? MyPositionAndOrientation.Default;
        x.Position = pos.Value;
        grid.PositionAndOrientation = x;
        MyEntities.CreateFromObjectBuilderParallel(grid, true);
    }
}