using System.Threading;

namespace SensEssentials;

/// <summary>
/// Which world is loaded. It changes when a world loads and when it starts to unload, so work queued in one world
/// can check it before running and never run in the next.
/// </summary>
public static class WorldSession
{
    private static int _current;
    private static CancellationTokenSource _ending = new();

    public static int Current => Volatile.Read(ref _current);

    /// <summary>
    /// Cancelled when the current world changes, so work waiting on it can stop at once.
    /// </summary>
    public static CancellationToken Ending => Volatile.Read(ref _ending).Token;

    internal static void Next()
    {
        Interlocked.Increment(ref _current);
        // Not disposed: work that has not reached its next check still reads the old token.
        Interlocked.Exchange(ref _ending, new CancellationTokenSource()).Cancel();
    }
}
