using BattleCity.Core.City;
using BattleCity.Core.Collision;
using BattleCity.Core.Ecs;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Core.Tests;

public class CommandCenterSpawnTests
{
    [Fact]
    public void GetDrivePlatformSpawnPosition_PlacesTankOnSouthernRow()
    {
        var spriteTopLeft = BuildingPlacement.GridAnchorToWorldPosition(12, 14);
        var spawn = CommandCenterLookup.GetDrivePlatformSpawnPosition(spriteTopLeft);
        var platform = BuildingCollision.GetDrivePlatformBounds(spriteTopLeft);

        Assert.InRange(spawn.X, platform.Left, platform.Right - GameConstants.TileSize);
        Assert.InRange(spawn.Y, platform.Top, platform.Bottom - GameConstants.TileSize);
        Assert.Equal(platform.Top + (platform.Height - GameConstants.TileSize) / 2f, spawn.Y);
    }

    [Fact]
    public void GetHomeReference_MatchesDrivePlatformTankCenter()
    {
        var spriteTopLeft = BuildingPlacement.GridAnchorToWorldPosition(20, 22);
        var spawn = CommandCenterLookup.GetDrivePlatformSpawnPosition(spriteTopLeft);
        var home = CommandCenterLookup.GetHomeReferenceFromSpawnTopLeft(spawn);

        Assert.Equal(spawn.X + GameConstants.TileSize / 2f, home.X);
        Assert.Equal(spawn.Y + GameConstants.TileSize / 2f, home.Y);
    }

    [Fact]
    public void TryGetHomeReferenceWorldPosition_UsesHomeCommandCenterFilter()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "HomeRefCity",
            SourcePath = "test.city",
            Buildings =
            [
                new CityBuildingPlacement(0, 40, 40, 0),
            ],
        });

        // Second CC elsewhere on the map (city 1) — must not be used as home.
        LevelLoader.SpawnCommandCenter(simulation.World, 10, 10, cityId: 1);

        Assert.True(simulation.TryGetCityBuild(0, out var homeCity));
        Assert.True(CommandCenterLookup.TryGetHomeReferenceWorldPosition(
            simulation.World,
            homeCity.CommandCenterGridX,
            homeCity.CommandCenterGridY,
            out var homeReference));

        Assert.True(CommandCenterLookup.TryGetRespawnPosition(
            simulation.World,
            homeCity.CommandCenterGridX,
            homeCity.CommandCenterGridY,
            out var spawn));

        var expected = CommandCenterLookup.GetHomeReferenceFromSpawnTopLeft(spawn);
        Assert.Equal(expected.X, homeReference.X, precision: 2);
        Assert.Equal(expected.Y, homeReference.Y, precision: 2);

        var otherHome = CommandCenterLookup.GetHomeReferenceFromSpawnTopLeft(
            CommandCenterLookup.GetRespawnPositionFromGridAnchor(10, 10));
        Assert.NotEqual(otherHome.X, homeReference.X);
    }

    [Fact]
    public void FindOpenTankSpawnNear_SkipsTileBlockedByWall()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "SpawnCity",
            SourcePath = "test.city",
            Buildings =
            [
                new CityBuildingPlacement(0, 20, 20, 0),
            ],
        });

        Assert.True(simulation.TryGetCityRespawnPosition(0, out var preferred, out _));

        var wallGridX = (int)(preferred.X / GameConstants.TileSize);
        var wallGridY = (int)(preferred.Y / GameConstants.TileSize);
        GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Wall,
            wallGridX,
            wallGridY,
            active: true);

        var open = simulation.FindOpenTankSpawnNear(preferred);
        var openGridX = (int)(open.X / GameConstants.TileSize);
        var openGridY = (int)(open.Y / GameConstants.TileSize);
        Assert.False(openGridX == wallGridX && openGridY == wallGridY);
    }

    [Fact]
    public void TryGetCityRespawnPosition_UsesDifferentCommandCentersPerCity()
    {
        var mapPath = FindLegacyMapDat();
        if (mapPath is null)
        {
            return; // optional asset; skip when not present in CI layouts
        }

        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.LoadFromLegacyMapDat(mapPath);
        simulation.LoadCityLayout(LevelLoader.LoadLegacyCity("Buenos Aires", "demo"));

        Assert.True(simulation.TryGetCityRespawnPosition(0, out var city0, out _));
        Assert.True(simulation.TryGetCityRespawnPosition(1, out var city1, out _));
        Assert.True(simulation.TryGetCityRespawnPosition(27, out var city27, out _));

        Assert.NotEqual(city0, city1);
        Assert.NotEqual(city0, city27);
        Assert.NotEqual(city1, city27);
    }

    private static string? FindLegacyMapDat()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "legacy", "data", "map.dat");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }
}
