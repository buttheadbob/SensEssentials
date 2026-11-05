using PluginSdk.Commands;
using SensEssentials.Commands;
using VRage.Game.ModAPI;

// Voting and world maintenance. On Torch these are root level paths like "identity clean"; here the first word is
// the command root.
namespace SensEssentials.MagnetarHost.Commands;

[CommandRoot("vote", "Sens Essentials", "Vote on an auto command")]
public sealed class VoteCommands : EssentialsCommands
{
    private VotingActions Actions => new(Call);

    [Command("", "Start a vote for an auto command. Usage: !vote <name>")]
    [Permission(MyPromoteLevel.None)]
    public void Vote(string name) => Actions.Vote(name);

    [Command("cancel", "Cancels the vote in progress")]
    [Permission(MyPromoteLevel.Admin)]
    public void VoteCancel() => Actions.VoteCancel();

    [Command("list", "Lists all vote commands")]
    [Permission(MyPromoteLevel.None)]
    public void VoteList() => Actions.VoteList();

    [Command("debug", "Shows the state of the voting module")]
    [Permission(MyPromoteLevel.Admin)]
    public void VoteDebug() => Actions.VoteDebug();

    [Command("reset", "Resets the voting module, including cooldowns")]
    [Permission(MyPromoteLevel.Admin)]
    public void VoteReset() => Actions.VoteReset();
}

[CommandRoot("yes", "Sens Essentials", "Vote yes")]
public sealed class YesCommand : EssentialsCommands
{
    [Command("", "Submit a yes vote")]
    [Permission(MyPromoteLevel.None)]
    public void VoteYes() => new VotingActions(Call).VoteYes();
}

[CommandRoot("no", "Sens Essentials", "Retract your vote")]
public sealed class NoCommand : EssentialsCommands
{
    [Command("", "Retract your yes vote")]
    [Permission(MyPromoteLevel.None)]
    public void VoteNo() => new VotingActions(Call).VoteNo();
}

[CommandRoot("identity", "Sens Essentials", "Reset or remove inactive identities")]
public sealed class IdentityCommands : EssentialsCommands
{
    private WorldActions Actions => new(Call);

    [Command("clean", "Resets identities that have not logged in for X days to a brand-new state. The identities are kept. Usage: clean <days> [includeNpcs]")]
    [Permission(MyPromoteLevel.Admin)]
    public void CleanIdentities(int days, bool includeNpcs = false) => Actions.CleanIdentities(days, includeNpcs);

    [Command("purge", "Removes identities that have not logged in for X days, together with everything they own. Usage: purge <days> [includeNpcs]")]
    [Permission(MyPromoteLevel.Admin)]
    public void PurgeIdentities(int days, bool includeNpcs = false) => Actions.PurgeIdentities(days, includeNpcs);

    [Command("clear", "Resets one player's identity to a brand-new state. The identity is kept")]
    [Permission(MyPromoteLevel.Admin)]
    public void ClearIdentity(string playername) => Actions.ClearIdentity(playername);
}

[CommandRoot("rep", "Sens Essentials", "Reputation")]
public sealed class RepCommands : EssentialsCommands
{
    [Command("wipe", "Resets the reputation on the server")]
    [Permission(MyPromoteLevel.Admin)]
    public void WipeReputation(bool removePlayerToFaction = true, bool removeFactionToFaction = true) =>
        new WorldActions(Call).WipeReputation(removePlayerToFaction, removeFactionToFaction);
}

[CommandRoot("faction", "Sens Essentials", "Faction maintenance")]
public sealed class FactionCommands : EssentialsCommands
{
    private WorldActions Actions => new(Call);

    [Command("clean", "Removes factions with fewer than the given number of players")]
    [Permission(MyPromoteLevel.Admin)]
    public void CleanFactions(int memberCount = 1) => Actions.CleanFactions(memberCount);

    [Command("remove", "Removes a faction by tag")]
    [Permission(MyPromoteLevel.Admin)]
    public void RemoveFaction(string tag) => Actions.RemoveFaction(tag);

    [Command("info", "Lists the members of every faction")]
    [Permission(MyPromoteLevel.Admin)]
    public void FactionInfo() => Actions.FactionInfo();
}

[CommandRoot("sandbox", "Sens Essentials", "Sandbox file maintenance")]
public sealed class SandboxCommands : EssentialsCommands
{
    [Command("clean", "Removes offline identities that own no blocks and other junk data. Deleted asteroids generate again.")]
    [Permission(MyPromoteLevel.Admin)]
    public void CleanSandbox() => new WorldActions(Call).CleanSandbox();
}
