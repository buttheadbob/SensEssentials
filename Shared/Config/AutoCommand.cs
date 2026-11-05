using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SensEssentials;

public class AutoCommand : ObservableObject
{
    private TimeSpan _interval = TimeSpan.Zero;
    private DateTime _nextRun = DateTime.MinValue;
    private DayOfWeek _day = DayOfWeek.All;
    private Trigger _trigger = Trigger.Disabled;
    private Gtl _comparer = Gtl.LessThan;
    private int _currentStep;
    private string _name = "";
    private float _triggerRatio;
    private double _triggerCount;

    // Guards the schedule and run state, which is used by the timer tick, commands and background runs.
    private readonly object _sync = new();
    private readonly List<CancellationTokenSource> _runs = [];
    private string? _loggedProblem;
    private const int MaxRuns = 8;

    private TimeSpan _simSpeedDuration = TimeSpan.FromSeconds(5);
    private TimeSpan _simSpeedCooldown = TimeSpan.FromSeconds(30);
    private bool _hysteresisEnabled;
    private float _hysteresisRatio = 0.7f;

    public Trigger CommandTrigger
    {
        get => _trigger;
        set
        {
            SetValue(ref _trigger, value);
            ResetSchedule();
        }
    }

    public Gtl Compare
    {
        get => _comparer;
        set => SetValue(ref _comparer, value);
    }

    public string Name
    {
        get => _name;
        set => SetValue(ref _name, value);
    }

    public string Interval
    {
        get => TimeFormat.Format(_interval);
        set
        {
            if (!TimeFormat.TryParse(value, out var interval))
            {
                Log.Warn($"Auto command '{_name}' has an unreadable interval '{value}'. {TimeFormat.Help}");
                return;
            }

            _interval = interval;
            OnPropertyChanged();
            ResetSchedule();
        }
    }

    [XmlIgnore]
    public TimeSpan IntervalSpan => _interval;

    /// <summary>
    /// Why this command must not run on its own, or null when it can. An Interval of zero would repeat it every second.
    /// A step's Delay of zero is fine: that only means the next step follows straight away.
    /// </summary>
    public string? Problem => _trigger switch
    {
        Trigger.Timed or Trigger.GridCount or Trigger.PlayerCount when _interval < TimeSpan.FromSeconds(1)
            => "Interval must be at least 1 second.",
        Trigger.Scheduled when _interval >= TimeSpan.FromDays(1)
            => "Time of day must be below 24:00:00.",
        Trigger.SimSpeed when _comparer == Gtl.Equal
            => "Sim speed needs LessThan or GreaterThan.",
        _ => null
    };

    /// <summary>
    /// True when <see cref="Problem"/> stops this command. Logged once, since this is checked every second.
    /// </summary>
    internal bool IsBlocked()
    {
        var problem = Problem;
        if (problem != null && problem != _loggedProblem)
            Log.Warn($"Auto command '{_name}' will not run: {problem}");
        _loggedProblem = problem;
        return problem != null;
    }

    /// <summary>
    /// Restarts the schedule from now. Called whenever the trigger or interval changes, so a command switched
    /// on in the UI never inherits a stale next-run time.
    /// </summary>
    private void ResetSchedule()
    {
        lock (_sync)
        {
            _currentStep = 0;
            _nextRun = _trigger switch
            {
                Trigger.Timed => DateTime.UtcNow + _interval,
                Trigger.Scheduled => NextScheduledRun(),
                _ => DateTime.MinValue
            };
        }
    }

    private DateTime NextScheduledRun()
    {
        var now = DateTime.Now;
        var next = now.Date + _interval;
        return (next <= now ? next + TimeSpan.FromDays(1) : next).ToUniversalTime();
    }

    public float TriggerRatio
    {
        get => _triggerRatio;
        set => SetValue(ref _triggerRatio, Math.Min(Math.Max(value, 0), 1));
    }

    public double TriggerCount
    {
        get => _triggerCount;
        set => SetValue(ref _triggerCount, Math.Max(0, value));
    }

    [XmlIgnore]
    public TimeSpan SimSpeedDurationSpan => _simSpeedDuration;

    public string SimSpeedDuration
    {
        get => TimeFormat.Format(_simSpeedDuration);
        set
        {
            if (TimeFormat.TryParse(value, out var span))
                SetValue(ref _simSpeedDuration, span);
            else
                Log.Warn($"Auto command '{_name}' has an unreadable duration '{value}'. {TimeFormat.Help}");
        }
    }

    [XmlIgnore]
    public TimeSpan SimSpeedCooldownSpan => _simSpeedCooldown;

    public string SimSpeedCooldown
    {
        get => TimeFormat.Format(_simSpeedCooldown);
        set
        {
            if (TimeFormat.TryParse(value, out var span))
                SetValue(ref _simSpeedCooldown, span);
            else
                Log.Warn($"Auto command '{_name}' has an unreadable cooldown '{value}'. {TimeFormat.Help}");
        }
    }

    public bool HysteresisEnabled
    {
        get => _hysteresisEnabled;
        set => SetValue(ref _hysteresisEnabled, value);
    }

    public float HysteresisRatio
    {
        get => _hysteresisRatio;
        set => SetValue(ref _hysteresisRatio, Math.Min(Math.Max(value, 0), 1));
    }

    public DayOfWeek DayOfWeek
    {
        get => _day;
        set => SetValue(ref _day, value);
    }

    public ObservableCollection<CommandStep> Steps { get; } = [];

    public AutoCommand()
    {
        Steps.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
                foreach (CommandStep item in e.NewItems)
                    item.PropertyChanged += StepChanged;
            if (e.OldItems != null)
                foreach (CommandStep item in e.OldItems)
                    item.PropertyChanged -= StepChanged;
            OnPropertyChanged();
        };
    }

    private void StepChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged();

    protected override void OnPropertyChanged([CallerMemberName] string propName = "")
    {
        base.OnPropertyChanged(propName);
        if (propName != nameof(Problem))
            base.OnPropertyChanged(nameof(Problem));
    }

    /// <summary>
    /// True once per Interval: how often grid and player counts are checked.
    /// </summary>
    internal bool IsCheckDue()
    {
        lock (_sync)
        {
            if (DateTime.UtcNow < _nextRun)
                return false;

            _nextRun = DateTime.UtcNow + _interval;
            return true;
        }
    }

    /// <summary>
    /// Runs unless the previous run is still going, so a condition that stays true does not start a new copy every check.
    /// </summary>
    internal void RunIfIdle()
    {
        lock (_sync)
        {
            if (_runs.Count == 0)
                RunNow();
        }
    }

    public void Update()
    {
        lock (_sync)
        {
            if (DateTime.UtcNow < _nextRun)
                return;

            // Only before the first step: a run that started on the right day finishes past midnight.
            if (_trigger == Trigger.Scheduled && _currentStep == 0 && DayOfWeek != DayOfWeek.All && DateTime.Now.DayOfWeek != (System.DayOfWeek)(int)DayOfWeek)
            {
                _nextRun = NextScheduledRun();
                return;
            }

            var steps = Steps.ToList();
            if (steps.Count <= 0)
                return;

            if (_currentStep >= steps.Count)
                _currentStep = 0;

            var step = steps[_currentStep];

            step.RunStep(_name);
            _currentStep++;
            // Once the last step has run, the Interval or the schedule takes over from its delay.
            _nextRun = DateTime.UtcNow + step.DelaySpan;

            if (_currentStep < steps.Count) return;
            _currentStep = 0;
            _nextRun = _trigger == Trigger.Scheduled
                    ? NextScheduledRun()
                    : DateTime.UtcNow + _interval;
        }
    }

    public class CommandStep : ObservableObject
    {
        internal TimeSpan DelaySpan;
        private string _command = "";

        public string Delay
        {
            get => TimeFormat.Format(DelaySpan);
            set
            {
                if (TimeFormat.TryParse(value, out var span))
                    SetValue(ref DelaySpan, span);
                else
                    Log.Warn($"Auto command step '{_command}' has an unreadable delay '{value}'. {TimeFormat.Help}");
            }
        }

        public string Command
        {
            get => _command;
            set => SetValue(ref _command, value);
        }

        public void RunStep(string owner)
        {
            if (!EssentialsCore.Host.IsServerRunning)
                return;

            if (string.IsNullOrEmpty(Command))
                return;

            var command = Command;
            GameThread.InvokeSafe(() => EssentialsCore.RunCommand(command, $"Auto command '{owner}'"));
        }

        public override string ToString()
        {
            return Command;
        }
    }

    /// <summary>
    /// Runs all steps in the background. Each run has its own cancellation token, so several runs of the same
    /// command can exist (for example a manual !admin runauto) without interfering with each other.
    /// </summary>
    internal async void RunNow()
    {
        var cts = new CancellationTokenSource();
        lock (_sync)
        {
            // Commands can start each other, and a loop of them must not grow without end.
            if (_runs.Count >= MaxRuns)
            {
                Log.Warn($"Auto command '{_name}' already has {MaxRuns} runs going, so another was not started.");
                cts.Dispose();
                return;
            }

            _runs.Add(cts);
        }

        try
        {
            var steps = Steps.ToList();
            await Task.Run(() =>
            {
                foreach (var step in steps)
                {
                    if (cts.IsCancellationRequested)
                        break;

                    step.RunStep(_name);

                    // Wakes up early when cancelled.
                    cts.Token.WaitHandle.WaitOne(step.DelaySpan);
                }
            });
        }
        catch (Exception e)
        {
            Log.Error(e, $"Auto command '{_name}' failed");
        }
        finally
        {
            lock (_sync)
                _runs.Remove(cts);
            cts.Dispose();
        }
    }

    internal void Cancel()
    {
        Log.Info($"Cancelling autocommand {_name}");
        lock (_sync)
        {
            foreach (var run in _runs)
                run.Cancel();

            _currentStep = 0;
            _nextRun = _trigger == Trigger.Scheduled
                ? NextScheduledRun()
                : DateTime.UtcNow + _interval;
        }
    }

    public override string ToString()
    {
        return $"{Name} : {_trigger.ToString()} : {Steps.Count}";
    }

    internal bool IsRunning()
    {
        lock (_sync)
            return _currentStep > 0 || _runs.Count > 0;
    }
}
