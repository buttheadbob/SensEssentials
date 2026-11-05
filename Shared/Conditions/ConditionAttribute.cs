using System;

namespace SensEssentials;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ConditionAttribute : Attribute
{
    public string Command;
    public string? InvertCommand;
    public string? HelpText;

    /// <summary>
    /// Name of a static method on the same class that turns the argument into the condition's value once, before
    /// any grid is checked. It throws ArgumentException when the argument is wrong, which stops the whole scan.
    /// </summary>
    public string? Prepare;

    public ConditionAttribute(string command, string? invertCommand = null, string? helpText = null)
    {
        Command = command;
        InvertCommand = invertCommand;
        HelpText = helpText;
    }
}