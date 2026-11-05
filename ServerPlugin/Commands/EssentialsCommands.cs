using System.Collections.Generic;
using PluginSdk.Commands;
using Sandbox.Game.World;
using VRage.Game.ModAPI;

namespace SensEssentials.MagnetarHost.Commands;

/// <summary>
/// Base for the Magnetar command modules. They only declare the commands; the bodies are in the shared Actions
/// classes, which get the Magnetar context through <see cref="Call"/>.
/// </summary>
public abstract class EssentialsCommands : CommandModule
{
    protected ICommandContext Call => new MagnetarCommandContext(Context);

    private sealed class MagnetarCommandContext(CommandContext context) : ICommandContext
    {
        private IMyPlayer? _player;
        private bool _resolved;

        public IReadOnlyList<string> Args => context.Args;

        public string RawArgs => context.RawArgs;

        public IMyPlayer? Player
        {
            get
            {
                var players = MySession.Static?.Players;
                if (!_resolved && !context.Caller.IsConsole && players != null
                    && players.TryGetPlayerById(new MyPlayer.PlayerId(context.Caller.SteamId, 0), out var player))
                    _player = player;

                _resolved = true;
                return _player;
            }
        }

        public bool SentBySelf => context.Caller.IsConsole;

        public void Respond(string message) => context.Respond(message);
    }
}
