using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using NLog;
using Sandbox.Game.World;
using SensEssentials.Commands;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;

namespace SensEssentials.TorchHost.Commands;

[Category("admin")]
public class AdminModule : EssentialsModule
{
    private static readonly Logger Log = LogManager.GetLogger("SensEssentials");

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

    [Command("runauto", "Runs the auto command with the given commandName immediately")]
    [Permission(MyPromoteLevel.Admin)]
    public void RunAuto(string name) => Actions.RunAuto(name);

    [Command("runpreset", "Runs the custom block preset with the given preset name immediately")]
    [Permission(MyPromoteLevel.Admin)]
    public void RunPreset(string name) => Actions.RunPreset(name);

    [Command("runcleanup", "Runs the cleanup preset with the given preset name immediately")]
    [Permission(MyPromoteLevel.Admin)]
    public void RunCleanup(string name) => Actions.RunCleanup(name);

    [Command("cancelauto", "Cancels the auto command with the given commandName immediately")]
    [Permission(MyPromoteLevel.Admin)]
    public void EndAuto(string commandName) => Actions.EndAuto(commandName);

    [Command("cancelautobyindex", "Cancels the auto command with the given index immediately")]
    [Permission(MyPromoteLevel.Admin)]
    public void EndByOrder(int commandNumber = 0) => Actions.EndByOrder(commandNumber);

    [Command("listauto", "Lists all detected autocommands ")]
    [Permission(MyPromoteLevel.Admin)]
    public void ListAuto() => Actions.ListAuto();

    [Command("listrunningauto", "Lists all running autocommands ")]
    [Permission(MyPromoteLevel.Admin)]
    public void ListAutoRunning() => Actions.ListAutoRunning();

    [Command("setrank", "Set the promote level of a player.")]
    [Permission(MyPromoteLevel.Admin)]
    public void SetRank(string playerNameOrId, string rank) => Actions.SetRank(playerNameOrId, rank);

    [Command("reserve", "Add a player to the reserved slots list.")]
    [Permission(MyPromoteLevel.Admin)]
    public void ReserveSlot(string playerNameOrId) => Actions.ReserveSlot(playerNameOrId);

    [Command("unreserve", "Remove a player from the reserved slots list.")]
    [Permission(MyPromoteLevel.Admin)]
    public void UnreserveSlot(string playerNameOrId) => Actions.UnreserveSlot(playerNameOrId);

    [Command("refill", "Keeps a player's suit topped up for the given minutes (default 30). Use 0 minutes to stop. Usage: refill <player|steamId> [minutes]")]
    [Permission(MyPromoteLevel.Admin)]
    public void Refill(string player, int minutes = 30) => Actions.Refill(player, minutes);

    [Command("give", "Insert an item with a specific quanity into a players inventory")]
    [Permission(MyPromoteLevel.Admin)]
    public void Give(string playerName, string itemType, string item, int quantity) => Actions.Give(playerName, itemType, item, quantity);

    #region Mute (Torch only: needs Torch's chat manager)

    // Command modules are created per command, so mute state must be static.
    private static readonly Dictionary<ulong, DateTime> _muted = [];
    private static readonly object _muteLock = new();
    private static Timer? _muteTimer;
    private static IChatManagerServer? ChatManager => EssentialsPlugin.Instance.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>();

    [Command("mute", "Mutes a user in global chat for the given number of minutes.")]
    public void MuteUser(string user, int timeout = 0)
    {
        var p = Utilities.GetPlayerByNameOrId(user);
        if (p == null)
        {
            Context.Respond($"Could not find user {user}");
            return;
        }

        bool res = ChatManager?.MuteUser(p.SteamUserId) ?? false;

        if (!res)
            Context.Respond($"Failed to mute user {user}. They are already muted.");
        else
            Context.Respond(timeout > 0 ? $"Muted user {p.DisplayName} for {timeout} minute(s)" : $"Muted user {p.DisplayName}");

        if (timeout > 0)
        {
            lock (_muteLock)
            {
                _muted[p.SteamUserId] = DateTime.Now + TimeSpan.FromMinutes(timeout);
                if (_muteTimer == null)
                {
                    _muteTimer = new Timer(1000) { AutoReset = true };
                    _muteTimer.Elapsed += MuteTimerElapsed;
                    _muteTimer.Start();
                }
            }
        }
    }

    private static void MuteTimerElapsed(object sender, ElapsedEventArgs e)
    {
        List<ulong> expired;
        lock (_muteLock)
        {
            expired = _muted.Where(m => m.Value <= DateTime.Now).Select(m => m.Key).ToList();
            foreach (var id in expired)
                _muted.Remove(id);

            if (_muted.Count == 0 && _muteTimer != null)
            {
                _muteTimer.Dispose();
                _muteTimer = null;
            }
        }

        if (expired.Count == 0)
            return;

        // Chat manager state belongs to the game thread.
        try
        {
            GameThread.InvokeSafe(() =>
            {
                foreach (var id in expired)
                    ChatManager?.UnmuteUser(id);
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to unmute expired users");
        }
    }

    internal static void StopMuteTimer()
    {
        lock (_muteLock)
        {
            _muteTimer?.Dispose();
            _muteTimer = null;
            _muted.Clear();
        }
    }

    [Command("unmute", "Removes a chat mute from a user, if they have been muted.")]
    public void UnmuteUser(string user)
    {
        var p = Utilities.GetPlayerByNameOrId(user);
        if (p == null)
        {
            Context.Respond($"Could not find user {user}");
            return;
        }
        bool res = ChatManager?.UnmuteUser(p.SteamUserId) ?? false;
        lock (_muteLock)
            _muted.Remove(p.SteamUserId);

        if (!res)
            Context.Respond($"Failed to unmute user {user}. They are not muted.");
        else
            Context.Respond($"Unmuted user {p.DisplayName}");
    }

    [Command("list mute", "Lists all muted users, an their timeout, if applciable.")]
    public void ListMute()
    {
        var mutedUsers = ChatManager?.MutedUsers.ToList();
        if (mutedUsers == null || mutedUsers.Count == 0)
        {
            Context.Respond("No muted users.");
            return;
        }

        var sb = new StringBuilder();

        foreach (var m in mutedUsers)
        {
            var s = MySession.Static.Players.TryGetIdentityNameFromSteamId(m);
            if (string.IsNullOrEmpty(s))
                s = m.ToString();
            bool f;
            DateTime t;
            lock (_muteLock)
                f = _muted.TryGetValue(m, out t);

            sb.AppendLine($"{s}: {(f ? (t - DateTime.Now).ToString(@"hh\:mm\:ss") : "inf")}");
        }

        if (Context.SentBySelf)
        {
            Context.Respond(sb.ToString());
            return;
        }
        var ms = new DialogMessage("Muted Users", content: sb.ToString());
        ModCommunication.SendMessageTo(ms, Context.Player.SteamUserId);
    }

    #endregion
}
