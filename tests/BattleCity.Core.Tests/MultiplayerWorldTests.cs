using Arch.Core;

using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;

using Xunit;

namespace BattleCity.Core.Tests;

public class MultiplayerWorldTests
{
    [Fact]
    public void LoadMultiplayerWorld_SpawnsCommandCentersOnly_NoDemoLayoutBuildings()
    {
        var mapPath = FindLegacyMapDat();
        if (mapPath is null)
        {
            return;
        }

        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.LoadFromLegacyMapDat(mapPath);

        var ccCount = simulation.LoadMultiplayerWorld();

        Assert.True(ccCount >= 2);
        Assert.Equal(ccCount, simulation.CountBuildingsInWorld());
        Assert.Null(simulation.LoadedCity);

        // Every building must be a command center with a valid city id.
        var buildingQuery = new QueryDescription().WithAll<BuildingRef>();
        simulation.World.Query(
            in buildingQuery,
            (ref BuildingRef building) =>
            {
                Assert.True(BuildingCatalog.IsCommandCenter(building.TypeCode));
                Assert.True(CityCatalog.IsValidCityId(building.CityId));
            });
    }

    [Fact]
    public void LoadMultiplayerWorld_SeedsCityBuildsForEachCommandCenter()
    {
        var mapPath = FindLegacyMapDat();
        if (mapPath is null)
        {
            return;
        }

        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.LoadFromLegacyMapDat(mapPath);
        var ccCount = simulation.LoadMultiplayerWorld();

        Assert.Equal(ccCount, simulation.EnumerateCityBuildIds().Count());

        Assert.True(simulation.TryGetCityBuild(27, out var ba));
        Assert.Equal(1, ba.CurrentBuildingCount);
        Assert.Equal(1, ba.CanBuild[1]); // House starter permission
        Assert.True(simulation.TryGetCityRespawnPosition(27, out var spawn27, out _));
        Assert.True(simulation.TryGetCityRespawnPosition(0, out var spawn0, out _));
        Assert.NotEqual(spawn27, spawn0);
    }

    [Fact]
    public void LoadMultiplayerWorld_DoesNotTagBuenosAiresBuildingsOntoOtherCities()
    {
        var mapPath = FindLegacyMapDat();
        if (mapPath is null)
        {
            return;
        }

        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.LoadFromLegacyMapDat(mapPath);
        simulation.LoadMultiplayerWorld();

        // Contrast: demo layout would place many non-CC buildings all under BA.
        var nonCc = 0;
        var buildingQuery = new QueryDescription().WithAll<BuildingRef>();
        simulation.World.Query(
            in buildingQuery,
            (ref BuildingRef building) =>
            {
                if (!BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    nonCc++;
                }
            });

        Assert.Equal(0, nonCc);
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
