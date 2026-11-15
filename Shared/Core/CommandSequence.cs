using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SensEssentials;

/// <summary>
/// Runs commands one after another on the game thread, for auto commands and presets.
/// </summary>
internal static class CommandSequence
{
    private static CancellationTokenSource _presets = new();

    /// <summary>
    /// Runs each command, then waits its delay, so a delay counts from when its command ran even while the game thread
    /// is busy. A command still waiting in the game thread's queue is skipped if the run was cancelled, the server stopped,
    /// or the world it started in has unloaded, and a run whose world changes stops at once. Throws
    /// <see cref="OperationCanceledException"/> when the token is cancelled.
    /// </summary>
    internal static async Task Run(IReadOnlyList<(string Command, TimeSpan Delay)> steps, string source, CancellationToken token)
    {
        var world = WorldSession.Current;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token, WorldSession.Ending);
        try
        {
            foreach (var (command, delay) in steps)
            {
                await GameThread.InvokeAsync(() =>
                {
                    if (!stop.IsCancellationRequested && WorldSession.Current == world && EssentialsCore.Host.IsServerRunning
                        && !string.IsNullOrEmpty(command))
                        EssentialsCore.RunCommand(command, source);
                }, stop.Token).ConfigureAwait(false);

                await Task.Delay(delay, stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            // The world changed: nothing more of this run belongs in the next one.
        }
    }

    /// <summary>
    /// Starts a preset in the background. <see cref="CancelPresets"/> stops every preset that is still running.
    /// </summary>
    internal static void Start(string source, List<(string Command, TimeSpan Delay)> steps)
    {
        var token = Volatile.Read(ref _presets).Token;
        Task.Run(async () =>
        {
            try
            {
                await Run(steps, source, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Log.Error(e, $"{source} failed");
            }
        });
    }

    /// <summary>
    /// Stops every running preset. Called when the world unloads.
    /// </summary>
    internal static void CancelPresets()
    {
        // Not disposed: a preset that has not reached its next check still reads the old token.
        Interlocked.Exchange(ref _presets, new CancellationTokenSource()).Cancel();
    }
}
