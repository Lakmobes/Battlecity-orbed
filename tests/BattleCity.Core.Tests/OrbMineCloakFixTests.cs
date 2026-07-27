using System.Numerics;

using Arch.Core;

using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Core.Tests;

public sealed class OrbMineCloakFixTests
{
    [Fact]
    public void CityBuildState_GetOrbValue_MatchesLegacyTiers()
    {
        var build = new CityBuildState { MaxBuildingCount = EconomyConstants.OrbableSize + 10, Orbs = 2 };
        Assert.Equal(60, build.GetOrbValue()); // 50 + 10

        build.MaxBuildingCount = EconomyConstants.OrbableSize;
        build.Orbs = 0;
        Assert.Equal(30, build.GetOrbValue());

        build.MaxBuildingCount = 1;
        build.HadOrbFactory = true;
        Assert.Equal(20, build.GetOrbValue());

        build.HadOrbFactory = false;
        build.HadBombFactory = true;
        Assert.Equal(10, build.GetOrbValue());
    }

    [Fact]
    public void OrbSystem_TriggersOnEnemyCommandCenter_NotOwnCity()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();

        var victim = simulation.EnsureCityBuild(1);
        victim.CommandCenterGridX = 40;
        victim.CommandCenterGridY = 40;
        victim.HadBombFactory = true;
        victim.MaxBuildingCount = 5;

        var attacker = simulation.EnsureCityBuild(2);
        attacker.CommandCenterGridX = 10;
        attacker.CommandCenterGridY = 10;

        // Orb owned by attacker sitting on victim CC (legacy CalcY==2, CalcX 0..2).
        GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Orb,
            gridX: 39,
            gridY: 38,
            active: false,
            cityId: 2);

        Assert.True(OrbSystem.TryTrigger(
            simulation.World,
            simulation.EnumerateCityBuildIds().Select(id =>
            {
                simulation.TryGetCityBuild(id, out var build);
                return build;
            }),
            out var victimCityId,
            out var attackerCityId));
        Assert.Equal(1, victimCityId);
        Assert.Equal(2, attackerCityId);
    }

    [Fact]
    public void CityOrbedService_DestroysOnlyVictimCityBuildingsAndItems()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();

        var victim = simulation.EnsureCityBuild(3);
        victim.HadBombFactory = true;
        victim.MaxBuildingCount = 25;
        victim.Orbs = 4;
        victim.CurrentBuildingCount = 8;

        LevelLoader.SpawnBuilding(
            simulation.World,
            new CityBuildingPlacement(13, 50, 50, 104), // Mine Factory
            cityId: 3);
        LevelLoader.SpawnBuilding(
            simulation.World,
            new CityBuildingPlacement(13, 60, 60, 104),
            cityId: 7);

        GameplayEntityFactory.CreatePlacedItem(simulation.World, ItemType.Mine, 51, 51, active: false, cityId: 3);
        GameplayEntityFactory.CreatePlacedItem(simulation.World, ItemType.Mine, 61, 61, active: true, cityId: 7);

        CityOrbedService.ApplyOrbed(simulation.World, victim);

        Assert.Equal(1, victim.CurrentBuildingCount);
        Assert.Equal(1, victim.MaxBuildingCount);
        Assert.False(victim.HadBombFactory);
        Assert.Equal(0, victim.Orbs);

        var victimFactoryGone = true;
        var otherFactoryAlive = false;
        var victimMineGone = true;
        var otherMineAlive = false;
        var query = new QueryDescription().WithAll<BuildingRef>();
        simulation.World.Query(
            in query,
            (ref BuildingRef building) =>
            {
                if (building.CityId == 3 && building.TypeCode == 104)
                {
                    victimFactoryGone = false;
                }

                if (building.CityId == 7 && building.TypeCode == 104)
                {
                    otherFactoryAlive = true;
                }
            });

        var itemQuery = new QueryDescription().WithAll<PlacedItemRef>();
        simulation.World.Query(
            in itemQuery,
            (ref PlacedItemRef item) =>
            {
                if (item.CityId == 3 && item.Type == ItemType.Mine)
                {
                    victimMineGone = false;
                }

                if (item.CityId == 7 && item.Type == ItemType.Mine)
                {
                    otherMineAlive = true;
                }
            });

        Assert.True(victimFactoryGone);
        Assert.True(otherFactoryAlive);
        Assert.True(victimMineGone);
        Assert.True(otherMineAlive);
    }

    [Fact]
    public void DemolishFactory_DeletesMatchingCityProductItems()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        var build = simulation.EnsureCityBuild(4);

        LevelLoader.SpawnBuilding(
            simulation.World,
            new CityBuildingPlacement(13, 30, 30, 104),
            cityId: 4);
        GameplayEntityFactory.CreatePlacedItem(simulation.World, ItemType.Mine, 31, 31, active: false, cityId: 4);
        GameplayEntityFactory.CreatePlacedItem(simulation.World, ItemType.Mine, 32, 32, active: true, cityId: 4);
        GameplayEntityFactory.CreatePlacedItem(simulation.World, ItemType.Dfg, 33, 33, active: false, cityId: 4);

        Assert.True(BuildingCommandService.TryDemolishAt(simulation.World, build, 30, 30));

        var mineCount = 0;
        var dfgCount = 0;
        var itemQuery = new QueryDescription().WithAll<PlacedItemRef>();
        simulation.World.Query(
            in itemQuery,
            (ref PlacedItemRef item) =>
            {
                if (item.Type == ItemType.Mine && item.CityId == 4)
                {
                    mineCount++;
                }

                if (item.Type == ItemType.Dfg && item.CityId == 4)
                {
                    dfgCount++;
                }
            });

        Assert.Equal(0, mineCount);
        Assert.Equal(1, dfgCount);
    }

    [Fact]
    public void ApplyNetworkCloak_SetsRechargeWhenCityHasCloakFactory()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        var build = simulation.EnsureCityBuild(0);
        build.CanBuild[BuildingCatalog.GetFactoryMenuIndex(EconomyConstants.CloakResearchTreeIndex)] = 2;

        simulation.CreatePlayerEntity(Vector2.Zero, cityId: 0);

        Assert.True(simulation.ApplyNetworkCloak(playerId: 1, localPlayerId: 1));

        var query = new QueryDescription().WithAll<InputControlled, WeaponState, TankStatus>();
        simulation.World.Query(
            in query,
            (ref WeaponState weapons, ref TankStatus status) =>
            {
                Assert.True(status.IsCloaked);
                Assert.True(weapons.CloakRechargeSeconds > 0f);
            });
    }

    [Fact]
    public void Tick_OrbsEnemyCity_QueuesOrbEventWithLegacyPoints()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();

        var victim = simulation.EnsureCityBuild(5);
        victim.CommandCenterGridX = 40;
        victim.CommandCenterGridY = 40;
        victim.HadBombFactory = true;
        victim.MaxBuildingCount = EconomyConstants.OrbableSize;

        var attacker = simulation.EnsureCityBuild(6);
        attacker.CommandCenterGridX = 10;
        attacker.CommandCenterGridY = 10;
        attacker.HadOrbFactory = true;

        LevelLoader.SpawnBuilding(
            simulation.World,
            new CityBuildingPlacement(13, 42, 42, 104),
            cityId: 5);

        GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Orb,
            gridX: 39,
            gridY: 38,
            active: false,
            cityId: 6);

        simulation.Tick(GameSimulation.FixedDeltaSeconds);

        Assert.True(simulation.TryConsumeOrbEvent(out var orbEvent));
        Assert.Equal(5, orbEvent.VictimCityId);
        Assert.Equal(6, orbEvent.AttackerCityId);
        Assert.Equal(30u, orbEvent.VictimPoints); // orbable size tier
        Assert.Equal(25u, orbEvent.AttackerPoints); // 20 orb-factory + 5 for Orbs++

        Assert.Equal(1, attacker.Orbs);
        Assert.Equal(0, victim.Orbs);
        Assert.False(victim.HadBombFactory);
        Assert.Equal(1, victim.MaxBuildingCount);

        var victimFactoryGone = true;
        var buildingQuery = new QueryDescription().WithAll<BuildingRef>();
        simulation.World.Query(
            in buildingQuery,
            (ref BuildingRef building) =>
            {
                if (building.CityId == 5 && building.TypeCode == 104)
                {
                    victimFactoryGone = false;
                }
            });
        Assert.True(victimFactoryGone);
    }
}
