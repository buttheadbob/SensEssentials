using System;
using System.Collections.Generic;
using System.Linq;
using PluginSdk.Config;

namespace SensEssentials.MagnetarHost;

/// <summary>
/// Magnetar config. Quasar renders the editor from these attributes, there is no UI code.
/// Quasar can only edit C# structs and List&lt;T&gt;, so this mirrors the shared <see cref="EssentialsConfig"/> and is
/// copied into it by <see cref="ApplyTo"/> whenever it changes. Command Permissions are Torch only and not here.
/// The Sens Essentials Quasar plugin edits the same values JSON (QuasarPlugin/SensEssentials.Quasar/Model/SharedConfigValues.cs),
/// so renaming or adding an option here must be done there too.
/// </summary>
[Tab("general", caption: "General")]
[Tab("motd", caption: "MOTD")]
[Tab("auto", caption: "Auto Commands")]
[Tab("info", caption: "Info Commands")]
[Tab("blocks", caption: "Block Presets")]
[Tab("cleanup", caption: "Cleanup Presets")]
[Tab("discord", caption: "Discord Logging")]
[Tab("admin", caption: "Admin Tools")]
public class MagnetarConfig : PluginConfig
{
    // General

    [StringOption(description: "Set by the Sens Essentials Quasar plugin. While a shared config is named here, changes made on this page are undone; edit the shared config in Quasar instead", Parent = "general")]
    public string SharedConfig { get; set => SetField(ref field, value ?? ""); } = "";

    [BoolOption("Stop every moving entity when the server starts", Parent = "general")]
    public bool StopShipsOnStart { get; set => SetField(ref field, value); }

    [BoolOption("!grids list shows the position of each grid", Parent = "general")]
    public bool UtilityShowPosition { get; set => SetField(ref field, value); }

    [BoolOption("!grids list adds a GPS marker for each grid", Parent = "general")]
    public bool MarkerShowPosition { get; set => SetField(ref field, value); }

    [IntOption(-1, 1000, "Backpacks each player may have in the world, counted per player; empty ones and the oldest over the limit, even with items in them, are deleted after 30 seconds. -1 for no limit", Parent = "general")]
    public int BackpackLimit { get; set => SetField(ref field, value); } = 1;

    [BoolOption("Players can see each other's balances with !economy top and !economy check. When off, only Admins can", Parent = "general")]
    public bool PublicBalances { get; set => SetField(ref field, value); } = true;

    // MOTD

    [StringOption(description: "Shown to every player when they spawn. %player% is replaced with their name", Parent = "motd", Multiline = true)]
    public string Motd { get; set => SetField(ref field, value ?? ""); } = "";

    [StringOption(description: "Shown instead of the MOTD to players joining for the first time", Parent = "motd", Multiline = true)]
    public string NewUserMotd { get; set => SetField(ref field, value ?? ""); } = "";

    [StringOption(description: "Opened in the player's Steam overlay instead of showing the MOTD text", Parent = "motd")]
    public string MotdUrl { get; set => SetField(ref field, value ?? ""); } = "";

    [BoolOption("Open the MOTD URL only for players joining for the first time", Parent = "motd")]
    public bool NewUserMotdUrl { get; set => SetField(ref field, value); }

    // Auto Commands

    [ListOption(description: "Commands that run on a timer, a schedule, at startup, on a vote, or when grid count, player count or sim speed crosses a limit", Parent = "auto")]
    public List<AutoCommandEntry> AutoCommands { get; set => SetField(ref field, value ?? []); } = [];

    // Info Commands

    [ListOption(description: "Run with !info <command>. Each one can answer with a chat line, a dialog and/or a link", Parent = "info")]
    public List<InfoCommandEntry> InfoCommands { get; set => SetField(ref field, value ?? []); } = [];

    // Presets

    [ListOption(description: "Run with !admin runpreset <name>, or as an auto command step", Parent = "blocks")]
    public List<BlockPresetEntry> BlockPresets { get; set => SetField(ref field, value ?? []); } = [];

    [ListOption(description: "Run with !admin runcleanup <name>, or as an auto command step", Parent = "cleanup")]
    public List<CleanupPresetEntry> CleanupPresets { get; set => SetField(ref field, value ?? []); } = [];

    // Discord Logging

    [BoolOption("Post log entries to Discord webhooks", Parent = "discord")]
    public bool DiscordLoggingEnabled { get; set => SetField(ref field, value); }

    [IntOption(1, 5, "Entries each webhook may post per second; the rest are dropped and counted", Parent = "discord")]
    public int DiscordRateLimitPerSecond { get; set => SetField(ref field, value); } = 3;

    [IntOption(0, 2500, "Stack traces longer than this many characters are attached as a file", Parent = "discord")]
    public int DiscordStackTraceEmbedLimit { get; set => SetField(ref field, value); } = 2000;

    [ListOption(description: "Each handler posts the entries it wants to one Discord channel", Parent = "discord")]
    public List<DiscordHandlerEntry> DiscordLogHandlers { get; set => SetField(ref field, value ?? []); } = [];

    // Admin Tools

    [BoolOption("Every 10 seconds, top up the suits of the players chosen below", Parent = "admin")]
    public bool SuitRefillEnabled { get; set => SetField(ref field, value); }

    [BoolOption("Refill admins and owners", Parent = "admin")]
    public bool SuitRefillAdmins { get; set => SetField(ref field, value); } = true;

    [BoolOption("Refill every online player (lobby mode)", Parent = "admin")]
    public bool SuitRefillEveryone { get; set => SetField(ref field, value); }

    [BoolOption("Refill oxygen", Parent = "admin")]
    public bool SuitRefillOxygen { get; set => SetField(ref field, value); } = true;

    [BoolOption("Refill hydrogen", Parent = "admin")]
    public bool SuitRefillHydrogen { get; set => SetField(ref field, value); } = true;

    [BoolOption("Refill energy", Parent = "admin")]
    public bool SuitRefillEnergy { get; set => SetField(ref field, value); } = true;

    [BoolOption("Refill health", Parent = "admin")]
    public bool SuitRefillHealth { get; set => SetField(ref field, value); } = true;

    [BoolOption("Refill food (worlds that use food)", Parent = "admin")]
    public bool SuitRefillFood { get; set => SetField(ref field, value); } = true;

    [BoolOption("Refill radiation immunity (worlds that use radiation)", Parent = "admin")]
    public bool SuitRefillRadiationImmunity { get; set => SetField(ref field, value); } = true;

    [BoolOption("Bring radiation down to a low level", Parent = "admin")]
    public bool SuitResetRadiation { get; set => SetField(ref field, value); } = true;

    /// <summary>
    /// Copies these settings into the shared config. Auto commands are matched by name and updated in place, so
    /// a running command and its schedule survive an unrelated config change.
    /// </summary>
    public void ApplyTo(EssentialsConfig c)
    {
        c.StopShipsOnStart = StopShipsOnStart;
        c.UtilityShowPosition = UtilityShowPosition;
        c.MarkerShowPosition = MarkerShowPosition;
        c.BackpackLimit = BackpackLimit;
        c.PublicBalances = PublicBalances;

        c.Motd = Motd;
        c.NewUserMotd = NewUserMotd;
        c.MotdUrl = MotdUrl;
        c.NewUserMotdUrl = NewUserMotdUrl;

        ApplyAutoCommands(c);

        c.InfoCommands.Clear();
        foreach (var e in InfoCommands)
            c.InfoCommands.Add(new InfoCommand { Command = e.Command, ChatResponse = e.ChatResponse, DialogResponse = e.DialogResponse, URL = e.Url });

        c.BlockLists.Clear();
        foreach (var e in BlockPresets)
        {
            var preset = new BlockList { Name = e.Name ?? "", Action = e.Action };
            foreach (var item in e.Items ?? [])
                preset.Items.Add(new BlockListItem { Selector = item.Selector, Value = item.Value ?? "", Delay = item.Delay ?? "" });
            foreach (var condition in e.Conditions ?? [])
                preset.Conditions.Add(new BlockListCondition { Condition = condition.Condition, Parameter = condition.Parameter });
            c.BlockLists.Add(preset);
        }

        c.CleanupPresets.Clear();
        foreach (var e in CleanupPresets)
        {
            var preset = new CleanupPreset { Name = e.Name ?? "", Action = e.Action, ApplyToAll = e.ApplyToAll };
            foreach (var target in e.Targets ?? [])
                preset.Targets.Add(new CleanupTarget { Condition = target.Condition, Parameter = target.Parameter, Delay = target.Delay ?? "" });
            foreach (var condition in e.Conditions ?? [])
                preset.Conditions.Add(new CleanupCondition { Condition = condition.Condition, Parameter = condition.Parameter });
            c.CleanupPresets.Add(preset);
        }

        c.DiscordLoggingEnabled = DiscordLoggingEnabled;
        c.DiscordRateLimitPerSecond = DiscordRateLimitPerSecond;
        c.DiscordStackTraceEmbedLimit = DiscordStackTraceEmbedLimit;
        c.DiscordLogHandlers.Clear();
        foreach (var e in DiscordLogHandlers)
        {
            c.DiscordLogHandlers.Add(new DiscordLogHandler
            {
                Name = e.Name ?? "",
                Enabled = e.Enabled,
                WebhookUrl = e.WebhookUrl ?? "",
                MinLevel = e.MinLevel,
                MaxLevel = e.MaxLevel,
                LoggerNames = e.LoggerNames ?? "",
                IgnoredLoggerNames = e.IgnoredLoggerNames ?? "",
                RolePings = e.RolePings ?? "",
                UserPings = e.UserPings ?? "",
                OnlyWhenServerOnline = e.OnlyWhenServerOnline
            });
        }

        c.SuitRefillEnabled = SuitRefillEnabled;
        c.SuitRefillAdmins = SuitRefillAdmins;
        c.SuitRefillEveryone = SuitRefillEveryone;
        c.SuitRefillOxygen = SuitRefillOxygen;
        c.SuitRefillHydrogen = SuitRefillHydrogen;
        c.SuitRefillEnergy = SuitRefillEnergy;
        c.SuitRefillHealth = SuitRefillHealth;
        c.SuitRefillFood = SuitRefillFood;
        c.SuitRefillRadiationImmunity = SuitRefillRadiationImmunity;
        c.SuitResetRadiation = SuitResetRadiation;
    }

    private void ApplyAutoCommands(EssentialsConfig c)
    {
        var old = c.AutoCommands.ToList();
        c.AutoCommands.Clear();

        foreach (var e in AutoCommands)
        {
            var name = e.Name ?? "";
            var cmd = old.FirstOrDefault(o => o.Name == name);
            if (cmd != null)
                old.Remove(cmd);
            else
                cmd = new AutoCommand { Name = name };

            // Trigger and interval restart the schedule, so they are only set when they changed.
            if (cmd.CommandTrigger != e.Trigger)
                cmd.CommandTrigger = e.Trigger;
            var interval = NormalizeTime(e.Interval, TimeSpan.Zero);
            if (cmd.Interval != interval)
                cmd.Interval = interval;

            cmd.DayOfWeek = e.DayOfWeek;
            cmd.Compare = e.Compare;
            cmd.TriggerCount = e.TriggerCount;
            cmd.TriggerRatio = e.TriggerRatio;
            cmd.SimSpeedDuration = NormalizeTime(e.SimSpeedDuration, TimeSpan.FromSeconds(5));
            cmd.SimSpeedCooldown = NormalizeTime(e.SimSpeedCooldown, TimeSpan.FromSeconds(30));
            cmd.HysteresisEnabled = e.HysteresisEnabled;
            cmd.HysteresisRatio = e.HysteresisRatio > 0 ? e.HysteresisRatio : 0.7f;

            cmd.Steps.Clear();
            foreach (var step in e.Steps ?? [])
                cmd.Steps.Add(new AutoCommand.CommandStep { Command = step.Command ?? "", Delay = NormalizeTime(step.Delay, TimeSpan.Zero) });

            c.AutoCommands.Add(cmd);
        }

        // Removed commands must not keep running.
        foreach (var removed in old)
            removed.Cancel();
    }

    private static string NormalizeTime(string? text, TimeSpan fallback)
    {
        return TimeFormat.Format(TimeFormat.TryParse(text, out var span) ? span : fallback);
    }
}

public struct AutoCommandEntry
{
    [StructMember, StructCaption] public string Name;
    [StructMember("When the command runs")] public Trigger Trigger;
    [StructMember("HH:MM:SS, or D:HH:MM:SS for days. Timed: time between runs, at least 1 second. Scheduled: time of day. Grid/Player count: how often to check. Vote: how long the vote lasts")] public string Interval;
    [StructMember("Scheduled only: the day to run on")] public DayOfWeek DayOfWeek;
    [StructMember("Grid count, player count and sim speed: the comparison")] public Gtl Compare;
    [StructMember("Grid count and player count: the number to compare with. Vote: seconds before the same player can start another vote")] public double TriggerCount;
    [StructMember("Sim speed: the sim speed to compare with (0 to 1). Vote: share of online players that must vote yes (0 to 1)")] public float TriggerRatio;
    [StructMember("Sim speed: HH:MM:SS the condition must hold before the command runs")] public string SimSpeedDuration;
    [StructMember("Sim speed: HH:MM:SS to wait before it can run again")] public string SimSpeedCooldown;
    [StructMember("Sim speed: after running, wait until sim speed passes the recovery ratio before it can run again")] public bool HysteresisEnabled;
    [StructMember("Sim speed: the recovery ratio (0 to 1)")] public float HysteresisRatio;
    [StructMember("Commands to run in order, each followed by its delay")] public List<AutoCommandStepEntry> Steps;
}

public struct AutoCommandStepEntry
{
    [StructMember("A chat command including the !, for example !save"), StructCaption] public string Command;
    [StructMember("HH:MM:SS to wait after this step")] public string Delay;
}

public struct InfoCommandEntry
{
    [StructMember("The name players type after !info"), StructCaption] public string Command;
    [StructMember("Sent to the player in chat")] public string ChatResponse;
    [StructMember("Shown to the player in a dialog")] public string DialogResponse;
    [StructMember("Opened in the player's Steam overlay")] public string Url;
}

public struct BlockPresetEntry
{
    [StructMember, StructCaption] public string Name;
    [StructMember("What to do with the matching blocks")] public BlockAction Action;
    [StructMember("Blocks to act on, one command each")] public List<BlockPresetItemEntry> Items;
    [StructMember("Only grids matching all of these, as used by !cleanup")] public List<BlockPresetConditionEntry> Conditions;
}

public struct BlockPresetItemEntry
{
    [StructMember] public SelectorType Selector;
    [StructMember("The block type or subtype"), StructCaption] public string Value;
    [StructMember("HH:MM:SS to wait after this item")] public string Delay;
}

public struct BlockPresetConditionEntry
{
    [StructMember] public ConditionType Condition;
    [StructMember("The condition's value, if it takes one")] public string Parameter;
}

public struct CleanupPresetEntry
{
    [StructMember, StructCaption] public string Name;
    [StructMember] public CleanupAction Action;
    [StructMember("Run once with only the conditions below, instead of once per target")] public bool ApplyToAll;
    [StructMember("Each target runs as its own !cleanup delete with the conditions below added")] public List<CleanupTargetEntry> Targets;
    [StructMember("Added to every target")] public List<CleanupConditionEntry> Conditions;
}

public struct CleanupTargetEntry
{
    [StructMember] public ConditionType Condition;
    [StructMember("The condition's value, if it takes one"), StructCaption] public string Parameter;
    [StructMember("HH:MM:SS to wait after this target")] public string Delay;
}

public struct CleanupConditionEntry
{
    [StructMember] public ConditionType Condition;
    [StructMember("The condition's value, if it takes one")] public string Parameter;
}

public struct DiscordHandlerEntry
{
    [StructMember, StructCaption] public string Name;
    [StructMember] public bool Enabled;
    [StructMember("In Discord: channel settings, Integrations, Webhooks, Copy Webhook URL. Keep it private")] public string WebhookUrl;
    [StructMember("Entries below this level are not sent")] public EssentialsLogLevel MinLevel;
    [StructMember("Entries above this level are not sent")] public EssentialsLogLevel MaxLevel;
    [StructMember("Only these loggers, comma separated; a trailing * matches by prefix. Empty sends every logger")] public string LoggerNames;
    [StructMember("Never these loggers, comma separated")] public string IgnoredLoggerNames;
    [StructMember("Role IDs to ping, comma separated")] public string RolePings;
    [StructMember("User IDs to ping, comma separated")] public string UserPings;
    [StructMember("Only send while a world is loaded")] public bool OnlyWhenServerOnline;
}
