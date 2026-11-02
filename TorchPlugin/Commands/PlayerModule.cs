using SensEssentials.Commands;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace SensEssentials.TorchHost.Commands;

/// <summary>
/// Root level commands, so no category: !say, !tp, !kick and so on.
/// </summary>
public class PlayerModule : EssentialsModule
{
    private PlayerActions Actions => new(Call);

    [Command("say", "Say a message as the server.")]
    public void Say(string message) => Actions.Say();

    [Command("tp", "Teleport one entity to another. Grids move together with everything attached by rotors, hinges and pistons. Usage: tp <entity> <destination> [includeConnectorDocked]")]
    [Permission(MyPromoteLevel.SpaceMaster)]
    public void Teleport(string? entityToMove, string? destination, bool includeConnectorDocked = false) => Actions.Teleport(entityToMove, destination, includeConnectorDocked);

    [Command("tpto", "Teleport directly to an another entity. Usage: tpto <destination> [entity] [includeConnectorDocked]")]
    [Permission(MyPromoteLevel.SpaceMaster)]
    public void TeleportTo(string destination, string? entityToMove = null, bool includeConnectorDocked = false) => Actions.TeleportTo(destination, entityToMove, includeConnectorDocked);

    [Command("tphere", "Teleport another entity directly to you. Usage: tphere <entity> [destination] [includeConnectorDocked]")]
    [Permission(MyPromoteLevel.SpaceMaster)]
    public void TeleportHere(string entityToMove, string? destination = null, bool includeConnectorDocked = false) => Actions.TeleportHere(entityToMove, destination, includeConnectorDocked);

    [Command("w", "Send a private message to another player.")]
    [Permission(MyPromoteLevel.None)]
    public void Whisper(string playerName) => Actions.Whisper(playerName);

    [Command("kick", "Kick a player from the game.")]
    [Permission(MyPromoteLevel.Moderator)]
    public void Kick(string playerName) => Actions.Kick(playerName);

    [Command("ban", "Ban a player from the game.")]
    [Permission(MyPromoteLevel.Moderator)]
    public void Ban(string nameOrSteamId) => Actions.Ban(nameOrSteamId);

    [Command("unban", "Unban a player from the game.")]
    [Permission(MyPromoteLevel.Moderator)]
    public void Unban(string nameOrSteamId) => Actions.Unban(nameOrSteamId);

    [Command("motd", "Show the server's Message of the Day.")]
    [Permission(MyPromoteLevel.None)]
    public void Motd() => Actions.ShowMotd();
}
