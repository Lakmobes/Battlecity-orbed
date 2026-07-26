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
    public void AngleDegreesToHeadOrientation_PointsWhereBulletFiresOnCompassSheet()
    {
        // Column 0 of TurretHead is north; legacy aim 0 fires south → frame 8.
        Assert.Equal(8, TurretTargeting.AngleDegreesToHeadOrientation(0f));
        Assert.Equal(12, TurretTargeting.AngleDegreesToHeadOrientation(90f));
        Assert.Equal(0, TurretTargeting.AngleDegreesToHeadOrientation(180f));
        Assert.Equal(4, TurretTargeting.AngleDegreesToHeadOrientation(270f));

        var fireDir = TurretTargeting.AngleDegreesToLegacyDirection(61.875f);
        Assert.Equal((fireDir / 2 + 8) % 16, TurretTargeting.AngleDegreesToHeadOrientation(61.875f));
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
    public void Sleeper_DoesNotBurnAtFullHealth()
    {
        using var world = World.Create();
        var sleeper = GameplayEntityFactory.CreatePlacedItem(world, ItemType.Sleeper, 10, 10);
        ref var state = ref world.Get<TurretState>(sleeper);
        ref var health = ref world.Get<Health>(sleeper);
        state.StartupDelaySeconds = 0f;
        state.TurnCooldownSeconds = 0f;
        state.AnimationCooldownSeconds = 0f;

        Assert.Equal(GameConstants.SleeperTurretMaxHealth, health.Current);
        Assert.False(TurretStats.IsBurning(ItemType.Sleeper, health.Current));

        TurretAiSystem.Update(world, GameSimulation.FixedDeltaSeconds);
        Assert.Equal(0, state.AnimationFrame);

        health.Current = GameConstants.SleeperTurretMaxHealth - 1;
        Assert.True(TurretStats.IsBurning(ItemType.Sleeper, health.Current));
        TurretAiSystem.Update(world, GameSimulation.FixedDeltaSeconds);
        Assert.InRange(state.AnimationFrame, 1, 2);
    }

    [Fact]
    public void TurretAiSystem_DoesNotFireFasterThanTurnIntervalWhileBulletLives()
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

        // Far target so the bullet stays alive across several turn intervals.
        CreateEnemyTank(simulation.World, new System.Numerics.Vector2(20 * 48f, 26 * 48f), cityId: 1);

        var shots = 0;
        var elapsed = 0f;
        while (elapsed < 0.9f)
        {
            var before = CountBulletsOwnedBy(simulation.World, turret);
            simulation.Tick(GameSimulation.FixedDeltaSeconds);
            var after = CountBulletsOwnedBy(simulation.World, turret);
            if (after > before)
            {
                shots++;
            }

            elapsed += GameSimulation.FixedDeltaSeconds;
        }

        // Legacy cadence: attempt every 250ms and only fire when the prior bullet is gone.
        // With a long-lived laser, expect at most 2 shots in 0.9s (not ~3 from bullet-lifetime spam).
        Assert.InRange(shots, 1, 2);
    }

    [Fact]
    public void BulletCollision_TurretDoesNotDamageItself()
    {
        using var simulation = new GameSimulation();
        var turret = GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Turret,
            15,
            15,
            cityId: 0);
        ref var health = ref simulation.World.Get<Health>(turret);
        var maxHealth = health.Current;

        var bullet = GameplayEntityFactory.CreateBullet(
            simulation.World,
            BulletKind.Laser,
            new System.Numerics.Vector2(15 * 48f + 20f, 15 * 48f + 20f),
            direction: 0,
            owner: turret);
        ref var bulletRef = ref simulation.World.Get<BulletRef>(bullet);
        bulletRef.CollisionGraceSeconds = 0f;

        BulletCollisionSystem.Resolve(simulation.World, simulation.TileMap);

        health = ref simulation.World.Get<Health>(turret);
        Assert.Equal(maxHealth, health.Current);
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

    private static int CountBulletsOwnedBy(World world, Entity owner)
    {
        var count = 0;
        var query = new QueryDescription().WithAll<BulletRef>();
        world.Query(in query, (ref BulletRef bullet) =>
        {
            if (bullet.Owner == owner)
            {
                count++;
            }
        });
        return count;
    }
}
