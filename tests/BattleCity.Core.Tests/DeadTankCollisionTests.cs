using System.Numerics;

using Arch.Core;

using BattleCity.Core.Collision;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;

using Xunit;

namespace BattleCity.Core.Tests;

public class DeadTankCollisionTests
{
    [Fact]
    public void ApplyDamageToTargets_SkipsDeadTanks()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();

        var shooter = simulation.CreatePlayerEntity(new Vector2(0f, 0f));
        var victim = simulation.CreateNetworkPlayerEntity(new Vector2(48f, 0f), playerId: 2);
        ref var life = ref simulation.World.Get<TankLifeState>(victim);
        life.IsDead = true;
        simulation.World.Get<Health>(victim).Current = 0;

        var bullet = simulation.World.Create(
            new Transform2D { Position = new Vector2(48f, 0f), PreviousPosition = new Vector2(48f, 0f) },
            new Collider
            {
                OffsetX = 0,
                OffsetY = 0,
                Width = GameConstants.TileSize,
                Height = GameConstants.TileSize,
                Layer = CollisionLayer.None,
            },
            new BulletRef { Owner = shooter },
            new Damage { Value = 10 },
            new Lifetime { Remaining = 1f });

        BulletCollisionSystem.Resolve(simulation.World, simulation.TileMap);

        Assert.True(simulation.World.IsAlive(bullet));
    }

    [Fact]
    public void IntersectsAnyEntity_IgnoresDeadTanks()
    {
        using var world = World.Create();
        var mover = world.Create(
            new Transform2D { Position = Vector2.Zero, PreviousPosition = Vector2.Zero },
            new Collider
            {
                OffsetX = 0,
                OffsetY = 0,
                Width = GameConstants.TileSize,
                Height = GameConstants.TileSize,
                Layer = CollisionLayer.Player,
            });

        world.Create(
            new Transform2D { Position = new Vector2(48f, 0f), PreviousPosition = new Vector2(48f, 0f) },
            new Collider
            {
                OffsetX = 0,
                OffsetY = 0,
                Width = GameConstants.TileSize,
                Height = GameConstants.TileSize,
                Layer = CollisionLayer.Player,
            },
            new TankLifeState { IsDead = true });

        var bounds = AxisAlignedBox.FromCollider(Vector2.Zero, world.Get<Collider>(mover));

        Assert.False(CollisionQueries.IntersectsAnyEntity(world, mover, bounds, CollisionLayer.Player));

        world.Dispose();
    }
}
