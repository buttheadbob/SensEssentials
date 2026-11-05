using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using VRage.Game.Entity;

namespace SensEssentials;

/// <summary>
/// Limits how many backpacks (dropped inventory bags) each player has in the world. Empty bags and bags over the
/// limit are deleted after 30 seconds. A limit below zero means no limit. Everything here runs on the game thread.
/// </summary>
public static class Backpacks
{
    private static readonly TimeSpan RemoveDelay = TimeSpan.FromSeconds(30);
    private static readonly Dictionary<long, List<MyInventoryBagEntity>> BagTracker = [];
    private static readonly List<MyInventoryBagEntity> NewBags = [];
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private static readonly Queue<(MyInventoryBagEntity Bag, DateTime RemoveAt)> RemovalTracker = new();
    private static DateTime _nextCheck;

    public static void Clear()
    {
        BagTracker.Clear();
        NewBags.Clear();
        RemovalTracker.Clear();
    }

    /// <summary>
    /// Picks up the bags saved with the world, so the limit holds across restarts.
    /// </summary>
    public static void TrackExisting()
    {
        NewBags.AddRange(MyEntities.GetEntities().OfType<MyInventoryBagEntity>());
    }

    /// <summary>
    /// Hooked to MyEntities.OnEntityAdd. SE sets a bag's owner after adding it, so bags are sorted out in the next check.
    /// </summary>
    public static void OnEntityAdded(MyEntity myEntity)
    {
        if (myEntity is MyInventoryBagEntity bag)
            NewBags.Add(bag);
    }

    public static void Update()
    {
        // Bags wait 30 seconds anyway, so there is no need to look at them every tick.
        if (DateTime.UtcNow < _nextCheck)
            return;
        _nextCheck = DateTime.UtcNow + CheckInterval;

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
        var limit = EssentialsCore.Config.BackpackLimit;
        if (limit < 0)
        {
            Clear();
            return;
        }

        foreach (var bag in NewBags)
        {
            if (limit == 0)
            {
                RemovalTracker.Enqueue((bag, DateTime.UtcNow + RemoveDelay));
                continue;
            }

            // A bag with no owner belongs to nobody's limit.
            if (bag.OwnerIdentityId == 0)
                continue;

            if (!BagTracker.TryGetValue(bag.OwnerIdentityId, out var owned))
            {
                owned = new List<MyInventoryBagEntity>(limit);
                BagTracker.Add(bag.OwnerIdentityId, owned);
            }

            owned.Add(bag);
        }
        NewBags.Clear();

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

                RemovalTracker.Enqueue((b, DateTime.UtcNow + RemoveDelay));
                bags.RemoveAt(i);
            }
            //lazy
            while (bags.Count > limit)
            {
                var rm = bags[0];
                bags.RemoveAt(0);
                RemovalTracker.Enqueue((rm, DateTime.UtcNow + RemoveDelay));
            }
        }

        // This runs once a second now, so take everything that is due, not just one bag.
        while (RemovalTracker.Count > 0 && DateTime.UtcNow >= RemovalTracker.Peek().RemoveAt)
        {
            var bag = RemovalTracker.Dequeue().Bag;
            if (!bag.MarkedForClose && !bag.Closed)
                bag.Close();
        }
    }
}
