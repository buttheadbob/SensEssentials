using Sandbox.Game.Entities;
using System;
using System.Linq;
using System.Text;

namespace SensEssentials.Commands;

/// <summary>
/// The !cleanup commands. Conditions are read from the context's arguments.
/// </summary>
public sealed class CleanupActions(ICommandContext context) : CommandActions(context)
{
    public void Scan()
    {
        var count = ConditionsChecker.ScanConditions(Context, Context.Args).Count();
        Context.Respond($"Found {count} grids matching the given conditions.");
    }

    public void List()
    {
        var grids = ConditionsChecker.ScanConditions(Context, Context.Args).OrderBy(g => g.DisplayName).ToList();
        var list = String.Join("\n", grids.Select((g, i) => $"{i + 1}. {g.DisplayName} ({g.BlocksCount} block(s))"));
        if (Context.SentBySelf)
        {
            Context.Respond(list);
            Context.Respond($"Found {grids.Count} grids matching the given conditions.");
        }
        else
        {
            Show("Cleanup", $"Found {grids.Count} matching", list);
        }
    }

    public void Delete()
    {
        var count = 0;
        foreach (var grid in ConditionsChecker.ScanConditions(Context, Context.Args))
        {
            Log.Info($"Deleting grid: {grid.EntityId}: {grid.DisplayName}");
            Utilities.EjectPilots(grid);
            grid.Close();
            count++;
        }

        Context.Respond($"Deleted {count} grids matching the given conditions.");
        Log.Info($"Cleanup deleted {count} grids matching conditions {string.Join(", ", Context.Args)}");
    }

    public void DeleteFloatingObjects()
    {
        var count = 0;
        foreach (var floater in MyEntities.GetEntities().OfType<MyFloatingObject>().Where(f => !f.MarkedForClose).ToList())
        {
            Log.Info($"Deleting floating object: {floater.DisplayName}");
            floater.Close();
            count++;
        }
        Context.Respond($"Deleted {count} floating objects.");
        Log.Info($"Cleanup deleted {count} floating objects");
    }

    public void Help()
    {
        var sb = new StringBuilder();
        foreach (var c in ConditionsChecker.GetAllConditions())
        {
            sb.AppendLine($"{c.Command}{(string.IsNullOrEmpty(c.InvertCommand) ? string.Empty : $" ({c.InvertCommand})")}:");
            sb.AppendLine($"   {c.HelpText}");
        }

        Show("Cleanup help", null, sb.ToString());
    }
}
