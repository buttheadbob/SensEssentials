using PluginSdk.Commands;
using SensEssentials.Commands;
using VRage.Game.ModAPI;

// The grid related command groups: blocks, cleanup, entities and grids.
namespace SensEssentials.MagnetarHost.Commands;

[CommandRoot("blocks", "Sens Essentials", "Turn on, turn off, remove or count blocks")]
public sealed class BlocksCommands : EssentialsCommands
{
    private BlocksActions Actions => new(Call);

    // The selector, value and conditions are read from all the words after the command.
    [Command("on", "Turn on blocks. Usage: !blocks on [type|subtype|general] <value> [conditions...]")]
    [Permission(MyPromoteLevel.Admin)]
    public void On(params string[] args) => Actions.On();

    [Command("off", "Turn off blocks. Usage: !blocks off [type|subtype|general] <value> [conditions...]")]
    [Permission(MyPromoteLevel.Admin)]
    public void Off(params string[] args) => Actions.Off();

    [Command("remove", "Remove blocks. Usage: !blocks remove [type|subtype] <value> [conditions...]")]
    [Permission(MyPromoteLevel.Admin)]
    public void Remove(params string[] args) => Actions.Remove();

    [Command("count", "Count blocks. Usage: !blocks count [type|subtype|general] <value> [conditions...]")]
    [Permission(MyPromoteLevel.Admin)]
    public void Count(params string[] args) => Actions.Count();
}

[CommandRoot("cleanup", "Sens Essentials", "Find and delete grids by conditions")]
public sealed class CleanupCommands : EssentialsCommands
{
    private CleanupActions Actions => new(Call);

    [Command("scan", "Count grids matching the given conditions")]
    [Permission(MyPromoteLevel.Admin)]
    public void Scan(params string[] conditions) => Actions.Scan();

    [Command("list", "List grids matching the given conditions")]
    [Permission(MyPromoteLevel.Admin)]
    public void List(params string[] conditions) => Actions.List();

    [Command("delete", "Delete grids matching the given conditions. 'all' deletes every grid without a pilot.")]
    [Permission(MyPromoteLevel.Admin)]
    public void Delete(params string[] conditions) => Actions.Delete();

    [Command("delete floatingobjects", "Delete all floating objects")]
    [Permission(MyPromoteLevel.Admin)]
    public void DeleteFloatingObjects() => Actions.DeleteFloatingObjects();

    [Command("conditions", "Lists all cleanup conditions")]
    [Permission(MyPromoteLevel.Admin)]
    public void Conditions() => Actions.Conditions();
}

[CommandRoot("entities", "Sens Essentials", "Find, stop, delete and power entities")]
public sealed class EntityCommands : EssentialsCommands
{
    private EntityActions Actions => new(Call);

    [Command("refresh", "Resyncs all entities for the player running the command")]
    [Permission(MyPromoteLevel.None)]
    public void Refresh() => Actions.Refresh();

    [Command("stop", "Stops an entity from moving")]
    [Permission(MyPromoteLevel.Admin)]
    public void Stop(string entityName) => Actions.Stop(entityName);

    [Command("delete", "Delete an entity")]
    [Permission(MyPromoteLevel.Admin)]
    public void Delete(string entityName) => Actions.Delete(entityName);

    [Command("kill", "Kill a player")]
    [Permission(MyPromoteLevel.Admin)]
    public void Kill(string playerName) => Actions.Kill(playerName);

    [Command("find", "Find entities with the given text in their name")]
    [Permission(MyPromoteLevel.Admin)]
    public void Find(string name) => Actions.Find(name);

    [Command("poweroff", "Turns off every power-producing block on a grid and everything attached by rotors, hinges and pistons. Usage: poweroff <grid> [includeConnectorDocked]")]
    [Permission(MyPromoteLevel.Admin)]
    public void PowerOff(string name, bool includeConnectorDocked = false) => Actions.PowerOff(name, includeConnectorDocked);

    [Command("poweron", "Turns on every power-producing block on a grid and everything attached by rotors, hinges and pistons. Usage: poweron <grid> [includeConnectorDocked]")]
    [Permission(MyPromoteLevel.Admin)]
    public void PowerOn(string name, bool includeConnectorDocked = false) => Actions.PowerOn(name, includeConnectorDocked);

    [Command("eject", "Ejects a player from the block they are seated in, or every player with 'all'")]
    [Permission(MyPromoteLevel.Admin)]
    public void Eject(string playerName) => Actions.Eject(playerName);
}

[CommandRoot("grids", "Sens Essentials", "Grid ownership, listing, export and import")]
public sealed class GridCommands : EssentialsCommands
{
    private GridActions Actions => new(Call);

    [Command("setowner", "Sets grid ownership to the given player or identity ID. Usage: setowner <grid> <newowner>")]
    [Permission(MyPromoteLevel.Admin)]
    public void SetOwner(string gridName, string playerName) => Actions.SetOwner(gridName, playerName);

    [Command("ejectall", "Ejects all players from the given grid, or from the grid you are looking at")]
    [Permission(MyPromoteLevel.Admin)]
    public void Eject(string? gridName = null) => Actions.Eject(gridName);

    [Command("static large", "Makes all large grids static")]
    [Permission(MyPromoteLevel.Admin)]
    public void StaticLarge() => Actions.StaticLarge();

    [Command("stopall", "Stops all moving grids")]
    [Permission(MyPromoteLevel.Admin)]
    public void StopAll() => Actions.StopAll();

    [Command("list", "Lists all grids where you own the most blocks")]
    [Permission(MyPromoteLevel.None)]
    public void List() => Actions.List();

    [Command("export", "Export the given grid to the given file name")]
    [Permission(MyPromoteLevel.Admin)]
    public void Export(string gridName, string exportName) => Actions.Export(gridName, exportName);

    [Command("import", "Import a grid from file and spawn it by the given entity or player")]
    [Permission(MyPromoteLevel.Admin)]
    public void Import(string gridName, string? targetName = null) => Actions.Import(gridName, targetName);
}

[CommandRoot("voxels", "Sens Essentials", "Reset asteroids, planets and voxel damage")]
public sealed class VoxelCommands : EssentialsCommands
{
    private VoxelActions Actions => new(Call);

    [Command("reset all", "Resets all voxel maps")]
    [Permission(MyPromoteLevel.Admin)]
    public void ResetAll(bool deleteStorage = false) => Actions.ResetAll(deleteStorage);

    [Command("cleanup asteroids", "Resets all asteroids that don't have a grid or player nearby")]
    [Permission(MyPromoteLevel.Admin)]
    public void CleanupAsteroids(bool deleteStorage = false) => Actions.CleanupAsteroids(deleteStorage);

    [Command("cleanup distant", "Resets all asteroids that don't have a grid or player inside the given radius")]
    [Permission(MyPromoteLevel.Admin)]
    public void CleanupAsteroidsDistant(double distance = 1000, bool deleteStorage = false) => Actions.CleanupAsteroidsDistant(distance, deleteStorage);

    [Command("reset planets", "Resets all planets")]
    [Permission(MyPromoteLevel.Admin)]
    public void ResetPlanets() => Actions.ResetPlanets();

    [Command("reset planet", "Resets the planet with the given name")]
    [Permission(MyPromoteLevel.Admin)]
    public void ResetPlanet(string planetName) => Actions.ResetPlanet(planetName);

    [Command("reset area", "Resets voxel damage in the given radius around you")]
    [Permission(MyPromoteLevel.Admin)]
    public void ResetVoxelArea(float radius) => Actions.ResetVoxelArea(radius);

    [Command("reset gps", "Resets voxel damage in the given radius from a point. Usage: !voxels reset gps <x y z | x:y:z | GPS:Name:x:y:z:> <radius>")]
    [Permission(MyPromoteLevel.Admin)]
    public void ResetVoxelGps(params string[] args) => Actions.ResetVoxelGps();
}
