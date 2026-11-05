namespace SensEssentials;

// Config enums and their helpers. This file must stay free of game, Torch and PluginSdk references:
// the Quasar plugin (QuasarPlugin/SensEssentials.Quasar) compiles it too, so both sides agree on the names.

public enum Gtl
{
    LessThan,
    GreaterThan,
    Equal
}

public enum Trigger
{
    Disabled,
    GridCount,
    OnStart,
    PlayerCount,
    Scheduled,
    SimSpeed,
    Timed,
    Vote
}

public enum DayOfWeek
{
    All = -1,
    Sunday = System.DayOfWeek.Sunday,
    Monday = System.DayOfWeek.Monday,
    Tuesday = System.DayOfWeek.Tuesday,
    Wednesday = System.DayOfWeek.Wednesday,
    Thursday = System.DayOfWeek.Thursday,
    Friday = System.DayOfWeek.Friday,
    Saturday = System.DayOfWeek.Saturday
}

public enum BlockAction
{
    TurnOn,
    TurnOff,
    Remove
}

public enum SelectorType
{
    Type,
    Subtype
}

public enum ConditionType
{
    Name,
    BlocksLessThan,
    BlocksGreaterThan,
    PcuGreaterThan,
    PcuLessThan,
    HasGridType,
    HasOwnerType,
    HasPower,
    NoPower,
    InsidePlanet,
    PlayerDistanceLessThan,
    PlayerDistanceGreaterThan,
    PoweredGridDistanceGreaterThan,
    CenterDistanceLessThan,
    CenterDistanceGreaterThan,
    OwnedBy,
    HasType,
    NoType,
    HasTypeFast,
    NoTypeFast,
    HasSubtype,
    NoSubtype,
    HasSubtypeFast,
    NoSubtypeFast,
    HasPilot
}

public enum CleanupAction
{
    Delete,
    DeleteFloatingObjects
}

/// <summary>
/// Log levels in NLog's order, so (int)level equals NLog's LogLevel.Ordinal.
/// </summary>
public enum EssentialsLogLevel
{
    Trace,
    Debug,
    Info,
    Warn,
    Error,
    Fatal
}

public static class ConditionTypeExtensions
{
    public static string ToCommandString(this ConditionType condition)
    {
        return condition switch
        {
            ConditionType.HasTypeFast => "hastype-fast",
            ConditionType.NoTypeFast => "notype-fast",
            ConditionType.HasSubtypeFast => "hassubtype-fast",
            ConditionType.NoSubtypeFast => "nosubtype-fast",
            _ => condition.ToString().ToLowerInvariant()
        };
    }

    public static bool RequiresParameter(this ConditionType condition)
    {
        return condition switch
        {
            ConditionType.HasPower or ConditionType.NoPower or ConditionType.InsidePlanet or ConditionType.HasPilot => false,
            _ => true
        };
    }
}

/// <summary>
/// Warnings about conditions, shared with the Quasar plugin.
/// </summary>
public static class ConditionNotes
{
    public const string OwnedByNobody = "OwnedBy Nobody matches grids where no block has an owner. A delete or remove action on it "
        + "is permanent. Check this is what you want, or use HasOwnerType nobody, which goes by the majority owner.";

    public static bool IsOwnedByNobody(string condition, string? parameter) =>
        condition.Equals("ownedby", System.StringComparison.OrdinalIgnoreCase)
        && string.Equals(parameter?.Trim(), "nobody", System.StringComparison.OrdinalIgnoreCase);
}
