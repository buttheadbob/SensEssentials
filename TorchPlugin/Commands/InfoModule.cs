using SensEssentials.Commands;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace SensEssentials.TorchHost.Commands;

[Category("info")]
public class InfoModule : EssentialsModule
{
    /// <summary>
    /// Hooks info commands into Torch's chat. Called once per session.
    /// </summary>
    public static void Init()
    {
        IChatManagerServer? c = EssentialsPlugin.Instance.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>();
        c?.MessageProcessing += MessageProcessing;
    }

    // Players use this to find the info commands, as on Magnetar. Without the attribute Torch would require Admin.
    [Command("list", "Lists all available info commands.")]
    [Permission(MyPromoteLevel.None)]
    public void List() => new InfoActions(Call).List();

    private static void MessageProcessing(TorchChatMessage msg, ref bool consumed)
    {
        if (msg.AuthorSteamId != null && InfoActions.TryHandle(msg.AuthorSteamId.Value, msg.Message))
            consumed = true;
    }
}
