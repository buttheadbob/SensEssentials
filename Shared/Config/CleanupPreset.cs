using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
namespace SensEssentials;

public class CleanupPreset : ObservableObject
{
    public string Name { get; set => SetValue(ref field, value); } = "";

    public CleanupAction Action { get; set => SetValue(ref field, value); }

    public bool ApplyToAll { get; set => SetValue(ref field, value); }

    public ObservableCollection<CleanupTarget> Targets { get; } = [];

    public ObservableCollection<CleanupCondition> Conditions { get; } = [];

    public CleanupPreset()
    {
        WatchSubCollection(Targets);
        WatchSubCollection(Conditions);
    }

    public override string ToString() => $"{Name} ({Action})";

    public List<string> GetCommands()
    {
        if (Action == CleanupAction.DeleteFloatingObjects)
            return ["!cleanup delete floatingobjects"];

        if (ApplyToAll)
            return [BuildCommand(null)];

        return [.. Targets.Select(t => BuildCommand(t))];
    }

    internal string BuildCommand(CleanupTarget? target)
    {
        var sb = new StringBuilder("!cleanup delete");
        if (target != null)
            AppendCondition(sb, target.Condition, target.Parameter);
        foreach (var c in Conditions)
            AppendCondition(sb, c.Condition, c.Parameter);
        return sb.ToString();
    }

    private static void AppendCondition(StringBuilder sb, ConditionType type, string? param)
    {
        sb.Append(' ');
        sb.Append(type.ToCommandString());
        if (!string.IsNullOrWhiteSpace(param))
        {
            sb.Append(' ');
            sb.Append(EssentialsCore.Host.QuoteArgument(param!));
        }
    }

    /// <summary>
    /// Problems that would stop this preset from running safely, or null when it is ready.
    /// </summary>
    public string? Problem
    {
        get
        {
            if (Action == CleanupAction.DeleteFloatingObjects)
                return null;
            if (!ApplyToAll && Targets.Count == 0)
                return "Add at least one target, or tick Apply To All.";
            if (ApplyToAll && Conditions.Count == 0)
                return "Apply To All needs at least one condition.";

            var all = Conditions.Select(c => (c.Condition, c.Parameter, Kind: "Condition"));
            if (!ApplyToAll)
                all = Targets.Select(t => (t.Condition, t.Parameter, Kind: "Target")).Concat(all);
            foreach (var (condition, parameter, kind) in all)
            {
                if (condition.RequiresParameter() && string.IsNullOrWhiteSpace(parameter))
                    return $"{kind} '{condition}' needs a Parameter.";
                if (!condition.RequiresParameter() && !string.IsNullOrWhiteSpace(parameter))
                    return $"{kind} '{condition}' does not take a Parameter.";
            }
            return null;
        }
    }

    protected override void OnPropertyChanged([CallerMemberName] string propName = "")
    {
        base.OnPropertyChanged(propName);
        if (propName is nameof(Problem) or nameof(Warning) or nameof(Attention))
            return;
        base.OnPropertyChanged(nameof(Problem));
        base.OnPropertyChanged(nameof(Warning));
        base.OnPropertyChanged(nameof(Attention));
    }

    /// <summary>
    /// Worth a second look, but it still runs.
    /// </summary>
    public string? Warning => Conditions.Any(c => ConditionNotes.IsOwnedByNobody(c.Condition.ToCommandString(), c.Parameter))
        || (!ApplyToAll && Targets.Any(t => ConditionNotes.IsOwnedByNobody(t.Condition.ToCommandString(), t.Parameter)))
        ? ConditionNotes.OwnedByNobody
        : null;

    /// <summary>
    /// What the list marker shows: a problem first, then a warning.
    /// </summary>
    public string? Attention => Problem ?? Warning;

    private void WatchSubCollection<T>(ObservableCollection<T> collection) where T : ObservableObject
    {
        collection.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
                foreach (T item in e.NewItems)
                    item.PropertyChanged += SubItemChanged;
            if (e.OldItems != null)
                foreach (T item in e.OldItems)
                    item.PropertyChanged -= SubItemChanged;
            OnPropertyChanged();
        };
    }

    private void SubItemChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged();
}

public class CleanupTarget : ObservableObject
{
    public ConditionType Condition { get; set => SetValue(ref field, value); }

    public string? Parameter { get; set => SetValue(ref field, value); }

    internal TimeSpan DelaySpan;

    public string Delay
    {
        get => TimeFormat.Format(DelaySpan);
        set
        {
            if (TimeFormat.TryParse(value, out var span))
                SetValue(ref DelaySpan, span);
            else
                Log.Warn($"Cleanup target '{Parameter}' has an unreadable delay '{value}'. {TimeFormat.Help}");
        }
    }

    public override string ToString() => string.IsNullOrWhiteSpace(Parameter) ? $"{Condition}" : $"{Condition}: {Parameter}";
}

public class CleanupCondition : ObservableObject
{
    public ConditionType Condition { get; set => SetValue(ref field, value); }

    public string? Parameter { get; set => SetValue(ref field, value); }

    public override string ToString() => string.IsNullOrWhiteSpace(Parameter) ? $"{Condition}" : $"{Condition}: {Parameter}";
}
