using System;
using NLog;
using Torch.API.Managers;
using Torch.Commands;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace SensEssentials.TorchHost;

/// <summary>
/// Applies Command Permissions through Torch's command hook.
/// </summary>
public static class CommandPermissionHook
{
    private static readonly Logger Log = LogManager.GetLogger("SensEssentials");

    /// <summary>
    /// Hooked to <see cref="CommandManager.OnCommandExecuting"/>. Only called for commands typed in chat by players,
    /// so the server console is never affected.
    /// </summary>
    public static void OnCommandExecuting(Command command, IMyPlayer player, bool hasPermission, ref bool? hasPermissionOverride)
    {
        // Any throw here escapes into Torch's chat handling, so failures fall back to Torch's own permission check.
        try
        {
            var config = EssentialsPlugin.Instance.Config;
            if (config == null || !config.EnableCommandPermissions || player == null)
                return;

            var path = string.Join(" ", command.Path);
            switch (CommandPermissions.GetAccess(config, player.SteamUserId, path))
            {
                case RuleAccess.Allow:
                    hasPermissionOverride = true;
                    break;
                case RuleAccess.Deny:
                    hasPermissionOverride = false;
                    Log.Info($"{player.DisplayName} was denied command '{path}' by a command permission rule");
                    EssentialsPlugin.Instance.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?
                        .SendMessageAsOther("Server", "You do not have permission to use that command.", Color.Red, player.SteamUserId, MyFontEnum.Red);
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to evaluate command permissions");
        }
    }
}
