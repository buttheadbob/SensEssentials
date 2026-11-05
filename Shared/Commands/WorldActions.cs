using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Game.World.Generator;
using Sandbox.ModAPI;
using VRage.Game;

namespace SensEssentials.Commands;

/// <summary>
/// World maintenance commands: !identity, !rep, !faction and !sandbox.
/// </summary>
public sealed class WorldActions(ICommandContext context) : CommandActions(context)
{
    public void CleanIdentities(int days, bool includeNpcs)
    {
        if (!CheckDays(days))
            return;

        var identities = GetInactiveIdentities(days, includeNpcs);
        if (identities.Count == 0)
        {
            Context.Respond($"No identities found that have been inactive for more than {days} days.");
            return;
        }

        var owned = OwnedGroups();
        int grids = identities.Sum(identity => ResetIdentity(identity, removeIdentity: false, owned));
        CleanFaction_Internal();
        Context.Respond($"Reset {identities.Count} identities and deleted {grids} grids they owned.");
    }

    public void PurgeIdentities(int days, bool includeNpcs)
    {
        if (!CheckDays(days))
            return;

        var identities = GetInactiveIdentities(days, includeNpcs);
        if (identities.Count == 0)
        {
            Context.Respond($"No identities found that have been inactive for more than {days} days.");
            return;
        }

        var owned = OwnedGroups();
        int grids = identities.Sum(identity => ResetIdentity(identity, removeIdentity: true, owned));
        CleanFaction_Internal();
        Context.Respond($"Removed {identities.Count} identities and {grids} grids they owned.");
    }

    private bool CheckDays(int days)
    {
        if (days >= 1)
            return true;

        Context.Respond("Days must be at least 1, or every offline player would match.");
        return false;
    }

    public void ClearIdentity(string playername)
    {
        var found = Utilities.GetIdentityByNameOrIds(playername);
        var identity = found == null ? null : MySession.Static.Players.TryGetIdentity(found.IdentityId);

        if (identity == null)
        {
            Context.Respond($"No Identity found for {playername}.  Try Again");
            return;
        }

        var steamId = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
        if (steamId != 0 && !CanActOn(steamId))
            return;

        int grids = ResetIdentity(identity, removeIdentity: false, OwnedGroups());
        CleanFaction_Internal();
        Context.Respond($"Reset identity {identity.DisplayName} and deleted {grids} grids they owned.");
    }

    /// <summary>
    /// Offline identities whose last login is older than the given number of days.
    /// NPC identities are only included when asked for.
    /// </summary>
    private static List<MyIdentity> GetInactiveIdentities(int days, bool includeNpcs)
    {
        var cutoff = DateTime.Now - TimeSpan.FromDays(days);
        return [.. MySession.Static.Players.GetAllIdentities()
            .Where(identity => identity.LastLoginTime < cutoff)
            .Where(identity => !MySession.Static.Players.IsPlayerOnline(identity.IdentityId))
            .Where(identity => includeNpcs || !Utils.Ownership.IsNpcIdentity(identity.IdentityId))];
    }

    /// <summary>
    /// Every grid group with its owners, worked out once so a command over many identities does not rescan all
    /// blocks for each one.
    /// </summary>
    private static List<(List<MyCubeGrid> Group, HashSet<long> Owners)> OwnedGroups() =>
        [.. GridFinder.GetAllGrids().Values.Select(group => (group, Utils.Ownership.GetAllOwnerIds(group)))];

    /// <summary>
    /// Returns an identity to a brand-new state: deletes grid groups it is the majority owner of (other owners'
    /// shared groups get its blocks instead), deletes its offline character, removes it from its faction and
    /// clears its GPS, reputation and credits. When removeIdentity is set the identity itself is removed as well.
    /// Returns the number of grids deleted.
    /// </summary>
    private static int ResetIdentity(MyIdentity identity, bool removeIdentity, List<(List<MyCubeGrid> Group, HashSet<long> Owners)> owned)
    {
        long id = identity.IdentityId;
        int deleted = 0;

        foreach (var (group, owners) in owned)
        {
            // An earlier identity in the same command may have deleted this group already.
            if (!owners.Contains(id) || group.All(g => g.MarkedForClose))
                continue;

            long majority = Utils.Ownership.GetOwner(group);
            if (majority == id)
            {
                foreach (var grid in group)
                {
                    Utilities.EjectPilots(grid);
                    grid.Close();
                    deleted++;
                }
                continue;
            }

            // Shared group owned mostly by someone else: hand this identity's blocks to the majority owner.
            foreach (var grid in group)
            {
                grid.TransferBlocksBuiltByID(id, majority);
                foreach (var block in grid.GetFatBlocks().Where(b => b.OwnerId == id).ToList())
                {
                    // Ownership can only move to or from nobody, so go through 0.
                    block.ChangeBlockOwnerRequest(0, MyOwnershipShareModeEnum.Faction);
                    block.ChangeBlockOwnerRequest(majority, MyOwnershipShareModeEnum.Faction);
                }
            }
        }

        if (!MySession.Static.Players.IsPlayerOnline(id) && identity.Character is { MarkedForClose: false } character)
            character.Close();

        LeaveFaction(id);

        var gpsApi = MyAPIGateway.Session.GPS;
        foreach (var gps in gpsApi.GetGpsList(id).ToList())
            gpsApi.RemoveGps(id, gps);

        var relations = _playerRelationsGet?.Invoke(MySession.Static.Factions);
        if (relations != null)
            foreach (var pair in relations.Keys.Where(pair => pair.RelateeId1 == id).ToList())
                relations.Remove(pair);

        long balance = MyBankingSystem.GetBalance(id);
        if (balance > 0)
            MyBankingSystem.ChangeBalance(id, -balance);

        if (removeIdentity)
        {
            // Passing the player id also removes the steam id mapping, so a returning player gets a fresh identity.
            MySession.Static.Players.TryGetPlayerId(id, out MyPlayer.PlayerId playerId);
            MySession.Static.Players.RemoveIdentity(id, playerId);
        }

        return deleted;
    }

    public void WipeReputation(bool removePlayerToFaction, bool removeFactionToFaction)
    {
        var count = WipeRep(removePlayerToFaction, removeFactionToFaction);
        Context.Respond($"Wiped {count} reputations");
    }
        
    public void CleanFactions(int memberCount)
    {
        int count = CleanFaction_Internal(memberCount);

        Context.Respond($"Removed {count} factions with fewer than {memberCount} members.");
    }

    public void RemoveFaction(string? tag)
    {
        if (tag == null)
        {
            Context.Respond("You need to add a faction tag to remove");
            return;
        }

        var fac = MySession.Static.Factions.TryGetFactionByTag(tag);
        if (fac == null || !MySession.Static.Factions.FactionTagExists(tag))
        {
            Context.Respond($"{tag} is not a faction on this server");
            return;
        }

        RemoveWithMembers(fac);
        Context.Respond(MySession.Static.Factions.FactionTagExists(tag)
            ? $"{tag} removal failed"
            : $"{tag} removal successful");
    }

    public void FactionInfo()
    {
        StringBuilder sb = new();

        foreach (KeyValuePair<long, MyFaction> factionId in MySession.Static.Factions)
        {
            var faction = factionId.Value;
            double memberCount = faction.Members.Count();
            sb.AppendLine();
            if (faction.IsEveryoneNpc())
            {
                sb.AppendLine($"{faction.Tag} - {memberCount} NPC found in this faction");
                continue;
            }
            sb.AppendLine($"{faction.Tag} - {memberCount} players in this faction");
            foreach (var member in faction.Members.Keys.ToList())
            {
                // Members whose identity was deleted have no name to show.
                var name = MySession.Static.Players.TryGetIdentity(member)?.DisplayName;
                if (string.IsNullOrEmpty(name))
                    continue;
                sb.AppendLine(name);
            }
        }
        Show("Faction Info", null, sb.ToString());
    }

    private static int CleanFaction_Internal(int memberCount = 1)
    {
        int result = 0;

        foreach (var faction in MySession.Static.Factions.ToList())
        {
            if ((faction.Value.IsEveryoneNpc() || !faction.Value.AcceptHumans) && faction.Value.Members.Count != 0) //needed to add this to catch the 0 member factions
                continue;

            int validmembers = 0;
            foreach (var member in faction.Value.Members)
            {
                // Members whose identity no longer exists do not count.
                if (!MySession.Static.Players.HasIdentity(member.Key))
                    continue;

                validmembers++;

                if (validmembers >= memberCount)
                    break;
            }

            if (validmembers >= memberCount)
                continue;

            RemoveWithMembers(faction.Value);
            result++;
        }

        return result;
    }

    /// <summary>
    /// The game removes the faction after its last member, and promotes the next member when the founder leaves.
    /// </summary>
    private static void LeaveFaction(long identityId)
    {
        if (MySession.Static.Factions.GetPlayerFaction(identityId) != null)
            MyVisualScriptLogicProvider.KickPlayerFromFaction(identityId);
    }
        
    /// <summary>
    /// Kicks everyone and then removes the faction, since removing it alone leaves its members linked to it in the save.
    /// </summary>
    private static void RemoveWithMembers(MyFaction faction)
    {
        // Kick from a snapshot, since kicking changes the member list. The founder goes last so the faction
        // always has a founder while other members remain.
        var members = faction.Members.Keys.ToList();
        var kickOrder = members.Where(m => m != faction.FounderId).ToList();
        if (members.Contains(faction.FounderId))
            kickOrder.Add(faction.FounderId);

        foreach (var member in kickOrder)
        {
            if (MySession.Static.Factions.TryGetFactionById(faction.FactionId) == null)
                break;
            MyFactionCollection.KickMember(faction.FactionId, member);
        }

        if (MySession.Static.Factions.TryGetFactionById(faction.FactionId) != null)
            RemoveFaction(faction);
    }

    /// <summary>
    /// Removes a faction from the server and all clients, along with its bank account.
    /// </summary>
    private static void RemoveFaction(MyFaction faction) => MyAPIGateway.Session.Factions.RemoveFaction(faction.FactionId);

    private static int FixBlockOwnership()
    {
        int count = 0;
        foreach (var entity in MyEntities.GetEntities())
        {
            if (entity is not MyCubeGrid grid)
                continue;
            var owner = Utils.Ownership.GetOwner(grid);
            if (!MySession.Static.Players.HasIdentity(owner))
                owner = 0;

            var share = owner == 0 ? MyOwnershipShareModeEnum.All : MyOwnershipShareModeEnum.Faction;
            foreach (var block in grid.GetFatBlocks().ToList())
            {
                if (block.OwnerId == 0 || MySession.Static.Players.HasIdentity(block.OwnerId))
                    continue;

                // Ownership can only move to or from nobody, so go through 0. The request also reaches clients.
                block.ChangeBlockOwnerRequest(0, share);
                if (owner != 0)
                    block.ChangeBlockOwnerRequest(owner, share);
                count++;
            }
        }
        return count;
    }

    private static readonly FieldInfo? GpsDicField = typeof(MyGpsCollection).GetField("m_playerGpss", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? SeedParamField = typeof(MyProceduralWorldGenerator).GetField("m_existingObjectsSeeds", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? CamerasField = typeof(MySession).GetField("Cameras", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? AllCamerasField = CamerasField?.FieldType.GetField("m_entityCameraSettings", BindingFlags.NonPublic | BindingFlags.Instance);

    public void CleanSandbox()
    {
        int count = 0;
        var validIdentities = new HashSet<long>();
        var idCache = new HashSet<long>();

        //find all identities owning a block
        foreach (var entity in MyEntities.GetEntities())
        {
            if (entity is not MyCubeGrid grid)
                continue;
            validIdentities.UnionWith(Utils.Ownership.GetAllOwnerIds(grid));
        }

        foreach (var online in MySession.Static.Players.GetOnlinePlayers())
        {
            if (online.Identity != null)
                validIdentities.Add(online.Identity.IdentityId);
        }


        // Remove identities that own no blocks. NPCs and identities with no name are kept.
        foreach (var identity in MySession.Static.Players.GetAllIdentities().ToList())
        {
            if (Utils.Ownership.IsNpcIdentity(identity.IdentityId)  || string.IsNullOrEmpty(identity.DisplayName))
            {
                validIdentities.Add(identity.IdentityId);
                continue;
            }

            if (validIdentities.Contains(identity.IdentityId))
            {
                continue;
            }

            LeaveFaction(identity.IdentityId);
            // Removing an identity leaves its offline character in the world.
            if (identity.Character is { MarkedForClose: false } character)
                character.Close();
            MySession.Static.Players.TryGetPlayerId(identity.IdentityId, out MyPlayer.PlayerId playerId);
            MySession.Static.Players.RemoveIdentity(identity.IdentityId, playerId);
            count++;
        }

        //reset ownership of blocks belonging to deleted identities
        count += FixBlockOwnership();

        //clean up empty factions
        count += CleanFaction_Internal();

        //cleanup reputations
        count += CleanupReputations();

        // The game has no public list of who has GPS markers.
        var playerGpss = GpsDicField?.GetValue(MySession.Static.Gpss) as Dictionary<long, Dictionary<int, MyGps>>;
        if (playerGpss is not null)
        {
            foreach (var id in playerGpss.Keys)
            {
                if (!validIdentities.Contains(id))
                    idCache.Add(id);
            }

            foreach (var id in idCache)
                playerGpss.Remove(id);
        }

        count += idCache.Count;
        idCache.Clear();

        var g = MySession.Static.GetComponent<MyProceduralWorldGenerator>();
        var f = SeedParamField?.GetValue(g) as HashSet<MyObjectSeedParams>;
        count += f?.Count ?? 0;
        f?.Clear();
            
        var cf = AllCamerasField?.GetValue(CamerasField?.GetValue(MySession.Static)) as Dictionary<MyPlayer.PlayerId, Dictionary<long, MyEntityCameraSettings>>;
        count += cf?.Count ?? 0;
        cf?.Clear();

        Context.Respond($"Removed {count} unnecessary elements.");
    }

    private static readonly FieldInfo? RelationsField =
        typeof(MyFactionCollection).GetField("m_relationsBetweenFactions", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? PlayerRelationsField =
        typeof(MyFactionCollection).GetField("m_relationsBetweenPlayersAndFactions", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly Func<MyFactionCollection, Dictionary<MyFactionCollection.MyRelatablePair, Tuple<MyRelationsBetweenFactions, int>>?>? _relationsGet =
        RelationsField == null ? null : factions => RelationsField.GetValue(factions) as Dictionary<MyFactionCollection.MyRelatablePair, Tuple<MyRelationsBetweenFactions, int>>;

    private static readonly Func<MyFactionCollection, Dictionary<MyFactionCollection.MyRelatablePair, Tuple<MyRelationsBetweenFactions, int>>?>? _playerRelationsGet =
        PlayerRelationsField == null ? null : factions => PlayerRelationsField.GetValue(factions) as Dictionary<MyFactionCollection.MyRelatablePair, Tuple<MyRelationsBetweenFactions, int>>;

    private static int WipeRep(bool removePlayerToFaction, bool removeFactionToFaction)
    {
        var result = 0;
        var collection0 = _relationsGet?.Invoke(MySession.Static.Factions);
        var collection1 = _playerRelationsGet?.Invoke(MySession.Static.Factions);

        if (removeFactionToFaction)
        {
            if (collection0 != null)
                foreach (var pair in collection0.Keys.ToList())
                {
                    collection0.Remove(pair);
                    result++;
                }
        }

        if (collection1 != null)
            if (removePlayerToFaction)
            {
                foreach (var pair in collection1.Keys.ToList())
                {
                    collection1.Remove(pair);
                    result++;
                }
            }

        return result;

    }
    private static int CleanupReputations()
    {
        var collection = _relationsGet?.Invoke(MySession.Static.Factions);
        var collection2 = _playerRelationsGet?.Invoke(MySession.Static.Factions);


        var validIdentities = new HashSet<long>();

        //find all identities owning a block
        foreach (var entity in MyEntities.GetEntities())
        {
            if (entity is not MyCubeGrid grid)
                continue;
            validIdentities.UnionWith(Utils.Ownership.GetAllOwnerIds(grid));
        }

        //find online identities
        foreach (var online in MySession.Static.Players.GetOnlinePlayers())
        {
            if (online.Identity != null)
                validIdentities.Add(online.Identity.IdentityId);
        }

        foreach (var identity in MySession.Static.Players.GetAllIdentities().ToList())
        {
            if (Utils.Ownership.IsNpcIdentity(identity.IdentityId))
            {
                validIdentities.Add(identity.IdentityId);
            }
        }

        //Add Factions with at least one member to valid identities
        foreach (var faction in MySession.Static.Factions.Factions.Where(x=>x.Value.Members.Count > 0))
        {
            validIdentities.Add(faction.Key);
        }

        var result = 0;

        var collection0List = collection?.Keys.ToList();
        var collection1List = collection2?.Keys.ToList();

        if (collection0List != null)
            foreach (var pair in collection0List)
            {
                if (validIdentities.Contains(pair.RelateeId1) && validIdentities.Contains(pair.RelateeId2))
                    continue;
                collection?.Remove(pair);
                result++;
            }

        if (collection1List != null)
            foreach (var pair in collection1List)
            {
                if (validIdentities.Contains(pair.RelateeId1) && validIdentities.Contains(pair.RelateeId2))
                    continue;
                collection2?.Remove(pair);
                result++;
            }

        return result;
    }
}