using System.Numerics;

using Arch.Core;

using BattleCity.Core.Collision;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Core.Tests;

public class RespawnNetworkTests
{
    [Fact]
    public void ReportRespawnEventsToNetwork_QueuesRespawnAtCommandCenter()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(CityLayoutTestHelper.CreateMinimalLayout());
        simulation.ReportRespawnEventsToNetwork = true;

        var entity = simulation.CreateNetworkPlayerEntity(new Vector2(100f, 200f), playerId: 1);
        ref var life = ref simulation.World.Get<TankLifeState>(entity);
        life.IsDead = true;
        life.RespawnTimerSeconds = 0f;

        simulation.Tick(0.016f);

        Assert.True(simulation.TryConsumeNetworkRespawnEvent(out var respawnEvent));
        Assert.Equal(1, respawnEvent.PlayerId);
        Assert.False(simulation.World.Get<TankLifeState>(entity).IsDead);
        Assert.Equal(GameConstants.MaxHealth, simulation.World.Get<Health>(entity).Current);
        Assert.False(simulation.TryConsumeNetworkRespawnEvent(out _));
    }

    [Fact]
    public void LocalPlayer_RespawnsOptimisticallyWhenTimerExpires()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.SuppressLocalPlayerRespawn = true;

        var spawn = new Vector2(48f, 96f);
        var entity = simulation.CreatePlayerEntity(spawn);
        ref var life = ref simulation.World.Get<TankLifeState>(entity);
        life.IsDead = true;
        life.RespawnTimerSeconds = 0f;
        simulation.World.Get<Health>(entity).Current = 0;
        simulation.World.Get<Collider>(entity).Layer = CollisionLayer.None;

        simulation.Tick(0.016f);

        Assert.False(simulation.World.Get<TankLifeState>(entity).IsDead);
        Assert.Equal(GameConstants.MaxHealth, simulation.World.Get<Health>(entity).Current);
        Assert.Equal(CollisionLayer.Player, simulation.World.Get<Collider>(entity).Layer);
    }

    [Fact]
    public void ApplyNetworkWarp_RespawnsLocalPlayer()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();

        var entity = simulation.CreatePlayerEntity(new Vector2(10f, 10f));
        ref var life = ref simulation.World.Get<TankLifeState>(entity);
        life.IsDead = true;
        life.RespawnTimerSeconds = 0f;
        simulation.World.Get<Health>(entity).Current = 0;

        simulation.ApplyNetworkWarp(new ServerStateGamePacket(x: 500, y: 600, city: 2));

        ref var transform = ref simulation.World.Get<Transform2D>(entity);
        Assert.False(simulation.World.Get<TankLifeState>(entity).IsDead);
        Assert.Equal(new Vector2(500f, 600f), transform.Position);
        Assert.Equal(GameConstants.MaxHealth, simulation.World.Get<Health>(entity).Current);
        Assert.Equal(2, simulation.World.Get<CityAffiliation>(entity).CityId);
    }

    [Fact]
    public void ApplyPlayerDeath_AppliesToLocalPlayerWhenIdsMatch()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.CreatePlayerEntity(Vector2.Zero);

        simulation.ApplyPlayerDeath(new ServerDeathPacket(playerId: 1, deathType: 0, killerCity: 2), localPlayerId: 1);

        var query = new QueryDescription().WithAll<InputControlled, TankLifeState>();
        simulation.World.Query(
            in query,
            (ref TankLifeState life) => Assert.True(life.IsDead));
    }

    [Fact]
    public void DeferRemotePlayerRespawn_KeepsRemoteTankDeadUntilRespawnPacket()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.DeferRemotePlayerRespawn = true;

        var entity = simulation.CreateNetworkPlayerEntity(new Vector2(100f, 200f), playerId: 2);
        ref var life = ref simulation.World.Get<TankLifeState>(entity);
        life.IsDead = true;
        life.RespawnTimerSeconds = 0f;
        simulation.World.Get<Collider>(entity).Layer = CollisionLayer.None;

        simulation.Tick(0.016f);

        Assert.True(simulation.World.Get<TankLifeState>(entity).IsDead);
        Assert.Equal(CollisionLayer.None, simulation.World.Get<Collider>(entity).Layer);
    }

    [Fact]
    public void ApplyPlayerDeath_DoesNotRestartLocalRespawnTimerWhenAlreadyDead()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        var entity = simulation.CreatePlayerEntity(Vector2.Zero);
        ref var life = ref simulation.World.Get<TankLifeState>(entity);
        life.IsDead = true;
        life.RespawnTimerSeconds = 0.5f;

        simulation.ApplyPlayerDeath(new ServerDeathPacket(playerId: 1, deathType: 0, killerCity: 0), localPlayerId: 1);

        Assert.Equal(0.5f, life.RespawnTimerSeconds);
    }

    [Fact]
    public void ApplyPlayerDeath_OnlineClientClearsPlaceablesWithoutRespawningThem()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.ReturnInventoryPlaceablesOnDeath = false;
        simulation.ReportFactoryItemSpawnsToNetwork = false;
        var entity = simulation.CreatePlayerEntity(Vector2.Zero);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(entity);
        inventory.Wall = 3;
        inventory.Turret = 2;

        simulation.ApplyPlayerDeath(new ServerDeathPacket(playerId: 1, deathType: 0, killerCity: 2), localPlayerId: 1);

        Assert.Equal(0, inventory.Wall);
        Assert.Equal(0, inventory.Turret);
        Assert.False(simulation.TryConsumeFactoryAddItem(out _));
    }
}

internal static class CityLayoutTestHelper
{
    public static BattleCity.Core.Levels.CityLayout CreateMinimalLayout() =>
        new()
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings =
            [
                new BattleCity.Core.Levels.CityBuildingPlacement(0, 10, 10, 0),
            ],
        };
}
