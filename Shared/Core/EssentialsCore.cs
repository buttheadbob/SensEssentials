using System;
using System.IO;
using VRageMath;

// Engine neutral core, compiled into BOTH the Torch plugin and the Magnetar plugin.
// Rules for everything under Shared/:
//  - game assemblies (Sandbox.*, VRage.*) and Newtonsoft.Json only: NO Torch, NLog, Harmony or PluginSdk references
//  - talk to the host plugin through IEssentialsHost only
//  - Magnetar compiles this folder itself, for .NET Framework 4.8 and .NET 10, so no WPF or Windows only APIs
namespace SensEssentials;

/// <summary>
/// What the shared code needs from whichever plugin loader it runs in.
/// </summary>
public interface IEssentialsHost
{
    EssentialsConfig Config { get; }

    /// <summary>
    /// Folder for the plugin's own files, such as the known Steam ID list.
    /// </summary>
    string StorageDir { get; }

    /// <summary>
    /// False while the server is starting or stopping, when commands must not run.
    /// </summary>
    bool IsServerRunning { get; }

    void Log(EssentialsLogLevel level, string channel, string message, Exception? exception);

    /// <summary>
    /// Runs a chat command line, including its "!", as the server console. Call on the game thread.
    /// Returns false when the line names no command.
    /// </summary>
    bool RunCommand(string commandLine);

    /// <summary>
    /// Shows a pop-up dialog to a player. Returns false when the host cannot show one, so the caller can fall
    /// back to chat.
    /// </summary>
    bool ShowDialog(ulong steamId, string title, string? subtitle, string content);

    /// <summary>
    /// Sends a chat message. A null author or color uses the server's own; a target of 0 sends to everyone.
    /// </summary>
    void SendChat(string? author, string message, Color? color = null, ulong targetSteamId = 0);

    /// <summary>
    /// Turns text into one command argument. Hosts read quotes and backslashes differently, so a preset's regex
    /// has to be quoted by the host that will read it.
    /// </summary>
    string QuoteArgument(string text);

    /// <summary>
    /// True when <see cref="NotifyVoxelsReset"/> reaches connected players.
    /// </summary>
    bool CanReloadVoxels { get; }

    /// <summary>
    /// Tells clients that these voxel maps were reset, so they reload them. Hosts without a client mod do nothing.
    /// </summary>
    void NotifyVoxelsReset(long[] entityIds);
}

public static class EssentialsCore
{
    private static IEssentialsHost? _host;

    /// <summary>
    /// Runs a command as the server. Nobody sees the reply of an auto command or preset step, so a line that
    /// is not a command goes to the log. Replies go to the log through the host.
    /// </summary>
    public static void RunCommand(string command, string source)
    {
        var line = command.Trim();
        if (!line.StartsWith("!"))
            line = "!" + line;

        if (!Host.RunCommand(line))
            Log.Error($"{source}: '{line}' could not run. Check the spelling.");
    }

    /// <summary>
    /// Keeps a config that could not be read. Hosts load defaults after this and overwrite the file at the next
    /// save, which would lose everything in it over one bad value.
    /// </summary>
    public static void KeepBadConfig(string path, Exception reason)
    {
        try
        {
            var copy = $"{path}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(path, copy, true);
            Log.Error(reason, $"Could not read {path}, so defaults are in use. The file was kept as {copy}. Fix it and copy it back.");
        }
        catch (Exception e)
        {
            Log.Error(e, $"Could not keep a copy of the unreadable config {path}");
        }
    }

    /// <summary>
    /// Set by the host plugin in Init, before any command or timer can run.
    /// </summary>
    public static IEssentialsHost Host
    {
        get => _host ?? throw new InvalidOperationException("Used before the host plugin finished starting.");
        set => _host = value;
    }

    /// <summary>
    /// For code that can run before the host is set, such as logging.
    /// </summary>
    public static IEssentialsHost? HostOrNull => _host;

    public static EssentialsConfig Config => Host.Config;
}

/// <summary>
/// Logging for the shared code, forwarded to the host's logger.
/// </summary>
public static class Log
{
    public const string Channel = "SensEssentials";

    public static void Debug(string message) => Write(EssentialsLogLevel.Debug, Channel, message, null);

    public static void Info(string message) => Write(EssentialsLogLevel.Info, Channel, message, null);

    public static void Warn(string message) => Write(EssentialsLogLevel.Warn, Channel, message, null);

    public static void Error(string message) => Write(EssentialsLogLevel.Error, Channel, message, null);

    public static void Error(Exception exception, string message) => Write(EssentialsLogLevel.Error, Channel, message, exception);

    public static void Write(EssentialsLogLevel level, string channel, string message, Exception? exception)
    {
        try
        {
            EssentialsCore.HostOrNull?.Log(level, channel, message, exception);
        }
        catch (Exception)
        {
            // Logging must never take anything down with it.
        }
    }
}
