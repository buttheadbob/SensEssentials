using SensEssentials.Commands;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace SensEssentials.TorchHost.Commands;

[Category("grids")]
public class GridModule : EssentialsModule
{
    private GridActions Actions => new(Call);

    [Command("setowner", "Sets grid ownership to the given player or ID.", "Usage: setowner <grid> <newowner>")]
    [Permission(MyPromoteLevel.SpaceMaster)]
    public void SetOwner(string gridName, string playerName) => Actions.SetOwner(gridName, playerName);

    [Command("ejectall", "Ejects all Players from given grid.")]
    [Permission(MyPromoteLevel.SpaceMaster)]
    public void Eject(string? gridName = null) => Actions.Eject(gridName);

    [Command("static large", "Makes all large grids static.")]
    [Permission(MyPromoteLevel.SpaceMaster)]
    public void StaticLarge() => Actions.StaticLarge();

    [Command("stopall", "Stops all moving grids.")]
    [Permission(MyPromoteLevel.SpaceMaster)]
    public void StopAll() => Actions.StopAll();

    [Command("list", "Lists all grids where you own the most blocks. Shows positions or GPS markers if the server admin enables the option.")]
    [Permission(MyPromoteLevel.None)]
    public void List() => Actions.List();

    [Command("export", "Export the given grid to the given file name.")]
    public void Export(string gridName, string exportName) => Actions.Export(gridName, exportName);

    [Command("import", "Import a grid from file and spawn it by the given entity/player.")]
    public void Import(string gridName, string? targetName = null) => Actions.Import(gridName, targetName);
}
