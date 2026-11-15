using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Game;
using Sandbox.Game.World;

namespace SensEssentials;

public static class GameThread
{
    /// <summary>
    /// Runs an action on the game thread. SE does not catch exceptions thrown by invoked actions, so an unguarded
    /// throw there takes the server down; this logs it instead.
    /// </summary>
    public static void InvokeSafe(Action action, [CallerMemberName] string caller = "")
    {
        MySandboxGame.Static.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Error(e, $"Error in game thread action from {caller}");
            }
        }, caller);
    }

    /// <summary>
    /// Runs an action on the game thread, and finishes once it has run, or when the token is cancelled first.
    /// </summary>
    public static Task InvokeAsync(Action action, CancellationToken token = default, [CallerMemberName] string caller = "")
    {
        // Continuations run off the game thread, so a caller awaiting this never delays the game.
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancel = token.Register(() => done.TrySetCanceled());
        InvokeSafe(() =>
        {
            try
            {
                action();
            }
            finally
            {
                cancel.Dispose();
                done.TrySetResult(true);
            }
        }, caller);
        return done.Task;
    }
}

/// <summary>
/// Player notifications that need no client mod.
/// </summary>
public static class Notify
{
    /// <summary>
    /// Shows a notification at the top of one player's screen. Does nothing for players without an identity.
    /// </summary>
    public static void Player(ulong steamId, string message, int milliseconds = 5000, string font = "Blue")
    {
        var identityId = MySession.Static?.Players.TryGetIdentityId(steamId) ?? 0;
        if (identityId != 0)
            MyVisualScriptLogicProvider.ShowNotification(message, milliseconds, font, identityId);
    }

    public static void All(string message, int milliseconds = 5000, string font = "Blue")
    {
        MyVisualScriptLogicProvider.ShowNotificationToAll(message, milliseconds, font);
    }
}
