using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.World;
using VRage.Game.ModAPI;
using VRageRender.Utils;

namespace SensEssentials.Commands;

/// <summary>
/// The !admin commands. Mute, unmute and list mute need Torch's chat manager and live in the Torch host.
/// </summary>
public sealed class AdminActions(ICommandContext context) : CommandActions(context)
{
    public void Statistics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Generic:");
        Stats.Generic.WriteTo(sb);
        sb.AppendLine("Network:");
        Stats.Network.WriteTo(sb);
        sb.AppendLine("Timing:");
        Stats.Timing.WriteTo(sb);

        Show("Statistics", null, sb.ToString());
    }

    public void PlayerCount(int count)
    {
        if (count != -1)
            MyMultiplayer.Static.MemberLimit = count;

        Context.Respond($"Max player count: {MyMultiplayer.Static.MemberLimit}. Current online players: {MyMultiplayer.Static.MemberCount - 1}");
    }

    public void ListPlayers()
    {
        var players = MySession.Static.Players.GetOnlinePlayers();
        if (players.Count == 0)
        {
            Context.Respond("No players online");
            return;
        }

        StringBuilder sb = new();
        sb.AppendLine($"Found {players.Count} Players on server");
        foreach (var player in players)
        {
            sb.AppendLine($"{player.DisplayName}");
            sb.AppendLine($">PlayerId: {player.Identity?.IdentityId}");
            sb.AppendLine($">SteamId: {player.Id.SteamId}");
        }

        Show("List of Online Players", $"{players.Count} players Online", sb.ToString());
    }

    public void RunAuto(string name)
    {
        var command = FindAutoCommand(name);
        if (command == null)
        {
            Context.Respond($"Couldn't find an auto command with the commandName {name}");
            return;
        }
        Context.Respond($"Running the autocommand {command.Name}");
        command.RunNow();
    }

    private static AutoCommand? FindAutoCommand(string name)
    {
        return EssentialsCore.Config.AutoCommands.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public void RunPreset(string name)
    {
        var preset = EssentialsCore.Config.BlockLists.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
        if (preset == null)
        {
            Context.Respond($"Couldn't find a block preset with the name '{name}'");
            return;
        }

        if (preset.Problem is { } problem)
        {
            Context.Respond($"Block preset '{preset.Name}' was not run: {problem}");
            return;
        }

        var action = preset.Action switch
        {
            BlockAction.TurnOn => "on",
            BlockAction.TurnOff => "off",
            BlockAction.Remove => "remove",
            _ => "off"
        };

        var items = preset.Items.ToList();
        Context.Respond($"Running block preset '{name}' ({items.Count} items)...");

        RunInOrder($"Block preset '{name}'", [.. items
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => (preset.GetCommand(action, item), item.DelaySpan))]);
    }

    /// <summary>
    /// Runs commands one at a time on the game thread, waiting in the background in between.
    /// </summary>
    private static void RunInOrder(string source, List<(string Command, TimeSpan Delay)> steps)
    {
        Task.Run(async () =>
        {
            try
            {
                foreach (var (command, delay) in steps)
                {
                    GameThread.InvokeSafe(() => EssentialsCore.RunCommand(command, source));
                    await Task.Delay(delay);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"{source} failed");
            }
        });
    }

    public void RunCleanup(string name)
    {
        var preset = EssentialsCore.Config.CleanupPresets.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (preset == null)
        {
            Context.Respond($"Couldn't find a cleanup preset with the name '{name}'");
            return;
        }

        if (preset.Problem is { } problem)
        {
            Context.Respond($"Cleanup preset '{preset.Name}' was not run: {problem}");
            return;
        }

        if (preset.ApplyToAll || preset.Action == CleanupAction.DeleteFloatingObjects)
        {
            var commands = preset.GetCommands();
            foreach (var command in commands)
                EssentialsCore.RunCommand(command, $"Cleanup preset '{name}'");
            Context.Respond($"Ran cleanup preset '{name}' ({commands.Count} commands).");
        }
        else
        {
            var targets = preset.Targets.ToList();
            Context.Respond($"Running cleanup preset '{name}' ({targets.Count} targets)...");
            RunInOrder($"Cleanup preset '{name}'", [.. targets.Select(target => (preset.BuildCommand(target), target.DelaySpan))]);
        }
    }

    public void EndAuto(string commandName)
    {
        var command = FindAutoCommand(commandName);
        if (command == null)
        {
            Context.Respond($"Couldn't find an auto command with the commandName {commandName}");
            return;
        }
        Context.Respond($"AutoCommand {command.Name} cancelled");
        command.Cancel();
    }

    public void EndByOrder(int commandNumber)
    {
        var commands = new List<AutoCommand>(EssentialsCore.Config.AutoCommands.Where(x => x.IsRunning()));

        if (commands.Count == 0)
        {
            Context.Respond("No active autocommand found");
            return;
        }

        if (commandNumber < 1 || commandNumber > commands.Count)
        {
            Context.Respond($"{commandNumber} number is out of range");
            return;
        }

        var command = commands[commandNumber - 1];
        Context.Respond($"AutoCommand {command.Name} cancelled");
        command.Cancel();
    }

    public void ListAuto()
    {
        ShowAutoCommands([.. EssentialsCore.Config.AutoCommands], "No auto commands configured");
    }

    public void ListAutoRunning()
    {
        ShowAutoCommands([.. EssentialsCore.Config.AutoCommands.Where(x => x.IsRunning())], "No active autocommand found");
    }

    private void ShowAutoCommands(List<AutoCommand> commands, string empty)
    {
        if (commands.Count == 0)
        {
            Context.Respond(empty);
            return;
        }

        var sb = new StringBuilder();
        int count = 1;
        foreach (var command in commands)
            sb.AppendLine((count++) + " " + command.Name);

        Show("Current AutoCommands", $"Found {commands.Count} Commands", sb.ToString());
    }

    /// <summary>
    /// The Steam id of an online or offline player, or the id itself when it was given. 0 when neither fits.
    /// </summary>
    private static ulong ResolveSteamId(string nameOrId)
    {
        if (Utilities.GetPlayerByNameOrId(nameOrId) is { } online)
            return online.SteamUserId;

        if (Utilities.GetIdentityByNameOrIds(nameOrId) is { } identity && Utilities.GetSteamId(identity.IdentityId) is var steamId and not 0)
            return steamId;

        return ulong.TryParse(nameOrId, out var id) ? id : 0;
    }

    public void SetRank(string playerNameOrId, string rank)
    {
        var id = ResolveSteamId(playerNameOrId);

        if (id == 0)
        {
            Context.Respond($"Player '{playerNameOrId}' not found or ID is invalid.");
            return;
        }

        // Only rank names, and nothing above Admin: TryParse alone also accepts numbers such as -3.
        if (!Enum.TryParse<MyPromoteLevel>(rank, true, out var promoteLevel) || !Enum.IsDefined(typeof(MyPromoteLevel), promoteLevel)
            || int.TryParse(rank, out _) || promoteLevel > MyPromoteLevel.Admin)
        {
            Context.Respond($"Invalid rank '{rank}'. Use None, Scripter, Moderator, SpaceMaster or Admin.");
            return;
        }

        // Owners are set in the server config; nobody demotes them with a command.
        if (MySession.Static.GetUserPromoteLevel(id) > MyPromoteLevel.Admin)
        {
            Context.Respond("The rank of an Owner cannot be changed with this command.");
            return;
        }

        MySession.Static.SetUserPromoteLevel(id, promoteLevel);
        Context.Respond($"Player '{playerNameOrId}' promoted to '{promoteLevel}'.");
    }

    public void ReserveSlot(string playerNameOrId)
    {
        var id = ResolveSteamId(playerNameOrId);

        if (id == 0)
        {
            Context.Respond($"Player '{playerNameOrId}' not found or ID is invalid.");
            return;
        }

        if (MySandboxGame.ConfigDedicated.Reserved.Contains(id))
        {
            Context.Respond($"ID {id} is already reserved.");
            return;
        }

        MySandboxGame.ConfigDedicated.Reserved.Add(id);
        MySandboxGame.ConfigDedicated.Save();
        Context.Respond($"ID {id} added to reserved slots.");
    }

    public void UnreserveSlot(string playerNameOrId)
    {
        var id = ResolveSteamId(playerNameOrId);

        if (id == 0)
        {
            Context.Respond($"Player '{playerNameOrId}' not found or ID is invalid.");
            return;
        }

        if (!MySandboxGame.ConfigDedicated.Reserved.Contains(id))
        {
            Context.Respond($"ID {id} is already unreserved.");
            return;
        }

        MySandboxGame.ConfigDedicated.Reserved.Remove(id);
        MySandboxGame.ConfigDedicated.Save();
        Context.Respond($"ID {id} removed from reserved slots.");
    }

    public void Refill(string player, int minutes)
    {
        Context.Respond(RefillPlayer(player, minutes));
    }

    public static string RefillPlayer(string player, int minutes)
    {
        var online = Utilities.GetPlayerByNameOrId(player);
        if (online != null)
            return SuitRefill.SetTimedRefill(online.SteamUserId, online.DisplayName, minutes);

        if (ulong.TryParse(player, out var steamId) && steamId != 0)
            return SuitRefill.SetTimedRefill(steamId, steamId.ToString(), minutes);

        return $"Player '{player}' not found. Use their name while they are online, or their Steam ID.";
    }

    public void Give(string playerName, string itemType, string item, int quantity)
    {
        if (quantity <= 0)
        {
            Context.Respond("Quantity must be greater than 0");
            return;
        }

        string type = "MyObjectBuilder_" + Utilities.NormalizeTypeName(itemType);
        if (!VRage.Game.MyDefinitionId.TryParse(type, item, out VRage.Game.MyDefinitionId defID)
            || !Sandbox.Definitions.MyDefinitionManager.Static.TryGetPhysicalItemDefinition(defID, out _))
        {
            Context.Respond("Invalid item type");
            return;
        }

        var message = $"You have been given {quantity} {item} {itemType}";
        if (playerName != "*")
        {
            var p = Utilities.GetPlayerByNameOrId(playerName);
            if (p == null)
            {
                Context.Respond("Player not found");
                return;
            }

            if (p.Character == null)
            {
                Context.Respond($"{p.DisplayName} has no character to give items to.");
                return;
            }

            Sandbox.Game.MyVisualScriptLogicProvider.AddToPlayersInventory(p.IdentityId, defID, quantity);
            Notify.Player(p.SteamUserId, message);
            Context.Respond("Item(s) given!");
        }
        else
        {
            int given = 0;
            foreach (var p in Utilities.GetOnlinePlayers())
            {
                // Players without a character have no inventory, and the game skips them silently.
                if (p.Identity?.Character == null)
                    continue;

                Sandbox.Game.MyVisualScriptLogicProvider.AddToPlayersInventory(p.Identity.IdentityId, defID, quantity);
                Notify.Player(p.Id.SteamId, message);
                given++;
            }
            Context.Respond($"Item(s) given to {given} player(s).");
        }
    }
}
