using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;

namespace SensEssentials.Commands;

/// <summary>
/// The !blocks commands. The selector, value and conditions are read from the context's arguments.
/// </summary>
public sealed class BlocksActions(ICommandContext context) : CommandActions(context)
{
    public void On()
    {
        if (Context.Args.Count == 0)
        {
            Context.Respond("Usage: !blocks on [type|subtype|general] <value> [conditions...]");
            Context.Respond("Examples:");
            Context.Respond("  !blocks on type MyObjectBuilder_Reactor");
            Context.Respond("  !blocks on subtype LargeBlockSmallGenerator");
            Context.Respond("  !blocks on general power");
            Context.Respond("  !blocks on type MyObjectBuilder_BatteryBlock ownedby PlayerName");
            return;
        }

        ProcessBlockCommand(BlockAction.TurnOn);
    }

    public void Off()
    {
        if (Context.Args.Count == 0)
        {
            Context.Respond("Usage: !blocks off [type|subtype|general] <value> [conditions...]");
            Context.Respond("Examples:");
            Context.Respond("  !blocks off type MyObjectBuilder_Reactor");
            Context.Respond("  !blocks off subtype LargeBlockSmallGenerator");
            Context.Respond("  !blocks off general weapons");
            Context.Respond("  !blocks off type MyObjectBuilder_LargeGatlingTurret playerdistancegreaterthan 5000");
            return;
        }

        ProcessBlockCommand(BlockAction.TurnOff);
    }

    public void Remove()
    {
        if (Context.Args.Count == 0)
        {
            Context.Respond("Usage: !blocks remove [type|subtype] <value> [conditions...]");
            Context.Respond("Examples:");
            Context.Respond("  !blocks remove type MyObjectBuilder_Beacon");
            Context.Respond("  !blocks remove subtype LargeBlockBeacon");
            Context.Respond("  !blocks remove type MyObjectBuilder_Beacon ownedby nobody");
            return;
        }

        ProcessBlockCommand(BlockAction.Remove);
    }

    public void Count()
    {
        if (Context.Args.Count == 0)
        {
            Context.Respond("Usage: !blocks count [type|subtype|general] <value> [conditions...]");
            Context.Respond("Examples:");
            Context.Respond("  !blocks count type MyObjectBuilder_Reactor");
            Context.Respond("  !blocks count general production");
            return;
        }

        ProcessBlockCommand(BlockAction.Count);
    }

    #region Core Processing Logic

    private enum BlockAction
    {
        TurnOn,
        TurnOff,
        Remove,
        Count
    }

    private void ProcessBlockCommand(BlockAction action)
    {
        if (Context.Args.Count < 2)
        {
            Context.Respond("Not enough arguments. Specify a selector (type/subtype/general) and value.");
            return;
        }

        var selector = Context.Args[0].ToLowerInvariant();
        var value = Context.Args[1];

        if ((selector == "type" && !BlockLookup.IsKnownType(value)) || (selector == "subtype" && !BlockLookup.IsKnownSubtype(value)))
        {
            Context.Respond($"Unknown block {selector} '{value}'. Nothing was changed.");
            return;
        }

        // Parse remaining args as conditions
        var conditionArgs = Context.Args.Skip(2).ToList();
            
        // Get grids matching conditions (if any)
        IEnumerable<MyCubeGrid> grids;
        grids = conditionArgs.Count > 0 
            ? ConditionsChecker.ScanConditions(Context, conditionArgs) 
            : MyEntities.GetEntities().OfType<MyCubeGrid>().Where(x => x.Projector == null);

        var gridsList = grids.ToList();
        if (gridsList.Count == 0)
        {
            Context.Respond("No grids found matching the specified conditions.");
            return;
        }

        int? processedCount;

        switch (selector)
        {
            case "type":
                processedCount = ProcessByType(gridsList, value, action);
                break;
            case "subtype":
                processedCount = ProcessBySubtype(gridsList, value, action);
                break;
            case "general":
            case "category":
                processedCount = ProcessByCategory(gridsList, value, action);
                break;
            default:
                Context.Respond($"Invalid selector '{selector}'. Use 'type', 'subtype', or 'general'.");
                return;
        }

        if (processedCount is { } count)
            ReportResult(action, count, selector, value);
    }

    private int ProcessByType(List<MyCubeGrid> grids, string type, BlockAction action)
    {
        int count = 0;

        // Armor and other simple blocks are only in the full block list.
        if (action is BlockAction.Remove or BlockAction.Count)
        {
            var matches = new List<MySlimBlock>();
            foreach (var grid in grids)
            {
                foreach (var block in grid.GetBlocks())
                {
                    if (Utilities.IsBlockType(block.BlockDefinition.Id.TypeId, type))
                        matches.Add(block);
                }
            }

            count = action == BlockAction.Remove ? RemoveBlocks(matches) : matches.Count;
        }
        else
        {
            foreach (var grid in grids)
            {
                foreach (var block in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    if (Utilities.IsBlockType(block.BlockDefinition.Id.TypeId, type))
                    {
                        if (ExecuteAction(block, action))
                            count++;
                    }
                }
            }
        }

        return count;
    }

    private int ProcessBySubtype(List<MyCubeGrid> grids, string subtype, BlockAction action)
    {
        int count = 0;

        if (action is BlockAction.Remove or BlockAction.Count)
        {
            var matches = new List<MySlimBlock>();
            foreach (var grid in grids)
            {
                foreach (var block in grid.GetBlocks())
                {
                    var blockSubtype = block.BlockDefinition.Id.SubtypeName;
                    if (string.Compare(subtype, blockSubtype, StringComparison.InvariantCultureIgnoreCase) == 0)
                        matches.Add(block);
                }
            }

            count = action == BlockAction.Remove ? RemoveBlocks(matches) : matches.Count;
        }
        else
        {
            foreach (var grid in grids)
            {
                foreach (var block in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    var blockSubtype = block.BlockDefinition.Id.SubtypeName;
                    if (string.Compare(subtype, blockSubtype, StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        if (ExecuteAction(block, action))
                            count++;
                    }
                }
            }
        }

        return count;
    }

    private int? ProcessByCategory(List<MyCubeGrid> grids, string category, BlockAction action)
    {
        // IsDefined rejects numbers, which TryParse accepts even when they match no category.
        if (!Enum.TryParse(category, true, out BlockCategory blockCategory) || !Enum.IsDefined(typeof(BlockCategory), blockCategory))
        {
            Context.Respond($"{category} is not a valid category. Use one of the following: " + 
                            string.Join(", ", Enum.GetNames(typeof(BlockCategory))));
            return null;
        }

        if (action == BlockAction.Remove)
        {
            Context.Respond("Remove action is not supported for categories. Use type or subtype instead.");
            return null;
        }

        int count = 0;
        foreach (var grid in grids)
        {
            foreach (var block in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
            {
                if (IsBlockTypeOf(block, blockCategory))
                {
                    if (ExecuteAction(block, action))
                        count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Removes the blocks and updates grid physics, so the removed blocks are not left as invisible collision.
    /// Returns the number actually removed.
    /// </summary>
    private static int RemoveBlocks(List<MySlimBlock> blocks)
    {
        int count = 0;
        foreach (var block in blocks)
        {
            var grid = block.CubeGrid;
            if (grid == null || grid.MarkedForClose || grid.GetCubeBlock(block.Min) != block)
                continue;

            grid.RemoveBlock(block, updatePhysics: grid.Physics != null);
            count++;
        }
        return count;
    }

    private bool ExecuteAction(MyFunctionalBlock block, BlockAction action)
    {
        switch (action)
        {
            case BlockAction.TurnOn:
                if (!block.Enabled)
                {
                    block.Enabled = true;
                    return true;
                }
                break;
            case BlockAction.TurnOff:
                if (block.Enabled)
                {
                    block.Enabled = false;
                    return true;
                }
                break;
            case BlockAction.Count:
                return true;
        }
        return false;
    }

    private void ReportResult(BlockAction action, int count, string selector, string value)
    {
        string actionText = action switch
        {
            BlockAction.TurnOn => "Enabled",
            BlockAction.TurnOff => "Disabled",
            BlockAction.Remove => "Removed",
            BlockAction.Count => "Found",
            _ => "Processed",
        };
        Context.Respond($"{actionText} {count} blocks of {selector} {value}.");
        Log.Info($"!blocks: {actionText} {count} blocks of {selector} {value}");
    }

    #endregion

    #region Helper Methods

    public bool IsBlockTypeOf(MyFunctionalBlock block, BlockCategory category)
    {
        return category switch
        {
            BlockCategory.Power => Utilities.ProducesPower(block),
            BlockCategory.Production => block is MyProductionBlock || block.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_OxygenGenerator),
            BlockCategory.Weapons => block is IMyUserControllableGun,
            _ => false,
        };
    }

    public enum BlockCategory
    {
        Power,
        Production,
        Weapons
    }

    #endregion
}