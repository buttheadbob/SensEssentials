using System.Collections.Generic;
using Torch.Commands;
using VRage.Game.ModAPI;

namespace SensEssentials.TorchHost.Commands;

/// <summary>
/// Base for the Torch command modules. They only declare the commands; the bodies are in the shared Actions
/// classes, which get the Torch context through <see cref="Call"/>.
/// </summary>
public abstract class EssentialsModule : CommandModule
{
    protected ICommandContext Call => new TorchCommandContext(Context);

    private sealed class TorchCommandContext(CommandContext context) : ICommandContext
    {
        public IReadOnlyList<string> Args => context.Args;

        public string RawArgs => context.RawArgs;

        public IMyPlayer? Player => context.Player;

        public bool SentBySelf => context.SentBySelf;

        public void Respond(string message) => context.Respond(message);
    }
}
