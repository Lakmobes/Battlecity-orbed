using System.Numerics;

using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;
using BattleCity.Server;

using Xunit;

namespace BattleCity.Core.Tests;

public class AiCityControllerTests
{
    [Fact]
    public void TryEnable_SpawnsMayorAndSoldiersInFreeCity()
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

        Assert.True(controller.TryEnable(
            simulation,
            cities,
            mayors,
            allocatePlayerId: () => nextId++,
            releasePlayerId: _ => { },
            humanPlayers: Array.Empty<(byte, byte, bool)>(),
            out var message));

        Assert.Contains("AI City enabled", message);
        Assert.True(controller.IsEnabled);
        Assert.Equal(AiCityController.DefaultSoldierCount + 1, controller.Bots.Count);
        Assert.True(mayors.HasMayor(controller.CityId));
        Assert.Contains(controller.Bots, bot => bot.IsMayor && bot.Role == BotRoles.Defend);
        Assert.Contains(controller.Bots, bot => !bot.IsMayor && bot.Role == BotRoles.Attack);

        foreach (var bot in controller.Bots)
        {
            Assert.True(simulation.TryGetNetworkPlayerSnapshot(bot.PlayerId, out _));
            Assert.True(simulation.TryGetNetworkPlayerEntity(bot.PlayerId, out var entity));
            Assert.True(simulation.World.Has<BotController>(entity));
        }
    }

    [Fact]
    public void TryEnable_FailsWhenAlreadyEnabled()
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
        byte Allocate() => nextId++;

        Assert.True(controller.TryEnable(
            simulation, cities, mayors, Allocate, _ => { },
            Array.Empty<(byte, byte, bool)>(), out _));

        Assert.False(controller.TryEnable(
            simulation, cities, mayors, Allocate, _ => { },
            Array.Empty<(byte, byte, bool)>(), out var message));
        Assert.Contains("already enabled", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Disable_RemovesBotsAndMayor()
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

        Assert.True(controller.TryEnable(
            simulation, cities, mayors,
            () => nextId++,
            released.Add,
            Array.Empty<(byte, byte, bool)>(),
            out _));

        var cityId = controller.CityId;
        var botIds = controller.Bots.Select(bot => bot.PlayerId).ToList();

        controller.Disable(simulation, mayors, released.Add);

        Assert.False(controller.IsEnabled);
        Assert.False(mayors.HasMayor(cityId));
        Assert.Equal(botIds.Count, released.Count);
        foreach (var id in botIds)
        {
            Assert.False(simulation.TryGetNetworkPlayerSnapshot(id, out _));
        }
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
        var mayors = new CityMayorRegistry();
        var controller = new AiCityController();
        var nextId = (byte)1;

        Assert.True(controller.TryEnable(
            simulation, cities, mayors,
            () => nextId++,
            _ => { },
            Array.Empty<(byte, byte, bool)>(),
            out _));

        var humanId = nextId++;
        var humanCity = (byte)((controller.CityId + 1) % 64);
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
