using Arch.Core;

using BattleCity.Core.Ai;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Core.Tests;

public class TurretAiTests
{
    [Fact]
    public void TryFindNearestEnemy_PrefersClosestDifferentCity()
    {
        using var world = World.Create();
        var turret = GameplayEntityFactory.CreatePlacedItem(world, ItemType.Turret, 10, 10, cityId: 0);
        var near = CreateEnemyTank(world, new System.Numerics.Vector2(10 * 48f, 11 * 48f), cityId: 1);
        CreateEnemyTank(world, new System.Numerics.Vector2(20 * 48f, 20 * 48f), cityId: 1);

        var center = TurretTargeting.GetTurretWorldCenter(10, 10);
        var found = TurretTargeting.TryFindNearestEnemy(
            world,
            ownerCityId: 0,
            center,
            GameConstants.TurretTargetRangePixels,
            out var target,
            out _);

        Assert.True(found);
        Assert.Equal(near, target);
    }

    [Fact]
    public void AngleDegreesToLegacyDirection_RoundsToThirtyTwoFacings()
    {
        var direction = TurretTargeting.AngleDegreesToLegacyDirection(90f);
        Assert.InRange(direction, 0, TankFacing.DirectionCount - 1);
    }

    [Fact]
    public void TurretAiSystem_FiresAtEnemyAfterStartup()
    {
        using var simulation = new GameSimulation();
        var turret = GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Turret,
            20,
            20,
            cityId: 0);
        ref var turretState = ref simulation.World.Get<TurretState>(turret);
        turretState.StartupDelaySeconds = 0f;
        turretState.TurnCooldownSeconds = 0f;

        CreateEnemyTank(simulation.World, new System.Numerics.Vector2(20 * 48f, 22 * 48f), cityId: 1);

        simulation.Tick(GameSimulation.FixedDeltaSeconds);

        var count = 0;
        var query = new QueryDescription().WithAll<BulletRef>();
        simulation.World.Query(in query, (ref BulletRef bullet) =>
        {
            count++;
            Assert.Equal(turret, bullet.Owner);
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public void ConfigureTurret_StartsWithHealthyAnimationFrame()
    {
        using var world = World.Create();
        var turret = GameplayEntityFactory.CreatePlacedItem(world, ItemType.Turret, 10, 10);

        ref var state = ref world.Get<TurretState>(turret);
        Assert.Equal(0, state.AnimationFrame);
    }

    [Fact]
    public void TurretAiSystem_OnlyBurnsWhenHealthIsLow()
    {
        using var world = World.Create();
        var turret = GameplayEntityFactory.CreatePlacedItem(world, ItemType.Turret, 10, 10);
        ref var state = ref world.Get<TurretState>(turret);
        ref var health = ref world.Get<Health>(turret);
        state.StartupDelaySeconds = 0f;
        state.TurnCooldownSeconds = 0f;
        state.AnimationCooldownSeconds = 0f;

        TurretAiSystem.Update(world, GameSimulation.FixedDeltaSeconds);
        Assert.Equal(0, state.AnimationFrame);

        health.Current = 8;
        TurretAiSystem.Update(world, GameSimulation.FixedDeltaSeconds);
        Assert.InRange(state.AnimationFrame, 1, 2);
    }

    [Fact]
    public void BotAiSystem_MovesTowardPlayer()
    {
        using var simulation = new GameSimulation();
        simulation.CreatePlayerEntity(new System.Numerics.Vector2(300f, 300f));
        var bot = simulation.CreateBotEntity(new System.Numerics.Vector2(500f, 300f), cityId: 1);

        for (var i = 0; i < 30; i++)
        {
            simulation.Tick(GameSimulation.FixedDeltaSeconds);
        }

        ref var transform = ref simulation.World.Get<Transform2D>(bot);
        Assert.True(transform.Position.X < 500f);
    }

    private static Entity CreateEnemyTank(Arch.Core.World world, System.Numerics.Vector2 position, int cityId)
    {
        return world.Create(
            new Transform2D { Position = position, PreviousPosition = position },
            new CityAffiliation { CityId = cityId },
            new Health { Current = GameConstants.MaxHealth, Max = GameConstants.MaxHealth },
            new TankStatus(),
            new Collider
            {
                OffsetX = GameConstants.PlayerCollisionInset,
                OffsetY = GameConstants.PlayerCollisionInset,
                Width = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Height = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Layer = CollisionLayer.Player,
            });
    }
}
