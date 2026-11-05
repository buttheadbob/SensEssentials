using System.Text.Json;
using System.Text.Json.Serialization;

namespace SensEssentials.Quasar.Model;

// The Sens Essentials Magnetar config (ServerPlugin\MagnetarConfig.cs) as the PluginSdk values JSON the server reads.
// Property names, types and defaults must match MagnetarConfig and its entry structs; the enums are the shared ones.

public sealed class SharedConfigValues
{
    // General
    public bool StopShipsOnStart { get; set; }
    public bool UtilityShowPosition { get; set; }
    public bool MarkerShowPosition { get; set; }
    public int BackpackLimit { get; set; } = 1;

    // MOTD
    public string Motd { get; set; } = "";
    public string NewUserMotd { get; set; } = "";
    public string MotdUrl { get; set; } = "";
    public bool NewUserMotdUrl { get; set; }

    public List<AutoCommandValues> AutoCommands { get; set; } = [];
    public List<InfoCommandValues> InfoCommands { get; set; } = [];
    public List<BlockPresetValues> BlockPresets { get; set; } = [];
    public List<CleanupPresetValues> CleanupPresets { get; set; } = [];

    // Discord Logging
    public bool DiscordLoggingEnabled { get; set; }
    public int DiscordRateLimitPerSecond { get; set; } = 3;
    public int DiscordStackTraceEmbedLimit { get; set; } = 2000;
    public List<DiscordHandlerValues> DiscordLogHandlers { get; set; } = [];

    // Admin Tools
    public bool SuitRefillEnabled { get; set; }
    public bool SuitRefillAdmins { get; set; } = true;
    public bool SuitRefillEveryone { get; set; }
    public bool SuitRefillOxygen { get; set; } = true;
    public bool SuitRefillHydrogen { get; set; } = true;
    public bool SuitRefillEnergy { get; set; } = true;
    public bool SuitRefillHealth { get; set; } = true;
    public bool SuitRefillFood { get; set; } = true;
    public bool SuitRefillRadiationImmunity { get; set; } = true;
    public bool SuitResetRadiation { get; set; } = true;

    /// <summary>The PluginSdk JSON settings: camelCase names, enums by name, fields included.</summary>
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Reads a values document, or a full PluginSdk envelope (schema, defaults, values) from a server.</summary>
    public static SharedConfigValues FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("values", out var values))
            root = values;
        var result = root.Deserialize<SharedConfigValues>(JsonOptions) ?? new SharedConfigValues();
        result.Normalize();
        return result;
    }

    public SharedConfigValues Clone() => FromJson(ToJson());

    /// <summary>Server structs arrive with null lists and strings; the editor never deals with nulls.</summary>
    private void Normalize()
    {
        Motd ??= "";
        NewUserMotd ??= "";
        MotdUrl ??= "";
        AutoCommands ??= [];
        InfoCommands ??= [];
        BlockPresets ??= [];
        CleanupPresets ??= [];
        DiscordLogHandlers ??= [];
        foreach (var c in AutoCommands)
        {
            c.Name ??= "";
            c.Interval ??= "00:00:00";
            c.SimSpeedDuration ??= "00:00:05";
            c.SimSpeedCooldown ??= "00:00:30";
            c.Steps ??= [];
            foreach (var s in c.Steps)
            {
                s.Command ??= "";
                s.Delay ??= "00:00:00";
            }
        }
        foreach (var i in InfoCommands)
        {
            i.Command ??= "";
            i.ChatResponse ??= "";
            i.DialogResponse ??= "";
            i.Url ??= "";
        }
        foreach (var b in BlockPresets)
        {
            b.Name ??= "";
            b.Items ??= [];
            b.Conditions ??= [];
            foreach (var item in b.Items)
            {
                item.Value ??= "";
                item.Delay ??= "00:00:00";
            }
            foreach (var condition in b.Conditions)
                condition.Parameter ??= "";
        }
        foreach (var p in CleanupPresets)
        {
            p.Name ??= "";
            p.Targets ??= [];
            p.Conditions ??= [];
            foreach (var target in p.Targets)
            {
                target.Parameter ??= "";
                target.Delay ??= "00:00:00";
            }
            foreach (var condition in p.Conditions)
                condition.Parameter ??= "";
        }
        foreach (var h in DiscordLogHandlers)
        {
            h.Name ??= "";
            h.WebhookUrl ??= "";
            h.LoggerNames ??= "";
            h.IgnoredLoggerNames ??= "";
            h.RolePings ??= "";
            h.UserPings ??= "";
        }
    }

    /// <summary>The first problem in the whole config, or null when every list is ready.</summary>
    public string? FirstProblem()
    {
        foreach (var c in AutoCommands)
            if (c.Problem(AutoCommands) is { } p)
                return $"Auto command '{c.Name}': {p}";
        foreach (var i in InfoCommands)
            if (i.Problem(InfoCommands) is { } p)
                return $"Info command '{i.Command}': {p}";
        foreach (var b in BlockPresets)
            if (b.Problem() is { } p)
                return $"Block preset '{b.Name}': {p}";
        foreach (var c in CleanupPresets)
            if (c.Problem() is { } p)
                return $"Cleanup preset '{c.Name}': {p}";
        foreach (var h in DiscordLogHandlers)
            if (h.Problem() is { } p)
                return $"Discord handler '{h.Name}': {p}";
        return null;
    }

    internal static bool IsTime(string? text) =>
        TimeFormat.TryParse(text, out _);
}

public sealed class AutoCommandValues
{
    public string Name { get; set; } = "";
    public Trigger Trigger { get; set; } = Trigger.Disabled;
    public string Interval { get; set; } = "00:00:00";
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.All;
    public Gtl Compare { get; set; } = Gtl.LessThan;
    public double TriggerCount { get; set; }
    public float TriggerRatio { get; set; }
    public string SimSpeedDuration { get; set; } = "00:00:05";
    public string SimSpeedCooldown { get; set; } = "00:00:30";
    public bool HysteresisEnabled { get; set; }
    public float HysteresisRatio { get; set; } = 0.7f;
    public List<AutoCommandStepValues> Steps { get; set; } = [];

    public string? Problem(IEnumerable<AutoCommandValues> all)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return "Give it a name.";
        // Commands are found by name in any case, so the second of two with one name could never be run or voted on.
        if (all.Count(c => string.Equals(c.Name, Name, StringComparison.OrdinalIgnoreCase)) > 1)
            return "Another auto command has the same name.";
        if (Trigger != Trigger.Disabled && Trigger != Trigger.SimSpeed && Trigger != Trigger.OnStart && !SharedConfigValues.IsTime(Interval))
            return "Interval must be HH:MM:SS.";
        if (Trigger is Trigger.Timed or Trigger.GridCount or Trigger.PlayerCount or Trigger.Vote
            && TimeFormat.TryParse(Interval, out var interval) && interval < TimeSpan.FromSeconds(1))
            return "Interval must be at least 1 second. This command will not run until it is.";
        if (Trigger == Trigger.Scheduled && TimeFormat.TryParse(Interval, out var timeOfDay) && timeOfDay >= TimeSpan.FromDays(1))
            return "Time of day must be below 24:00:00.";
        if (Trigger == Trigger.SimSpeed)
        {
            if (Compare == Gtl.Equal)
                return "Sim speed needs LessThan or GreaterThan.";
            if (TriggerRatio <= 0 || TriggerRatio > 1)
                return "Ratio must be between 0 and 1.";
            if (!SharedConfigValues.IsTime(SimSpeedDuration) || !SharedConfigValues.IsTime(SimSpeedCooldown))
                return "Duration and Cooldown must be HH:MM:SS.";
            if (HysteresisEnabled && (HysteresisRatio <= 0 || HysteresisRatio > 1))
                return "Recovery ratio must be between 0 and 1.";
        }
        if (Trigger == Trigger.Vote && (TriggerRatio <= 0 || TriggerRatio > 1))
            return "Yes votes needed must be between 0 and 1.";
        if (Steps.Count == 0)
            return "Add at least one step.";
        if (Steps.Any(s => string.IsNullOrWhiteSpace(s.Command)))
            return "Every step needs a command.";
        if (Steps.Any(s => !SharedConfigValues.IsTime(s.Delay)))
            return "Step delays must be HH:MM:SS.";
        return null;
    }
}

public sealed class AutoCommandStepValues
{
    public string Command { get; set; } = "";
    public string Delay { get; set; } = "00:00:00";
}

public sealed class InfoCommandValues
{
    public string Command { get; set; } = "";
    public string ChatResponse { get; set; } = "";
    public string DialogResponse { get; set; } = "";
    public string Url { get; set; } = "";

    public string? Problem(IEnumerable<InfoCommandValues> all)
    {
        if (string.IsNullOrWhiteSpace(Command))
            return "Enter the command name.";
        if (Command.Contains(' '))
            return "The command name cannot contain spaces.";
        if (all.Count(i => string.Equals(i.Command, Command, StringComparison.OrdinalIgnoreCase)) > 1)
            return "Another info command has the same name.";
        if (string.IsNullOrWhiteSpace(ChatResponse) && string.IsNullOrWhiteSpace(DialogResponse) && string.IsNullOrWhiteSpace(Url))
            return "Fill in a chat response, a dialog response or a URL.";
        return null;
    }
}

public sealed class BlockPresetValues
{
    public string Name { get; set; } = "";
    public BlockAction Action { get; set; } = BlockAction.TurnOff;
    public List<BlockPresetItemValues> Items { get; set; } = [];
    public List<BlockPresetConditionValues> Conditions { get; set; } = [];

    public string? Warning() =>
        Conditions.Any(c => ConditionNotes.IsOwnedByNobody(c.Condition.ToCommandString(), c.Parameter)) ? ConditionNotes.OwnedByNobody : null;

    // Same checks as Shared\Config\BlockList.cs
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return "Give it a name.";
        if (Items.Count == 0)
            return "Add at least one item.";
        if (Items.Any(i => string.IsNullOrWhiteSpace(i.Value)))
            return "Every item needs a Value.";
        if (Items.Any(i => !SharedConfigValues.IsTime(i.Delay)))
            return "Item delays must be HH:MM:SS.";
        var missing = Conditions.FirstOrDefault(c => c.Condition.RequiresParameter() && string.IsNullOrWhiteSpace(c.Parameter));
        if (missing != null)
            return $"Condition '{missing.Condition}' needs a Parameter.";
        var extra = Conditions.FirstOrDefault(c => !c.Condition.RequiresParameter() && !string.IsNullOrWhiteSpace(c.Parameter));
        if (extra != null)
            return $"Condition '{extra.Condition}' does not take a Parameter.";
        return null;
    }
}

public sealed class BlockPresetItemValues
{
    public SelectorType Selector { get; set; }
    public string Value { get; set; } = "";
    public string Delay { get; set; } = "00:00:00";
}

public sealed class BlockPresetConditionValues
{
    public ConditionType Condition { get; set; }
    public string Parameter { get; set; } = "";
}

public sealed class CleanupPresetValues
{
    public string Name { get; set; } = "";
    public CleanupAction Action { get; set; } = CleanupAction.Delete;
    public bool ApplyToAll { get; set; }
    public List<CleanupTargetValues> Targets { get; set; } = [];
    public List<CleanupConditionValues> Conditions { get; set; } = [];

    public string? Warning() =>
        Conditions.Any(c => ConditionNotes.IsOwnedByNobody(c.Condition.ToCommandString(), c.Parameter))
        || (!ApplyToAll && Targets.Any(t => ConditionNotes.IsOwnedByNobody(t.Condition.ToCommandString(), t.Parameter)))
            ? ConditionNotes.OwnedByNobody
            : null;

    // Same checks as Shared\Config\CleanupPreset.cs
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return "Give it a name.";
        if (Action == CleanupAction.DeleteFloatingObjects)
            return null;
        if (!ApplyToAll && Targets.Count == 0)
            return "Add at least one target, or tick Apply To All.";
        if (ApplyToAll && Conditions.Count == 0)
            return "Apply To All needs at least one condition.";
        if (!ApplyToAll && Targets.Any(t => !SharedConfigValues.IsTime(t.Delay)))
            return "Target delays must be HH:MM:SS.";

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

public sealed class CleanupTargetValues
{
    public ConditionType Condition { get; set; }
    public string Parameter { get; set; } = "";
    public string Delay { get; set; } = "00:00:00";
}

public sealed class CleanupConditionValues
{
    public ConditionType Condition { get; set; }
    public string Parameter { get; set; } = "";
}

public sealed class DiscordHandlerValues
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string WebhookUrl { get; set; } = "";
    public EssentialsLogLevel MinLevel { get; set; } = EssentialsLogLevel.Warn;
    public EssentialsLogLevel MaxLevel { get; set; } = EssentialsLogLevel.Fatal;
    public string LoggerNames { get; set; } = "";
    public string IgnoredLoggerNames { get; set; } = "";
    public string RolePings { get; set; } = "";
    public string UserPings { get; set; } = "";
    public bool OnlyWhenServerOnline { get; set; }

    // Same checks as Shared\Config\DiscordLogHandler.cs
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(WebhookUrl))
            return "Enter the Discord webhook URL.";
        if (!Uri.TryCreate(WebhookUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !WebhookHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
            || !uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.OrdinalIgnoreCase))
            return "The webhook URL must look like https://discord.com/api/webhooks/...";
        if (MinLevel > MaxLevel)
            return "Min Level is higher than Max Level, so nothing is sent.";
        if (!AreIds(RolePings))
            return "Role Pings must be numeric role IDs separated by commas.";
        if (!AreIds(UserPings))
            return "User Pings must be numeric user IDs separated by commas.";
        return null;
    }

    // Same list as Shared\Config\DiscordLogHandler.cs: the server sends only to these.
    private static readonly string[] WebhookHosts =
        ["discord.com", "ptb.discord.com", "canary.discord.com", "discordapp.com", "ptb.discordapp.com", "canary.discordapp.com"];

    private static bool AreIds(string text) =>
        text.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).All(part => ulong.TryParse(part.Trim(), out _));
}
