using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;

namespace SensEssentials.Commands;

/// <summary>
/// Votes on auto commands with the Vote trigger: !vote, !yes, !no and the admin vote commands.
/// </summary>
public sealed class VotingActions(ICommandContext context) : CommandActions(context)
{
    public enum Status
    {
        VoteStandby,
        VoteInProgress,
        VoteCancel,

        // Last vote
        VoteFail,
        VoteSuccess
    }

    private static AutoCommand? _command;
    private static string? _voteInProgress;
    private static readonly Dictionary<ulong, DateTime> VoteReg = [];
    private static readonly Dictionary<ulong, DateTime> VoteCooldown = [];
    public static Status VoteStatus = Status.VoteStandby;

    //last vote info for debugging
    private static string? _lastVoteName;
    private static Status _voteResult;
    private static double _voteResultPercentage;

    public void Vote(string name)
    {
        if (Context.Player == null)
        {
            Context.Respond("This is an in-game command");
            return;
        }

        if (VoteStatus == Status.VoteInProgress)
        {
            Context.Respond($"vote for {_voteInProgress} is currently active. Use !yes to vote and !no to retract vote");
            return;
        }

        _command = EssentialsCore.Config.AutoCommands.FirstOrDefault(c => !string.IsNullOrEmpty(c.Name) 
                                                                                     && c.CommandTrigger == Trigger.Vote && c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (_command == null)
        {
            Context.Respond($"Couldn't find any votable command with the name {name}");
            return;
        }

        var steamid = Context.Player.SteamUserId;
        if (VoteCooldown.TryGetValue(steamid, out var activeCooldown))
        {
            var difference = activeCooldown - DateTime.Now;
            if (difference.TotalSeconds > 0)
            {
                Context.Respond($"Cooldown active. You can use this command again in {difference.Minutes:N0} minutes : {difference.Seconds:N0} seconds");
                return;
            }

            VoteCooldown[steamid] = DateTime.Now.AddSeconds(_command.TriggerCount);
        }
        else
            VoteCooldown.Add(steamid, DateTime.Now.AddSeconds(_command.TriggerCount));

        var voteDuration = TimeSpan.Parse(_command.Interval);
        // voting status
        _voteInProgress = name;
        VoteStatus = Status.VoteInProgress;
        _secondsLeft = (int)voteDuration.TotalSeconds;
        int voteId = Interlocked.Increment(ref _nextVoteId);
        Volatile.Write(ref _activeVoteId, voteId);
        VoteYes();
        var sb = new StringBuilder();
        sb.AppendLine($"Voting started for {name} by {Context.Player.DisplayName}.");
        sb.AppendLine("Use !yes to vote and !no to retract your vote");
        Notify.All(sb.ToString(), 15000);

        // The background task only waits. Each tick runs on the game thread, where !yes and !no also run,
        // so the vote state is never touched from two threads.
        Task.Run(async () =>
        {
            try
            {
                while (Volatile.Read(ref _activeVoteId) == voteId)
                {
                    await Task.Delay(1000);
                    MySandboxGame.Static.Invoke(() =>
                    {
                        try
                        {
                            CountdownTick(voteId);
                        }
                        catch (Exception e)
                        {
                            // End the vote, otherwise the same error repeats every second.
                            Log.Error(e, $"Vote for {name} failed");
                            VoteEnd();
                        }
                    }, "SensEssentials Vote");
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Vote countdown for {name} stopped");
            }
        });

        _lastVoteName = _voteInProgress;
    }

    public void VoteCancel()
    {
        if (VoteStatus == Status.VoteInProgress)
            VoteStatus = Status.VoteCancel;
        else
            Context.Respond("A vote is not in progress");
    }

    public void VoteList()
    {
        StringBuilder sb = new StringBuilder();
        var voteCommands = new List<AutoCommand>(EssentialsCore.Config.AutoCommands.Where(x=>x.CommandTrigger == Trigger.Vote));
        if (Context.Player == null)sb.AppendLine($"Found {voteCommands.Count}");
        var c = 1;
        foreach (var command in voteCommands)
        {
            if (string.IsNullOrEmpty(command.Name)) continue;
            sb.AppendLine($"{c}. {command.Name}");
            c++;
        }

        Show("Vote Commands", $"Found {voteCommands.Count} vote commands", sb.ToString());
    }

    public void VoteNo()
    {
        if (Context.Player == null)
            return;

        if (VoteStatus == Status.VoteStandby)
        {
            Context.Respond("no vote in progress");
            return;
        }

        var steamid = Context.Player.SteamUserId;

        VoteReg.Remove(steamid);
        Context.Respond("your vote has been retracted");
    }

    public void VoteYes()
    {
        if (Context.Player == null)
            return;
            
        if (_command == null)
            return;

        if (VoteStatus == Status.VoteStandby)
        {
            Context.Respond("no vote in progress");
            return;
        }

        var steamid = Context.Player.SteamUserId;
        if (VoteReg.TryGetValue(steamid, out DateTime lastcommand))
        {
            TimeSpan difference = DateTime.Now - lastcommand;
            TimeSpan voteDuration = TimeSpan.Parse(_command.Interval);
            if (difference.TotalSeconds < voteDuration.TotalSeconds)
            {
                Context.Respond("Your vote has already been submitted.");
                return;
            }

            VoteReg[steamid] = DateTime.Now;
        }
        else
        {
            VoteReg.Add(steamid, DateTime.Now);
        }

        Context.Respond("Your vote has been submitted.");
    }

    public void VoteDebug()
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"Current Vote Status: {VoteStatus.ToString()}");
        sb.AppendLine($"Current Vote Name: {_voteInProgress}");
        sb.AppendLine($"Current vote count: {VoteReg.Count}");
        sb.AppendLine();
        sb.AppendLine("Last vote info");
        if (_lastVoteName != null)
            sb.AppendLine($"Last vote: {_lastVoteName}");
        sb.AppendLine($"Last Vote Result: {_voteResult.ToString()}");
        sb.AppendLine($"Last vote percent: {_voteResultPercentage}");
        Show("Vote Debug", null, sb.ToString());
    }

    public void VoteReset()
    {
        if (VoteStatus == Status.VoteInProgress) VoteCancel();
        VoteReg.Clear();
        VoteCooldown.Clear();
        _lastVoteName = null;
        _voteResult = Status.VoteStandby;
        _voteResultPercentage = 0;
        Context.Respond("Vote reset successful");
        Log.Info($"Voting module reset by {Context.Player?.DisplayName ?? "Console"}");
    }

    private static int _secondsLeft;
    private static int _nextVoteId;
    private static int _activeVoteId;

    // One second of the vote countdown. Runs on the game thread.
    private static void CountdownTick(int voteId)
    {
        if (voteId != _activeVoteId)
            return;

        if (VoteStatus != Status.VoteInProgress || VoteReg.Count < 1)
        {
            Announce($"Vote for {_voteInProgress} cancelled");
            _voteResult = Status.VoteCancel;
            VoteEnd();
            return;
        }

        int i = _secondsLeft--;

        if (i > 0)
        {
            if (i >= 60 && i % 60 == 0)
                Announce($"Voting for {_voteInProgress} ends in {i / 60} minute{Pluralize(i / 60)}.");
            else if (i < 11)
                Announce($"Voting for {_voteInProgress} ends in {i} second{Pluralize(i)}.");
            return;
        }

        int online = Utilities.GetOnlinePlayerCount();
        double vr = online == 0 ? 0 : (double)VoteReg.Count / online;
        if (_command != null && vr >= _command.TriggerRatio)
        {
            Announce($"Vote for {_voteInProgress} is successful");
            _voteResult = Status.VoteSuccess;
            _command.RunNow();
        }
        else
        {
            Announce($"Vote for {_voteInProgress} failed");
            _voteResult = Status.VoteFail;
        }

        _voteResultPercentage = vr * 100;
        VoteEnd();
    }

    public static void VoteEnd()
    {
        //Make sure it's all good for next round
        Volatile.Write(ref _activeVoteId, 0);
        _command = null;
        VoteStatus = Status.VoteStandby;
        _voteInProgress = null;
        VoteReg.Clear();
    }

    private static void Announce(string text) => EssentialsCore.Host.SendChat(null, text);

    private static string Pluralize(double num)
    {
        return num == 1 ? "" : "s";
    }
}