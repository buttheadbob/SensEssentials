using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;

namespace SensEssentials;

public class SimSpeedEvents() : GameThreadTimer("sim speed events")
{
    private static SimSpeedEvents? _instance;
    public static SimSpeedEvents Instance => _instance ??= new SimSpeedEvents();

    private readonly Dictionary<AutoCommand, State> _states = [];

    private class State
    {
        public DateTime ConditionMetAt = DateTime.MinValue;
        public DateTime LastFiredAt = DateTime.MinValue;
        public bool HysteresisCleared = true;
    }

    protected override void Tick()
    {
        var config = EssentialsCore.Config;
        if (config == null)
            return;

        if (MySession.Static?.Ready != true)
            return;

        var simSpeed = Math.Min(Sync.ServerSimulationRatio, 1);
        var now = DateTime.UtcNow;

        // Snapshot, since the UI can edit the list at any time.
        var commands = config.AutoCommands.Where(c => c.CommandTrigger == Trigger.SimSpeed).ToList();

        foreach (var cmd in commands)
        {
            if (cmd.IsBlocked() || cmd.Steps.Count == 0)
                continue;

            if (!_states.TryGetValue(cmd, out var state))
            {
                state = new State();
                _states[cmd] = state;
            }

            bool conditionMet = IsConditionMet(cmd, simSpeed);
            bool cooldownElapsed = (now - state.LastFiredAt) >= cmd.SimSpeedCooldownSpan;

            if (!conditionMet)
            {
                state.ConditionMetAt = DateTime.MinValue;

                // Recovery counts at any time after firing, including during the cooldown.
                // The cooldown only delays the next fire.
                if (cmd.HysteresisEnabled && !state.HysteresisCleared && IsRecovered(cmd, simSpeed))
                    state.HysteresisCleared = true;
            }
            else if (cmd.IsRunning())
            {
                state.ConditionMetAt = DateTime.MinValue;
            }
            else if (cmd.HysteresisEnabled && !state.HysteresisCleared)
            {
                // Still waiting for sim speed to recover past the recovery ratio.
                state.ConditionMetAt = DateTime.MinValue;
            }
            else if (cooldownElapsed)
            {
                if (state.ConditionMetAt == DateTime.MinValue)
                    state.ConditionMetAt = now;

                if ((now - state.ConditionMetAt) >= cmd.SimSpeedDurationSpan)
                {
                    cmd.RunNow();
                    state.LastFiredAt = now;
                    state.ConditionMetAt = DateTime.MinValue;
                    state.HysteresisCleared = !cmd.HysteresisEnabled;
                }
            }
        }

        // Forget state for events that were removed or changed to another trigger.
        var active = new HashSet<AutoCommand>(commands);
        foreach (var key in _states.Keys.Where(k => !active.Contains(k)).ToList())
            _states.Remove(key);
    }

    private static bool IsConditionMet(AutoCommand cmd, float simSpeed)
    {
        return cmd.Compare switch
        {
            Gtl.LessThan => simSpeed < cmd.TriggerRatio,
            Gtl.GreaterThan => simSpeed > cmd.TriggerRatio,
            _ => false
        };
    }

    private static bool IsRecovered(AutoCommand cmd, float simSpeed)
    {
        return cmd.Compare switch
        {
            Gtl.LessThan => simSpeed > cmd.HysteresisRatio,
            Gtl.GreaterThan => simSpeed < cmd.HysteresisRatio,
            _ => true
        };
    }

    public void Dispose()
    {
        Stop();
        _states.Clear();
    }
}
