using SensEssentials.Commands;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace SensEssentials.TorchHost.Commands;

[Category("entities")]
public class EntityModule : EssentialsModule
{
    private EntityActions Actions => new(Call);

    [Command("refresh", "Resyncs all entities for the player running the command.")]
    [Permission(MyPromoteLevel.None)]
    public void Refresh() => Actions.Refresh();

    [Command("stop", "Stops an entity from moving")]
    [Permission(MyPromoteLevel.Admin)]
    public void Stop(string entityName) => Actions.Stop(entityName);

    [Command("delete", "Delete an entity.")]
    [Permission(MyPromoteLevel.Admin)]
    public void Delete(string entityName) => Actions.Delete(entityName);

    [Command("kill", "kill a player.")]
    [Permission(MyPromoteLevel.Admin)]
    public void Kill(string playerName) => Actions.Kill(playerName);

    [Command("find", "Find entities with the given text in their name.")]
    [Permission(MyPromoteLevel.Admin)]
    public void Find(string name) => Actions.Find(name);

    [Command("poweroff", "Turns off every power-producing block (reactors, batteries, solar, wind, engines, ...) on a grid and everything attached by rotors, hinges and pistons. Usage: poweroff <grid> [includeConnectorDocked]")]
    [Permission(MyPromoteLevel.Admin)]
    public void PowerOff(string name, bool includeConnectorDocked = false) => Actions.PowerOff(name, includeConnectorDocked);

    [Command("poweron", "Turns on every power-producing block (reactors, batteries, solar, wind, engines, ...) on a grid and everything attached by rotors, hinges and pistons. Usage: poweron <grid> [includeConnectorDocked]")]
    [Permission(MyPromoteLevel.Admin)]
    public void PowerOn(string name, bool includeConnectorDocked = false) => Actions.PowerOn(name, includeConnectorDocked);

    [Command("eject", "Ejects a specific player from any block they are seated in, or all players in the server if run with 'all'")]
    public void Eject(string playerName) => Actions.Eject(playerName);
}
