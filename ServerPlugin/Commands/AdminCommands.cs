using PluginSdk.Commands;
using SensEssentials.Commands;
using VRage.Game.ModAPI;

namespace SensEssentials.MagnetarHost.Commands;

/// <summary>Mute, unmute and list mute need Torch's chat manager and are Torch only.</summary>
[CommandRoot("admin", "Sens Essentials", "Server administration")]
public sealed class AdminCommands : EssentialsCommands
{
    private AdminActions Actions => new(Call);

    [Command("stats", "Get performance statistics of the server")]
    [Permission(MyPromoteLevel.Admin)]
    public void Statistics() => Actions.Statistics();

    [Command("playercount", "Gets or sets the max number of players on the server")]
    [Permission(MyPromoteLevel.Admin)]
    public void PlayerCount(int count = -1) => Actions.PlayerCount(count);

    [Command("playerlist", "Lists current players on the server")]
    [Permission(MyPromoteLevel.Admin)]
    public void ListPlayers() => Actions.ListPlayers();

    [Command("leavesharedconfig", "Stops following a Quasar shared config. Quasar sends it again if the server is still assigned there.")]
    [Permission(MyPromoteLevel.Admin)]
    public void LeaveSharedConfig()
    {
        SharedConfigBridge.Plugin?.ReleaseSharedConfig();
        Call.Respond("This server no longer follows a shared config.");
    }

    [Command("runauto", "Runs the auto command with the given name immediately")]
    [Permission(MyPromoteLevel.Admin)]
    public void RunAuto(string name) => Actions.RunAuto(name);

    [Command("runpreset", "Runs the block preset with the given name immediately")]
    [Permission(MyPromoteLevel.Admin)]
    public void RunPreset(string name) => Actions.RunPreset(name);

    [Command("runcleanup", "Runs the cleanup preset with the given name immediately")]
    [Permission(MyPromoteLevel.Admin)]
    public void RunCleanup(string name) => Actions.RunCleanup(name);

    [Command("cancelauto", "Cancels the auto command with the given name")]
    [Permission(MyPromoteLevel.Admin)]
    public void EndAuto(string commandName) => Actions.EndAuto(commandName);

    [Command("cancelautobyindex", "Cancels the running auto command with the given number from listrunningauto")]
    [Permission(MyPromoteLevel.Admin)]
    public void EndByOrder(int commandNumber = 0) => Actions.EndByOrder(commandNumber);

    [Command("listauto", "Lists all auto commands")]
    [Permission(MyPromoteLevel.Admin)]
    public void ListAuto() => Actions.ListAuto();

    [Command("listrunningauto", "Lists all running auto commands")]
    [Permission(MyPromoteLevel.Admin)]
    public void ListAutoRunning() => Actions.ListAutoRunning();

    [Command("setrank", "Set the promote level of a player")]
    [Permission(MyPromoteLevel.Admin)]
    public void SetRank(string playerNameOrId, string rank) => Actions.SetRank(playerNameOrId, rank);

    [Command("reserve", "Add a player to the reserved slots list")]
    [Permission(MyPromoteLevel.Admin)]
    public void ReserveSlot(string playerNameOrId) => Actions.ReserveSlot(playerNameOrId);

    [Command("unreserve", "Remove a player from the reserved slots list")]
    [Permission(MyPromoteLevel.Admin)]
    public void UnreserveSlot(string playerNameOrId) => Actions.UnreserveSlot(playerNameOrId);

    [Command("refill", "Keeps a player's suit topped up for the given minutes (default 30). Use 0 minutes to stop. Usage: refill <player|steamId> [minutes]")]
    [Permission(MyPromoteLevel.Admin)]
    public void Refill(string player, int minutes = 30) => Actions.Refill(player, minutes);

    [Command("give", "Insert an item with a specific quantity into a player's inventory. Use * for every online player")]
    [Permission(MyPromoteLevel.Admin)]
    public void Give(string playerName, string itemType, string item, int quantity) => Actions.Give(playerName, itemType, item, quantity);
}
