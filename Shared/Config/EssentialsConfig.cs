using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SensEssentials;

public class EssentialsConfig : ObservableObject
{
    public EssentialsConfig()
    {
        WatchCollection(AutoCommands);
        WatchCollection(InfoCommands);
        WatchCollection(BlockLists);
        WatchCollection(CleanupPresets);
        WatchCollection(PlayerPermissions);
        WatchCollection(DiscordLogHandlers);
    }

    public ObservableCollection<AutoCommand> AutoCommands { get; } = [];

    public ObservableCollection<InfoCommand> InfoCommands { get; } = [];

    public ObservableCollection<BlockList> BlockLists { get; } = [];

    public ObservableCollection<CleanupPreset> CleanupPresets { get; } = [];

    public bool EnableCommandPermissions { get; set => SetValue(ref field, value); }

    public ObservableCollection<PlayerPermission> PlayerPermissions { get; } = [];

    public string Motd { get; set => SetValue(ref field, value); } = "";

    public string NewUserMotd { get; set => SetValue(ref field, value); } = "";

    public string MotdUrl { get; set => SetValue(ref field, value); } = "";

    public bool NewUserMotdUrl { get; set => SetValue(ref field, value); }

    public bool StopShipsOnStart { get; set => SetValue(ref field, value); }

    public bool UtilityShowPosition { get; set => SetValue(ref field, value); }

    public bool MarkerShowPosition { get; set => SetValue(ref field, value); }

    public int BackpackLimit { get; set => SetValue(ref field, value); } = 1;

    public bool CutGameTags { get; set => SetValue(ref field, value); }

    // Discord Logging

    public bool DiscordLoggingEnabled { get; set => SetValue(ref field, value); }

    public ObservableCollection<DiscordLogHandler> DiscordLogHandlers { get; } = [];

    /// <summary>
    /// Entries each webhook may post per second; the rest are dropped and counted. Discord allows about 5.
    /// </summary>
    public int DiscordRateLimitPerSecond { get; set => SetValue(ref field, Math.Min(Math.Max(value, 1), 5)); } = 3;

    /// <summary>
    /// Stack traces longer than this are attached as a file instead of shown in the message.
    /// </summary>
    public int DiscordStackTraceEmbedLimit { get; set => SetValue(ref field, Math.Min(Math.Max(value, 0), 2500)); } = 2000;

    // Admin Tools: Suit Refill

    public bool SuitRefillEnabled { get; set => SetValue(ref field, value); }

    public bool SuitRefillAdmins { get; set => SetValue(ref field, value); } = true;

    /// <summary>
    /// Lobby mode: every online player is refilled.
    /// </summary>
    public bool SuitRefillEveryone { get; set => SetValue(ref field, value); }

    public bool SuitRefillOxygen { get; set => SetValue(ref field, value); } = true;

    public bool SuitRefillHydrogen { get; set => SetValue(ref field, value); } = true;

    public bool SuitRefillEnergy { get; set => SetValue(ref field, value); } = true;

    public bool SuitRefillHealth { get; set => SetValue(ref field, value); } = true;

    public bool SuitRefillFood { get; set => SetValue(ref field, value); } = true;

    public bool SuitRefillRadiationImmunity { get; set => SetValue(ref field, value); } = true;

    public bool SuitResetRadiation { get; set => SetValue(ref field, value); } = true;

    /// <summary>
    /// Set once the settings of the old Senxs Utilities plugin have been imported, so they are not imported twice.
    /// </summary>
    public bool SenxsUtilitiesImported { get; set => SetValue(ref field, value); }

    private void WatchCollection<T>(ObservableCollection<T> collection) where T : ObservableObject
    {
        collection.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
                foreach (T item in e.NewItems)
                    item.PropertyChanged += ItemChanged;
            if (e.OldItems != null)
                foreach (T item in e.OldItems)
                    item.PropertyChanged -= ItemChanged;
            OnPropertyChanged();
        };

        foreach (var item in collection)
            item.PropertyChanged += ItemChanged;
    }

    private void ItemChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged();
}
