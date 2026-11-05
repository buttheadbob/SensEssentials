using System;
using System.Threading;
using System.Timers;
using Sandbox;
using Timer = System.Timers.Timer;

namespace SensEssentials;

/// <summary>
/// Runs <see cref="Tick"/> on the game thread once a second, where entities, players and commands are safe to use.
/// </summary>
public abstract class GameThreadTimer(string name) : IDisposable
{
    private Timer? _timer;
    private int _tickQueued;

    public void Start()
    {
        if (_timer != null)
            return;

        _timer = new Timer(1000);
        _timer.Elapsed += TimerElapsed;
        _timer.AutoReset = true;
        _timer.Start();
    }

    public virtual void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    protected abstract void Tick();

    private void TimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // Skip this tick if the previous one has not been processed yet.
        if (Interlocked.Exchange(ref _tickQueued, 1) == 1)
            return;

        try
        {
            MySandboxGame.Static.Invoke(() =>
            {
                try
                {
                    Tick();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error during the {name} update");
                }
                finally
                {
                    Interlocked.Exchange(ref _tickQueued, 0);
                }
            }, $"SensEssentials {name}");
        }
        catch (Exception ex)
        {
            // The game is shutting down; let the next tick try again.
            Interlocked.Exchange(ref _tickQueued, 0);
            Log.Debug($"Could not queue the {name} update: {ex.Message}");
        }
    }
}
