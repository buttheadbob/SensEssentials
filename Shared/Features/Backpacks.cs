using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.Entity;

namespace SensEssentials;

/// <summary>
/// Limits how many backpacks (dropped inventory bags) each player has in the world. Empty bags and bags over the
/// limit are deleted after 30 seconds. Everything here runs on the game thread.
/// </summary>
public static class Backpacks
{
    private static readonly TimeSpan RemoveDelay = TimeSpan.FromSeconds(30);
    private static readonly Dictionary<long, List<MyInventoryBagEntity>> BagTracker = new();
    private static readonly Queue<Tuple<MyInventoryBagEntity, DateTime>> RemovalTracker = new();

    public static void Clear()
    {
        BagTracker.Clear();
        RemovalTracker.Clear();
    }

    /// <summary>
    /// Hooked to MyEntities.OnEntityAdd. Raised by SE while adding the entity; a throw here would break the add.
    /// </summary>
    public static void OnEntityAdded(MyEntity myEntity)
    {
        try
        {
            var limit = EssentialsCore.Config.BackpackLimit;
            if (limit < 0 || myEntity is not MyInventoryBagEntity b)
                return;

            if (limit == 0)
            {
                RemovalTracker.Enqueue(Tuple.Create(b, DateTime.Now + RemoveDelay));
                return;
            }

            if (!BagTracker.TryGetValue(b.OwnerIdentityId, out var bags))
            {
                bags = new List<MyInventoryBagEntity>(limit);
                BagTracker.Add(b.OwnerIdentityId, bags);
            }

            bags.Add(b);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error tracking backpack");
        }
    }

    public static void Update()
    {
        try
        {
            ProcessBags();
        }
        catch (Exception e)
        {
            // Drop the tracked bags so a bad entry does not throw again every tick.
            Log.Error(e, "Error processing backpacks");
            Clear();
        }
    }

    private static void ProcessBags()
    {
        var limit = Math.Max(EssentialsCore.Config.BackpackLimit, 0);

        //bags don't have inventory in the Add event, so we wait until the next tick. I hate everything.
        foreach (var bags in BagTracker.Values)
        {
            //iterate backwards so we can remove while we iterate
            for (int i = bags.Count - 1; i >= 0; i--)
            {
                var b = bags[i];

                // Picked up or removed by something else.
                if (b.MarkedForClose || b.Closed)
                {
                    bags.RemoveAt(i);
                    continue;
                }

                if (b.GetInventory()?.GetItemsCount() > 0)
                    continue;

                RemovalTracker.Enqueue(Tuple.Create(b, DateTime.Now + RemoveDelay));
                bags.RemoveAt(i);
            }
            //lazy
            while (bags.Count > limit)
            {
                var rm = bags[0];
                bags.RemoveAt(0);
                RemovalTracker.Enqueue(Tuple.Create(rm, DateTime.Now + RemoveDelay));
            }
        }

        if (RemovalTracker.Count > 0)
        {
            var b = RemovalTracker.Peek();
            if (DateTime.Now >= b.Item2)
            {
                RemovalTracker.Dequeue();
                if (!b.Item1.MarkedForClose && !b.Item1.Closed)
                    b.Item1.Close();
            }
        }
    }
}
