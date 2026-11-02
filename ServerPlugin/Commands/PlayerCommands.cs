using PluginSdk.Commands;
using SensEssentials.Commands;
using VRage.Game.ModAPI;

// Root level commands. Magnetar gives every command root its own module, so each is a default ("") command.
namespace SensEssentials.MagnetarHost.Commands
{
    [CommandRoot("say", "Sens Essentials", "Say a message as the server")]
    public sealed class SayCommand : EssentialsCommands
    {
        [Command("", "Say a message as the server. Usage: !say <message>")]
        [Permission(MyPromoteLevel.Admin)]
        public void Say(params string[] message) => new PlayerActions(Call).Say();
    }

    [CommandRoot("tp", "Sens Essentials", "Teleport an entity")]
    public sealed class TeleportCommand : EssentialsCommands
    {
        [Command("", "Teleport one entity to another. Grids move with everything attached by rotors, hinges and pistons. Usage: !tp <entity> <destination> [includeConnectorDocked]")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Teleport(string? entityToMove = null, string? destination = null, bool includeConnectorDocked = false) =>
            new PlayerActions(Call).Teleport(entityToMove, destination, includeConnectorDocked);
    }

    [CommandRoot("tpto", "Sens Essentials", "Teleport to an entity")]
    public sealed class TeleportToCommand : EssentialsCommands
    {
        [Command("", "Teleport directly to another entity. Usage: !tpto <destination> [entity] [includeConnectorDocked]")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void TeleportTo(string destination, string? entityToMove = null, bool includeConnectorDocked = false) =>
            new PlayerActions(Call).TeleportTo(destination, entityToMove, includeConnectorDocked);
    }

    [CommandRoot("tphere", "Sens Essentials", "Teleport an entity to you")]
    public sealed class TeleportHereCommand : EssentialsCommands
    {
        [Command("", "Teleport another entity directly to you. Usage: !tphere <entity> [destination] [includeConnectorDocked]")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void TeleportHere(string entityToMove, string? destination = null, bool includeConnectorDocked = false) =>
            new PlayerActions(Call).TeleportHere(entityToMove, destination, includeConnectorDocked);
    }

    [CommandRoot("w", "Sens Essentials", "Private message")]
    public sealed class WhisperCommand : EssentialsCommands
    {
        [Command("", "Send a private message to another player. Usage: !w <player> <message>")]
        [Permission(MyPromoteLevel.None)]
        public void Whisper(string playerName, params string[] message) => new PlayerActions(Call).Whisper(playerName);
    }

    [CommandRoot("kick", "Sens Essentials", "Kick a player")]
    public sealed class KickCommand : EssentialsCommands
    {
        [Command("", "Kick a player from the game")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Kick(string playerName) => new PlayerActions(Call).Kick(playerName);
    }

    [CommandRoot("ban", "Sens Essentials", "Ban a player")]
    public sealed class BanCommand : EssentialsCommands
    {
        [Command("", "Ban a player from the game by name or Steam ID")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Ban(string nameOrSteamId) => new PlayerActions(Call).Ban(nameOrSteamId);
    }

    [CommandRoot("unban", "Sens Essentials", "Unban a player")]
    public sealed class UnbanCommand : EssentialsCommands
    {
        [Command("", "Unban a player by name or Steam ID")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Unban(string nameOrSteamId) => new PlayerActions(Call).Unban(nameOrSteamId);
    }

    [CommandRoot("motd", "Sens Essentials", "Message of the day")]
    public sealed class MotdCommand : EssentialsCommands
    {
        [Command("", "Show the server's Message of the Day")]
        [Permission(MyPromoteLevel.None)]
        public void Motd() => new PlayerActions(Call).ShowMotd();
    }

    [CommandRoot("info", "Sens Essentials", "Server information")]
    public sealed class InfoCommands : EssentialsCommands
    {
        [Command("list", "Lists all available info commands")]
        [Permission(MyPromoteLevel.None)]
        public void List() => new InfoActions(Call).List();

        [Command("", "Shows an info command. Usage: !info <name>")]
        [Permission(MyPromoteLevel.None)]
        public void Show(string name) => new InfoActions(Call).Show(name);
    }

    [CommandRoot("economy", "Sens Essentials", "Player credits")]
    public sealed class EcoCommands : EssentialsCommands
    {
        private EcoActions Actions => new(Call);

        [Command("give", "Add credits to a player's account. Use * for every player")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoGive(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) => Actions.EcoGive(player, amount, onlyOnline, excludeNpcs);

        [Command("take", "Take credits from a player's account. Use * for every player")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoTake(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) => Actions.EcoTake(player, amount, onlyOnline, excludeNpcs);

        [Command("set", "Set a player's balance. Use * for every player")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoSet(string player, long amount, bool onlyOnline = false, bool excludeNpcs = true) => Actions.EcoSet(player, amount, onlyOnline, excludeNpcs);

        [Command("reset", "Reset a player's balance to 10,000. Use * for every player")]
        [Permission(MyPromoteLevel.Admin)]
        public void EcoReset(string player, bool onlyOnline = false, bool excludeNpcs = true) => Actions.EcoReset(player, onlyOnline, excludeNpcs);

        [Command("top", "List every player's balance from highest to lowest")]
        [Permission(MyPromoteLevel.None)]
        public void EcoTop(bool onlyOnline = false, bool excludeNpcs = true) => Actions.EcoTop(onlyOnline, excludeNpcs);

        [Command("check", "Check the balance of a player")]
        [Permission(MyPromoteLevel.None)]
        public void EcoCheck(string player) => Actions.EcoCheck(player);

        [Command("pay", "Pay credits to an online player")]
        [Permission(MyPromoteLevel.None)]
        public void EcoPay(string player, long amount) => Actions.EcoPay(player, amount);
    }
}
