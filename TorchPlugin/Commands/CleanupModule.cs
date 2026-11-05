using SensEssentials.Commands;
using Torch.Commands;

namespace SensEssentials.TorchHost.Commands;

[Category("cleanup")]
public class CleanupModule : EssentialsModule
{
    private CleanupActions Actions => new(Call);

    [Command("scan", "Find grids matching the given conditions")]
    public void Scan() => Actions.Scan();

    [Command("list", "Lists grids matching the given conditions")]
    public void List() => Actions.List();

    [Command("delete", "Delete grids matching the given conditions. 'all' deletes every grid without a pilot.")]
    public void Delete() => Actions.Delete();

    [Command("delete floatingobjects", "deletes floating objects")]
    public void FlObjDelete() => Actions.DeleteFloatingObjects();

    [Command("conditions", "Lists all cleanup conditions.")]
    public void Conditions() => Actions.Conditions();

    // Magnetar reserves "help", so the documented name is "conditions". This stays for existing Torch users.
    [Command("help", "Lists all cleanup conditions.")]
    public void Help() => Actions.Conditions();
}
