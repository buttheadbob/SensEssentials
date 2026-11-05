using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.World;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using static SensEssentials.Gtl;


namespace SensEssentials;

public class AutoCommands() : GameThreadTimer("auto commands")
{
    private static AutoCommands? _instance;
    public static AutoCommands Instance => _instance ??= new AutoCommands();
    private bool _startCommandsDone;

    public override void Stop()
    {
        base.Stop();
        _startCommandsDone = false;
    }

    private bool CanRun(AutoCommand command)
    {
        if (MySession.Static?.Ready != true || command.IsBlocked()) return false;

        switch (command.CommandTrigger)
        {
            case Trigger.Disabled:
                return  false;
            case Trigger.OnStart:
                // Only the first tick of a session, so one added or renamed later waits for the next start.
                if (!_startCommandsDone)
                    command.RunNow();
                break;
            case Trigger.Vote:
                break;
            case Trigger.Timed:
                return true;
            case Trigger.Scheduled:
                return true;
            case Trigger.GridCount:
                if (command.IsCheckDue() && Matches(command, MyEntities.GetEntities().OfType<IMyCubeGrid>().Count()))
                    command.RunIfIdle();
                break;
            case Trigger.PlayerCount:
                if (command.IsCheckDue() && Matches(command, Utilities.GetOnlinePlayerCount()))
                    command.RunIfIdle();
                break;

            case Trigger.SimSpeed:
                return false;
                            
            default:
                return false;
        }

        return false;
    }


    private static bool Matches(AutoCommand command, double count) => command.Compare switch
    {
        GreaterThan => count > command.TriggerCount,
        LessThan => count < command.TriggerCount,
        Equal => Math.Abs(count - command.TriggerCount) < 1,
        _ => false,
    };

    protected override void Tick()
    {
        AutoCommand[] commands;
        try
        {
            // Snapshot, since the UI can edit the list at any time.
            commands = [.. EssentialsCore.Config.AutoCommands];
        }
        catch (Exception)
        {
            return;
        }

        foreach (var command in commands)
        {
            try
            {
                if(!CanRun(command))
                    continue;

                command.Update();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error encountered during autocommand update!");
            }
        }

        if (MySession.Static?.Ready == true)
            _startCommandsDone = true;
    }

}