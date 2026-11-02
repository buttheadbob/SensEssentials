using System;
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

    void SaveConfig();

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
    /// Tells clients that these voxel maps were reset, so they reload them. Hosts without a client mod do nothing.
    /// </summary>
    void NotifyVoxelsReset(long[] entityIds);
}

public static class EssentialsCore
{
    public const string Name = "Sens Essentials";

    /// <summary>
    /// Set by the host plugin in Init, before any command, patch or timer can run.
    /// </summary>
    public static IEssentialsHost Host { get; set; } = null!;

    public static EssentialsConfig Config => Host.Config;
}

/// <summary>
/// Log levels in NLog's order, so (int)level equals NLog's LogLevel.Ordinal.
/// </summary>
public enum EssentialsLogLevel
{
    Trace,
    Debug,
    Info,
    Warn,
    Error,
    Fatal
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

    public static void Warn(Exception exception, string message) => Write(EssentialsLogLevel.Warn, Channel, message, exception);

    public static void Error(string message) => Write(EssentialsLogLevel.Error, Channel, message, null);

    public static void Error(Exception exception, string message) => Write(EssentialsLogLevel.Error, Channel, message, exception);

    public static void Write(EssentialsLogLevel level, string channel, string message, Exception? exception)
    {
        try
        {
            EssentialsCore.Host?.Log(level, channel, message, exception);
        }
        catch (Exception)
        {
            // Logging must never take anything down with it.
        }
    }
}
