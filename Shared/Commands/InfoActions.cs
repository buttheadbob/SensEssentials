using System;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.World;
using VRageMath;

namespace SensEssentials.Commands;

/// <summary>
/// Info commands: admin defined chat words (like !discord) that answer with a chat line, a dialog and/or a link.
/// </summary>
public sealed class InfoActions(ICommandContext context) : CommandActions(context)
{
    public void List()
    {
        Context.Respond(string.Join(", ", EssentialsCore.Config.InfoCommands.Select(i => i.Command).Where(c => !string.IsNullOrEmpty(c))));
    }

    /// <summary>
    /// !info &lt;name&gt;, for hosts without a chat hook. The leading ! of the configured command is optional.
    /// </summary>
    public void Show(string name)
    {
        if (Context.Player == null)
        {
            Context.Respond("Info commands can only be shown to a player in-game.");
            return;
        }

        var key = name.Trim().TrimStart('!');
        var match = EssentialsCore.Config.InfoCommands.FirstOrDefault(i =>
            string.Equals(i.Command?.Trim().TrimStart('!'), key, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            Context.Respond($"No info command named '{name}'. Use !info list to see them.");
            return;
        }

        TryHandle(Context.Player.SteamUserId, match.Command);
    }

    /// <summary>
    /// Answers a chat message that matches an info command. Returns true when it did, so the host can stop the
    /// message from reaching chat. Never throws.
    /// </summary>
    public static bool TryHandle(ulong authorSteamId, string? message)
    {
        try
        {
            // Snapshot, since the UI can edit the list at any time.
            var infoCommands = EssentialsCore.Config.InfoCommands.ToArray();
            if (infoCommands.Length == 0)
                return false;

            var text = message?.Trim();
            var c = infoCommands.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Command)
                                                     && string.Equals(i.Command!.Trim(), text, StringComparison.OrdinalIgnoreCase));
            if (c == null)
                return false;

            if (authorSteamId == 0)
                return true;

            long playerId = MySession.Static.Players.TryGetIdentityId(authorSteamId);

            if (!string.IsNullOrEmpty(c.ChatResponse))
                EssentialsCore.Host.SendChat("Server", c.ChatResponse!, Color.Blue, authorSteamId);

            if (!string.IsNullOrEmpty(c.DialogResponse)
                && !EssentialsCore.Host.ShowDialog(authorSteamId, c.Command!, null, c.DialogResponse!))
                EssentialsCore.Host.SendChat("Server", c.DialogResponse!, Color.Blue, authorSteamId);

            if (!string.IsNullOrEmpty(c.URL))
                MyVisualScriptLogicProvider.OpenSteamOverlay(Motd.MakeUrl(c.URL!), playerId);

            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Error handling info command");
            return false;
        }
    }
}
