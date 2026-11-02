using SensEssentials.Commands;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace SensEssentials.TorchHost.Commands;

/// <summary>
/// World maintenance commands, with the group as the first word: !identity, !rep, !faction, !sandbox.
/// </summary>
public class WorldModule : EssentialsModule
{
    private WorldActions Actions => new(Call);

    [Command("identity clean", "Resets identities that have not logged in for X days to a brand-new state (grids, faction, GPS, reputation, credits). The identities are kept. Usage: identity clean <days> [includeNpcs]")]
    [Permission(MyPromoteLevel.SpaceMaster)]
    public void CleanIdentities(int days, bool includeNpcs = false) => Actions.CleanIdentities(days, includeNpcs);

    [Command("identity purge", "Removes identities that have not logged in for X days, together with everything they own (grids, faction membership, GPS, reputation, credits). Usage: identity purge <days> [includeNpcs]")]
    [Permission(MyPromoteLevel.Admin)]
    public void PurgeIdentities(int days, bool includeNpcs = false) => Actions.PurgeIdentities(days, includeNpcs);

    [Command("identity clear", "Resets one player's identity to a brand-new state (grids, faction, GPS, reputation, credits). The identity is kept.")]
    [Permission(MyPromoteLevel.Admin)]
    public void ClearIdentity(string playername) => Actions.ClearIdentity(playername);

    [Command("rep wipe", "Resets the reputation on the server")]
    public void WipeReputation(bool removePlayerToFaction = true, bool removeFactionToFaction = true) => Actions.WipeReputation(removePlayerToFaction, removeFactionToFaction);

    [Command("faction clean", "Removes factions with fewer than the given number of players.")]
    public void CleanFactions(int memberCount = 1) => Actions.CleanFactions(memberCount);

    [Command("faction remove", "removes faction by tag name")]
    [Permission(MyPromoteLevel.Admin)]
    public void RemoveFaction(string? tag) => Actions.RemoveFaction(tag);

    [Command("faction info", "Lists the members of every faction.")]
    [Permission(MyPromoteLevel.Admin)]
    public void FactionInfo() => Actions.FactionInfo();

    [Command("sandbox clean", "Cleans up junk data from the sandbox file")]
    [Permission(MyPromoteLevel.SpaceMaster)]
    public void CleanSandbox() => Actions.CleanSandbox();
}
