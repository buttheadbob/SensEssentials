using SensEssentials.Commands;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace SensEssentials.TorchHost.Commands;

[Category("voxels")]
public class VoxelModule : EssentialsModule
{
    private VoxelActions Actions => new(Call);

    [Command("reset all", "Resets all voxel maps.")]
    public void ResetAll(bool deleteStorage = false) => Actions.ResetAll(deleteStorage);

    [Command("cleanup asteroids", "Resets all asteroids that don't have a grid or player nearby.")]
    public void CleanupAsteroids(bool deleteStorage = false) => Actions.CleanupAsteroids(deleteStorage);

    [Command("cleanup distant", "Resets all asteroids that don't have a grid or player inside the specified radius.")]
    public void CleanupAsteroidsDistant(double distance = 1000, bool deleteStorage = false) => Actions.CleanupAsteroidsDistant(distance, deleteStorage);

    [Command("reset planets", "Resets all planets.")]
    public void ResetPlanets() => Actions.ResetPlanets();

    [Command("reset planet", "Resets the planet with a given name.")]
    public void ResetPlanet(string planetName) => Actions.ResetPlanet(planetName);

    [Command("reset area", "Resets voxel damange in specified radius from player")]
    [Permission(MyPromoteLevel.Admin)]
    public void ResetVoxelArea(float radius) => Actions.ResetVoxelArea(radius);

    [Command("reset gps", "Resets voxel damange in specified radius from given point. Usage: !voxels reset gps <x y z | x:y:z | GPS:Name:x:y:z:> <radius>")]
    [Permission(MyPromoteLevel.Admin)]
    public void ResetVoxelGps() => Actions.ResetVoxelGps();
}
