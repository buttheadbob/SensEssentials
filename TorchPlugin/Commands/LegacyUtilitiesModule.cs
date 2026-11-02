using SensEssentials.Commands;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace SensEssentials.TorchHost.Commands;

/// <summary>
/// Command names from the Senxs Utilities plugin, kept for one version so existing scripts keep working.
/// </summary>
[Category("sutils")]
public class LegacyUtilitiesModule : CommandModule
{
    [Command("refillSuit", "Old name for !admin refill, which also accepts player names. Will be removed in a later version.")]
    [Permission(MyPromoteLevel.Admin)]
    public void RefillSuit(ulong steamId, int minutes = 30)
    {
        Context.Respond(AdminActions.RefillPlayer(steamId.ToString(), minutes) + " (!sutils refillSuit is now !admin refill)");
    }
}
