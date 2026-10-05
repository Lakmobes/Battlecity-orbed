using System.Numerics;

using Arch.Core;

using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;
using BattleCity.Server;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Core.Tests;

public class AiCityControllerTests
{
    [Fact]
    public void Sync_DoesNotSpawnUntilAHumanIsInGame()
    {
        using var simulation = CreateMpSimulation();
        if (simulation is null)
        {
            return;
        }

        var controller = new AiCityController();
        controller.Arm(1, AiCityStance.Balanced);
        var change = controller.Sync(
            simulation,
            new CityRegistry(),
            new CityMayorRegistry(),
            () => 1,
            _ => { },
            Array.Empty<(byte, byte, bool)>());

        Assert.False(controller.IsActive);
        Assert.Empty(change.Spawned);
    }

    [Fact]
    public void Sync_SpawnsMayorAndSoldiersWhenAHumanIsInGame()
    {
        using var simulation = CreateMpSimulation();
        if (simulation is null)
        {
            return;
        }

        var cities = new CityRegistry();
        cities.SetStartingCityForTests(27);
        var mayors = new CityMayorRegistry();
        var controller = new AiCityController();
        var nextId = (byte)1;
        controller.Arm(1, AiCityStance.Balanced);

        var change = controller.Sync(
            simulation,
            cities,
            mayors,
            () => nextId++,
            _ => { },
            [(PlayerId: 200, CityId: 0, InGame: true)]);

        Assert.NotNull(change.Message);
        Assert.Contains("AI cities active", change.Message);
        Assert.True(controller.IsActive);
        Assert.Equal(AiCityController.DefaultSoldierCount + 1, controller.Bots.Count);
        Assert.Single(controller.CityIds);
        Assert.True(mayors.HasMayor(controller.CityIds[0]));
        Assert.Contains(controller.Bots, bot => bot.IsMayor && bot.Role == BotRoles.Defend);
        Assert.Contains(controller.Bots, bot => !bot.IsMayor && bot.Role == BotRoles.Attack);
    }

    [Fact]
    public void TankBlocksRedeploy_WhenTheTankIsOnTheCommandCenter()
    {
        Assert.True(AiCityController.TankBlocksRedeploy(40, 40, 40, 40));
        Assert.True(AiCityController.TankBlocksRedeploy(40, 40, 46, 40));
        Assert.False(AiCityController.TankBlocksRedeploy(40, 40, 47, 40));
    }

    [Fact]
    public void RetireCity_RedeploysTheSameCityAfterThirtySeconds()
    {
        using var simulation = CreateMpSimulation();
        if (simulation is null)
        {
            return;
        }

        var cities = new CityRegistry();
        cities.SetStartingCityForTests(27);
        var mayors = new CityMayorRegistry();
        var controller = new AiCityController();
        var nextId = (byte)1;
        var humans = new (byte PlayerId, byte CityId, bool InGame)[] { (200, 0, true) };

        controller.Arm(1, AiCityStance.Balanced);
        controller.Sync(simulation, cities, mayors, () => nextId++, _ => { }, humans);
        var cityId = controller.CityIds[0];
        Assert.NotEmpty(controller.RetireCity(cityId, simulation, mayors, _ => { }));
        Assert.False(controller.IsActive);

        var immediate = controller.Sync(simulation, cities, mayors, () => nextId++, _ => { }, humans);
        Assert.Empty(immediate.Spawned);
        Assert.False(controller.IsActive);

        controller.AdvanceRedeploy(AiCityController.RedeployDelaySeconds - 0.05f);
        var early = controller.TryRedeployDue(
            simulation, cities, mayors, () => nextId++, _ => { }, humans, _ => false);
        Assert.Empty(early.Spawned);

        controller.AdvanceRedeploy(0.05f);
        var redeployed = controller.TryRedeployDue(
            simulation, cities, mayors, () => nextId++, _ => { }, humans, _ => false);
        Assert.Equal(cityId, Assert.Single(controller.CityIds));
        Assert.NotEmpty(redeployed.Spawned);
        Assert.Contains("redeployed", redeployed.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RetireCity_WaitsWhileAPlayerStandsOnTheCommandCenter()
    {
        using var simulation = CreateMpSimulation();
        if (simulation is null)
        {
            return;
        }

        var cities = new CityRegistry();
        cities.SetStartingCityForTests(27);
        var mayors = new CityMayorRegistry();
        var controller = new AiCityController();
        var nextId = (byte)1;
        var humans = new (byte PlayerId, byte CityId, bool InGame)[] { (200, 0, true) };

        controller.Arm(1, AiCityStance.Balanced);
        controller.Sync(simulation, cities, mayors, () => nextId++, _ => { }, humans);
        var cityId = controller.CityIds[0];
        controller.RetireCity(cityId, simulation, mayors, _ => { });

        controller.AdvanceRedeploy(AiCityController.RedeployDelaySeconds);
        var blocked = controller.TryRedeployDue(
            simulation,
            cities,
            mayors,
            () => nextId++,
            _ => { },
            humans,
            candidate => candidate == cityId);
        Assert.False(controller.IsActive);
        Assert.Contains("in the way", blocked.Message, StringComparison.OrdinalIgnoreCase);

        var stillEarly = controller.TryRedeployDue(
            simulation, cities, mayors, () => nextId++, _ => { }, humans, _ => false);
        Assert.False(controller.IsActive);
        Assert.Null(stillEarly.Message);

        controller.AdvanceRedeploy(AiCityController.RedeployRetrySeconds);
        var clear = controller.TryRedeployDue(
            simulation, cities, mayors, () => nextId++, _ => { }, humans, _ => false);
        Assert.Equal(cityId, Assert.Single(controller.CityIds));
        Assert.NotEmpty(clear.Spawned);
    }

    [Fact]
    public void Sync_SpawnsMultipleCitiesAndStandsDownWhenHumansLeave()
    {
        using var simulation = CreateMpSimulation();
        if (simulation is null)
        {
            return;
        }

        var cities = new CityRegistry();
        cities.SetStartingCityForTests(27);
        var mayors = new CityMayorRegistry();
        var controller = new AiCityController();
        var nextId = (byte)1;
        var released = new List<byte>();
        controller.Arm(2, AiCityStance.LeanDefense);

        controller.Sync(
            simulation, cities, mayors,
            () => nextId++,
            released.Add,
            [(200, 0, true)]);

        Assert.Equal(2, controller.CityIds.Distinct().Count());
        Assert.All(controller.Bots, bot => Assert.Equal(BotRoles.Defend, bot.Role));

        var change = controller.Sync(
            simulation, cities, mayors,
            () => nextId++,
            released.Add,
            Array.Empty<(byte, byte, bool)>());

        Assert.False(controller.IsActive);
        Assert.NotEmpty(change.Removed);
        Assert.Contains("standing down", change.Message);
    }

    [Fact]
    public void Sync_LeanOffenseSendsSoldiersToAttack()
    {
        using var simulation = CreateMpSimulation();
        if (simulation is null)
        {
            return;
        }

        var cities = new CityRegistry();
        cities.SetStartingCityForTests(27);
        var controller = new AiCityController();
        var nextId = (byte)1;
        controller.Arm(1, AiCityStance.LeanOffense);
        controller.Sync(
            simulation, cities, new CityMayorRegistry(),
            () => nextId++,
            _ => { },
            [(200, 0, true)]);

        Assert.All(controller.Bots.Where(bot => bot.IsMayor), bot => Assert.Equal(BotRoles.Defend, bot.Role));
        Assert.All(controller.Bots.Where(bot => !bot.IsMayor), bot => Assert.Equal(BotRoles.Attack, bot.Role));
    }

    [Fact]
    public void RefreshAttackGoals_SetsGoalOnAttackBots()
    {
        using var simulation = CreateMpSimulation();
        if (simulation is null)
        {
            return;
        }

        var cities = new CityRegistry();
        cities.SetStartingCityForTests(27);
        var controller = new AiCityController();
        var nextId = (byte)1;
        controller.Arm(1, AiCityStance.Balanced);
        controller.Sync(
            simulation, cities, new CityMayorRegistry(),
            () => nextId++,
            _ => { },
            [(200, 0, true)]);

        var humanId = (byte)201;
        var humanCity = (byte)((controller.CityIds[0] + 1) % 64);
        simulation.CreateNetworkPlayerEntity(new Vector2(500f, 500f), humanId, humanCity);

        controller.RefreshAttackGoals(
            simulation,
            [(humanId, humanCity, InGame: true)]);

        foreach (var bot in controller.Bots.Where(bot => bot.Role == BotRoles.Attack))
        {
            Assert.True(simulation.TryGetNetworkPlayerEntity(bot.PlayerId, out var entity));
            ref var brain = ref simulation.World.Get<BotController>(entity);
            Assert.True(brain.HasGoal);
        }
    }

    [Fact]
    public void Sync_PlacesAnOrbableBaseAroundTheCommandCenter()
    {
        using var simulation = CreateMpSimulation();
        if (simulation is null)
        {
            return;
        }

        var cities = new CityRegistry();
        cities.SetStartingCityForTests(27);
        var controller = new AiCityController();
        var nextId = (byte)1;
        controller.Arm(1, AiCityStance.Balanced);
        var change = controller.Sync(
            simulation,
            cities,
            new CityMayorRegistry(),
            () => nextId++,
            _ => { },
            [(200, 0, true)]);

        var cityId = controller.CityIds[0];
        Assert.True(simulation.TryGetCityBuild(cityId, out var build));
        Assert.True(build.IsOrbable);
        Assert.True(build.HadBombFactory);
        Assert.True(build.HadOrbFactory);
        Assert.True(build.MaxBuildingCount >= EconomyConstants.OrbableSize);
        Assert.True(build.GetOrbValue() >= 30);
        Assert.Contains(change.Buildings, building => building.City == cityId);
        Assert.Contains(change.Walls, item => item.Type == (byte)ItemType.Wall);
        Assert.Contains(change.Walls, item => item.Type == (byte)ItemType.Turret);
        Assert.Contains(change.Walls, item => item.Type == (byte)ItemType.Orb);

        var typeCodes = BuildingTypeCodes(simulation, cityId);
        Assert.Contains(AiCityTemplate.HouseTypeCode, typeCodes);
        Assert.Contains(AiCityTemplate.HospitalTypeCode, typeCodes);
        Assert.Contains(AiCityTemplate.BombFactoryTypeCode, typeCodes);
        Assert.Contains(AiCityTemplate.OrbFactoryTypeCode, typeCodes);

        var buildingCount = typeCodes.Count;
        var wallCount = CountWalls(simulation, cityId);
        simulation.PlaceAiCityBase(cityId);
        Assert.Equal(buildingCount, BuildingTypeCodes(simulation, cityId).Count);
        Assert.Equal(wallCount, CountWalls(simulation, cityId));
    }

    [Fact]
    public void CreateNetworkBotPlayer_EnqueuesShotWhenFiringAtEnemy()
    {
        using var simulation = new GameSimulation
        {
            TileMap = TileMap.CreateEmpty(),
            NetworkPlayersUseLocalBulletDamage = false,
        };

        simulation.CreateNetworkBotPlayer(
            new Vector2(12 * 48f, 12 * 48f),
            playerId: 1,
            cityId: 0,
            isMayor: false,
            BotRoles.Defend,
            aggroRangePixels: 5000f);
        simulation.CreateNetworkPlayerEntity(new Vector2(12 * 48f + 96f, 12 * 48f), playerId: 2, cityId: 1);

        var sawShot = false;
        for (var i = 0; i < 120; i++)
        {
            simulation.Tick(GameSimulation.FixedDeltaSeconds);
            if (simulation.TryConsumeBotNetworkShot(out var shot))
            {
                Assert.Equal(1, shot.PlayerId);
                sawShot = true;
                break;
            }
        }

        Assert.True(sawShot);
    }

    private static int CountWalls(GameSimulation simulation, int cityId)
    {
        var count = 0;
        var query = new QueryDescription().WithAll<PlacedItemRef>();
        simulation.World.Query(
            in query,
            (ref PlacedItemRef item) =>
            {
                if (item.CityId == cityId && item.Type == ItemType.Wall)
                {
                    count++;
                }
            });
        return count;
    }

    private static List<int> BuildingTypeCodes(GameSimulation simulation, int cityId)
    {
        var typeCodes = new List<int>();
        var query = new QueryDescription().WithAll<BuildingRef>();
        simulation.World.Query(
            in query,
            (ref BuildingRef building) =>
            {
                if (building.CityId == cityId && !BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    typeCodes.Add(building.TypeCode);
                }
            });
        return typeCodes;
    }

    private static GameSimulation? CreateMpSimulation()
    {
        var mapPath = FindLegacyMapDat();
        if (mapPath is null)
        {
            return null;
        }

        var simulation = new GameSimulation
        {
            TileMap = TileMap.LoadFromLegacyMapDat(mapPath),
            NetworkPlayersUseLocalBulletDamage = false,
            NetworkPlayersUseLocalHealthDeath = false,
        };
        simulation.LoadMultiplayerWorld();
        return simulation;
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
