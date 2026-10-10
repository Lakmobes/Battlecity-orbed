using System.Numerics;

using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Levels;
using BattleCity.Shared.Constants;

using Xunit;

namespace BattleCity.Core.Tests;

public class WorldClickTargetTests
{
    [Fact]
    public void RightClick_HitsATankAndABuilding()
    {
        using var simulation = new GameSimulation();
        simulation.CreateNetworkPlayerEntity(new Vector2(10 * 48f, 12 * 48f), playerId: 4, cityId: 1);
        var build = simulation.EnsureCityBuild(1);
        build.CommandCenterGridX = 20;
        build.CommandCenterGridY = 20;
        LevelLoader.SpawnCommandCenter(simulation.World, 20, 20, 1);

        Assert.True(WorldClickTarget.TryFindPlayer(
            simulation.World,
            (10 * 48f) + 8f,
            (12 * 48f) + 8f,
            out var playerId,
            out var isLocal));
        Assert.Equal(4, playerId);
        Assert.False(isLocal);

        var buildingWorldX = (20 - GameConstants.BuildingCollisionOffset) * GameConstants.TileSize + 4f;
        var buildingWorldY = (20 - GameConstants.BuildingCollisionOffset) * GameConstants.TileSize + 4f;
        Assert.True(WorldClickTarget.TryFindBuildingCity(
            simulation.World,
            buildingWorldX,
            buildingWorldY,
            out var cityId));
        Assert.Equal(1, cityId);
    }
}
