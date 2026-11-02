using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace SensEssentials;

/// <summary>
/// The parts of a host's command context the shared command code uses. Member names match Torch's
/// CommandContext so command bodies read the same on both hosts.
/// </summary>
public interface ICommandContext
{
    /// <summary>
    /// Words after the command path. Quoted words are already joined.
    /// </summary>
    IReadOnlyList<string> Args { get; }

    /// <summary>
    /// Everything after the command path, as typed.
    /// </summary>
    string RawArgs { get; }

    /// <summary>
    /// The calling player, or null for the server console.
    /// </summary>
    IMyPlayer? Player { get; }

    /// <summary>
    /// True when the server itself ran the command (console, auto commands).
    /// </summary>
    bool SentBySelf { get; }

    void Respond(string message);
}

/// <summary>
/// Base class for the shared command bodies. Each host wraps them in its own command modules.
/// </summary>
public abstract class CommandActions
{
    protected CommandActions(ICommandContext context)
    {
        Context = context;
    }

    protected ICommandContext Context { get; }

    /// <summary>
    /// Shows the text in a dialog to the calling player, or replies in chat when called from the console or when
    /// the host cannot show dialogs.
    /// </summary>
    protected void Show(string title, string? subtitle, string content)
    {
        var steamId = Context.Player?.SteamUserId ?? 0;
        if (steamId == 0 || !EssentialsCore.Host.ShowDialog(steamId, title, subtitle, content))
            Context.Respond(content);
    }
}
