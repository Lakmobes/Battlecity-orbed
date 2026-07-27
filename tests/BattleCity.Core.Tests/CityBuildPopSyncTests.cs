using BattleCity.Core.Ecs;
using BattleCity.Core.Maps;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Core.Tests;

public class CityBuildPopSyncTests
{
    [Fact]
    public void CollectCanBuildChanges_EmitsWhenSlotChanges()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(BattleCity.Core.Levels.LevelLoader.LoadLegacyCity("Buenos Aires", "demo"));
        var sync = new BattleCity.Server.CityBuildPopSync();
        sync.Reset(simulation, cityId: 0);

        Assert.True(simulation.TryGetCityBuild(0, out var build));
        build.CanBuild[1] = 0;

        var changes = sync.CollectCanBuildChanges(simulation, cityId: 0).ToList();

        Assert.Contains(changes, packet => packet.BuildSlot == 2 && packet.CanBuildState == 0);
    }

    [Fact]
    public void CollectPopulationChanges_EmitsWhenPopulationChanges()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(BattleCity.Core.Levels.LevelLoader.LoadLegacyCity("Buenos Aires", "demo"));
        var sync = new BattleCity.Server.CityBuildPopSync();
        sync.Reset(simulation);

        var buildingId = simulation.CollectBuildingPopulations().First().BuildingId;
        simulation.ApplyNetworkUpdatePop(new ServerUpdatePopPacket(buildingId, 99));

        var changes = sync.CollectPopulationChanges(simulation).ToList();

        Assert.Single(changes);
        Assert.Equal(buildingId, changes[0].BuildingId);
        Assert.Equal(99, changes[0].Population);
    }
}
