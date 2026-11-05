using SensEssentials.Commands;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace SensEssentials.TorchHost.Commands;

[Category("economy")]
public class EcoModule : EssentialsModule
{
    private EcoActions Actions => new(Call);

    [Command("give", "Add a specified amount of credits into a users account. Use '*' to affect all players")]
    [Permission(MyPromoteLevel.Admin)]
    public void EcoGive(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) => Actions.EcoGive(player, amount, onlyOnline, excludeNpcs);

    [Command("take", "Take a specified amount of credits from a users account. Use '*' to affect all players")]
    [Permission(MyPromoteLevel.Admin)]
    public void EcoTake(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) => Actions.EcoTake(player, amount, onlyOnline, excludeNpcs);

    [Command("set", "Set a users account to a specified balance. Use '*' to affect all players")]
    [Permission(MyPromoteLevel.Admin)]
    public void EcoSet(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) => Actions.EcoSet(player, amount, onlyOnline, excludeNpcs);

    [Command("reset", "Reset the credits in a users account to 10,000. Use '*' to affect all players")]
    [Permission(MyPromoteLevel.Admin)]
    public void EcoReset(string player, bool onlyOnline = false, bool excludeNpcs = true) => Actions.EcoReset(player, onlyOnline, excludeNpcs);

    [Command("top", "Return a list of each players balance on the server sorted from highest to lowest")]
    [Permission(MyPromoteLevel.None)]
    public void EcoTop(bool onlyOnline = false, bool excludeNpcs = true) => Actions.EcoTop(onlyOnline, excludeNpcs);

    [Command("check", "Check the balance of a specific player")]
    [Permission(MyPromoteLevel.None)]
    public void EcoCheck(string player) => Actions.EcoCheck(player);

    [Command("pay", "Pay credits to an online player")]
    [Permission(MyPromoteLevel.None)]
    public void EcoPay(string player, long amount) => Actions.EcoPay(player, amount);
}
