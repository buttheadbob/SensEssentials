using SensEssentials.Commands;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace SensEssentials.TorchHost.Commands;

/// <summary>
/// Root level vote commands, so no category: !vote, !yes, !no.
/// </summary>
public class VotingModule : EssentialsModule
{
    private VotingActions Actions => new(Call);

    [Command("vote", "starts a vote for a command")]
    [Permission(MyPromoteLevel.None)]
    public void Vote(string name) => Actions.Vote(name);

    [Command("vote cancel", "Cancels current vote in progress")]
    [Permission(MyPromoteLevel.Admin)]
    public void VoteCancel() => Actions.VoteCancel();

    [Command("vote list", "Lists all possible vote commands")]
    [Permission(MyPromoteLevel.None)]
    public void VoteList() => Actions.VoteList();

    [Command("no", "cancel your casted vote")]
    [Permission(MyPromoteLevel.None)]
    public void VoteNo() => Actions.VoteNo();

    [Command("yes", "Submit a yes vote")]
    [Permission(MyPromoteLevel.None)]
    public void VoteYes() => Actions.VoteYes();

    [Command("vote debug", "prints out info from the voting module")]
    [Permission(MyPromoteLevel.Admin)]
    public void VoteDebug() => Actions.VoteDebug();

    [Command("vote reset", "Resets the voting module data including cooldowns")]
    [Permission(MyPromoteLevel.Admin)]
    public void VoteReset() => Actions.VoteReset();
}
