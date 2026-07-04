using System.Numerics;

using Arch.Core;

using BattleCity.Core.Collision;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Core.Tests;

public class GameplayEntityTests
{
    [Fact]
    public void BulletVelocity_UsesLegacySpeedCap()
    {
        var velocity = BulletSystem.ComputeBulletVelocity(BulletKind.Laser, 0, 0.016f);
        Assert.True(velocity.LengthSquared() > 0f);
        Assert.True(MathF.Abs(velocity.Y) <= 20f / 0.016f);
    }

    [Fact]
    public void WeaponSystem_SpawnsLaserWhenFireHeld()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(new Vector2(100f, 100f));
        ref var input = ref simulation.World.Get<InputCommand>(player);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(player);
        input.FireHeld = true;
        inventory.Rocket = 0;

        simulation.Tick(GameSimulation.FixedDeltaSeconds);

        var bulletQuery = new QueryDescription().WithAll<BulletRef>();
        var count = 0;
        simulation.World.Query(in bulletQuery, (ref BulletRef bullet) =>
        {
            count++;
            Assert.Equal(BulletKind.Laser, bullet.Kind);
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public void WeaponSystem_FiresRocketOnlyWhenStopped()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(new Vector2(100f, 100f));
        ref var input = ref simulation.World.Get<InputCommand>(player);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(player);
        input.FireHeld = true;
        inventory.Rocket = 2;

        input.Move = 1;
        simulation.Tick(GameSimulation.FixedDeltaSeconds);

        var rocketCount = 0;
        var laserCount = 0;
        simulation.World.Query(new QueryDescription().WithAll<BulletRef>(), (ref BulletRef bullet) =>
        {
            if (bullet.Kind == BulletKind.Rocket)
            {
                rocketCount++;
            }
            else if (bullet.Kind == BulletKind.Laser)
            {
                laserCount++;
            }
        });

        Assert.Equal(0, rocketCount);
        Assert.Equal(1, laserCount);

        input.Move = 0;
        ResetWeaponCooldowns(simulation, player);
        simulation.Tick(GameSimulation.FixedDeltaSeconds);

        rocketCount = 0;
        simulation.World.Query(new QueryDescription().WithAll<BulletRef>(), (ref BulletRef bullet) =>
        {
            if (bullet.Kind == BulletKind.Rocket)
            {
                rocketCount++;
            }
        });

        Assert.Equal(1, rocketCount);
    }

    [Fact]
    public void MineSystem_DetonatesWhenEnemyTankDrivesOver()
    {
        using var simulation = new GameSimulation();
        var mine = GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Mine,
            10,
            10,
            cityId: 0);
        var bot = simulation.CreateBotEntity(new Vector2(10 * 48f, 10 * 48f), cityId: 1);
        ref var health = ref simulation.World.Get<Health>(bot);

        simulation.Tick(GameSimulation.FixedDeltaSeconds);

        Assert.False(simulation.World.IsAlive(mine));
        Assert.True(health.Current < GameConstants.MaxHealth);
    }

    [Fact]
    public void MineSystem_DoesNotDetonateForFriendlyTank()
    {
        using var simulation = new GameSimulation();
        var mine = GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Mine,
            10,
            10,
            cityId: 0);
        simulation.CreatePlayerEntity(new Vector2(10 * 48f, 10 * 48f));

        simulation.Tick(GameSimulation.FixedDeltaSeconds);

        Assert.True(simulation.World.IsAlive(mine));
    }

    [Fact]
    public void BulletCollision_AppliesDamageToPatrolTank()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(new Vector2(100f, 100f));
        var patrol = simulation.CreatePatrolEntity(new Vector2(200f, 100f), Vector2.Zero);

        var bullet = GameplayEntityFactory.CreateBullet(
            simulation.World,
            BulletKind.Laser,
            new Vector2(200f, 100f),
            0,
            player);

        ref var transform = ref simulation.World.Get<Transform2D>(bullet);
        transform.Position = new Vector2(200f, 100f);
        ref var bulletRef = ref simulation.World.Get<BulletRef>(bullet);
        bulletRef.CollisionGraceSeconds = 0f;

        BulletCollisionSystem.Resolve(simulation.World, simulation.TileMap);

        ref var health = ref simulation.World.Get<Health>(patrol);
        Assert.True(health.Current < GameConstants.MaxHealth);
    }

    private static void ResetWeaponCooldowns(GameSimulation simulation, Entity player)
    {
        ref var weapons = ref simulation.World.Get<WeaponState>(player);
        weapons.LaserCooldownSeconds = 0f;
        weapons.RocketCooldownSeconds = 0f;
    }

    [Fact]
    public void CreatePlacedItem_WallHasBlockingCollider()
    {
        using var world = World.Create();
        var item = GameplayEntityFactory.CreatePlacedItem(world, ItemType.Wall, 10, 10);

        Assert.True(world.Has<Collider>(item));
        ref var collider = ref world.Get<Collider>(item);
        Assert.Equal(CollisionLayer.Item, collider.Layer);
    }

    [Fact]
    public void DroppingTurret_PlacesAheadOfTankWithoutBlockingMovement()
    {
        using var simulation = new GameSimulation();
        var tankTopLeft = new Vector2(10 * 48f, 10 * 48f);
        var player = simulation.CreatePlayerEntity(tankTopLeft);
        ref var facing = ref simulation.World.Get<TankFacing>(player);
        facing.Direction = InputSystem.ToSpriteFacing(0);

        Assert.True(ItemDropActions.TryDropForEntity(
            simulation.World,
            player,
            tankTopLeft,
            ItemType.Turret,
            active: true,
            out var gridX,
            out var gridY));
        Assert.False(gridX == 10 && gridY == 10);

        ref var transform = ref simulation.World.Get<Transform2D>(player);
        ref var collider = ref simulation.World.Get<Collider>(player);
        transform.Position = tankTopLeft + new Vector2(6f, 0f);
        var result = CollisionQueries.CheckPlayerCollision(
            simulation.World,
            simulation.TileMap,
            player,
            transform.Position,
            collider);
        Assert.Equal(PlayerCollisionResult.None, result);
    }

    [Fact]
    public void DroppingDefensiveItem_FailsWhenFarFromCity()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });

        Assert.True(simulation.TryGetCityBuild(0, out var build));

        var farTopLeft = new Vector2(5 * 48f, 5 * 48f);
        var player = simulation.CreatePlayerEntity(farTopLeft);

        Assert.False(ItemDropActions.TryDropForEntity(
            simulation.World,
            player,
            farTopLeft,
            ItemType.Turret,
            active: true,
            out _,
            out _,
            cityBuild: build));
    }

    [Fact]
    public void DroppingDefensiveItem_SucceedsNearCommandCenter()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });

        Assert.True(simulation.TryGetCityBuild(0, out var build));

        var ccTileX = build.CommandCenterGridX - 1;
        var ccTileY = build.CommandCenterGridY - 1;
        var nearTopLeft = new Vector2(ccTileX * 48f, ccTileY * 48f);
        var player = simulation.CreatePlayerEntity(nearTopLeft);

        Assert.True(ItemDropActions.TryDropForEntity(
            simulation.World,
            player,
            nearTopLeft,
            ItemType.Wall,
            active: true,
            out _,
            out _,
            cityBuild: build));
    }

    [Fact]
    public void DroppingDefensiveItem_SucceedsNearFriendlyBuilding()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });

        Assert.True(simulation.TryGetCityBuild(0, out var build));
        Assert.True(simulation.TryPlaceBuilding(buildSlot: 2, gridAnchorX: 80, gridAnchorY: 80));

        var nearTopLeft = new Vector2(78 * 48f, 78 * 48f);
        var player = simulation.CreatePlayerEntity(nearTopLeft);

        Assert.True(ItemDropActions.TryDropForEntity(
            simulation.World,
            player,
            nearTopLeft,
            ItemType.Turret,
            active: true,
            out _,
            out _,
            cityBuild: build));
    }

    [Fact]
    public void ItemDropSystem_ConsumesMedKit()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(Vector2.Zero);
        ref var input = ref simulation.World.Get<InputCommand>(player);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(player);
        ref var health = ref simulation.World.Get<Health>(player);

        inventory.MedKit = 1;
        health.Current = 10;
        input.UseMedKitPressed = true;

        ItemDropSystem.Update(simulation.World);

        Assert.Equal(0, inventory.MedKit);
        Assert.Equal(GameConstants.MaxHealth, health.Current);
    }

    [Fact]
    public void WeaponSystem_DoesNotConsumeRocketInventory()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(new Vector2(100f, 100f));
        ref var input = ref simulation.World.Get<InputCommand>(player);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(player);

        input.FireHeld = true;
        inventory.Rocket = 1;

        simulation.Tick(GameSimulation.FixedDeltaSeconds);

        Assert.Equal(1, inventory.Rocket);
    }

    [Fact]
    public void ItemDropSystem_PicksUpItemAtTankCenter()
    {
        using var simulation = new GameSimulation();
        var tankTopLeft = new Vector2(10 * 48f, 10 * 48f);
        var player = simulation.CreatePlayerEntity(tankTopLeft);
        var item = GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.MedKit,
            10,
            10,
            active: false,
            cityId: 0);

        ref var input = ref simulation.World.Get<InputCommand>(player);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(player);
        var medkitsBefore = inventory.MedKit;
        input.PickUpItemPressed = true;

        ItemDropSystem.Update(simulation.World);

        Assert.False(simulation.World.IsAlive(item));
        Assert.Equal(medkitsBefore + 1, inventory.MedKit);
    }

    [Fact]
    public void ItemDropFeedback_ReportsOutOfRangeForDefensiveItem()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });

        Assert.True(simulation.TryGetCityBuild(0, out var build));
        var player = simulation.CreatePlayerEntity(new Vector2(5 * 48f, 5 * 48f));

        var message = ItemDropFeedback.GetFailureMessage(
            simulation.World,
            player,
            new Vector2(5 * 48f, 5 * 48f),
            ItemType.Turret,
            build);

        Assert.Equal(ItemDropFeedback.OutOfRangeMessage, message);
    }

    [Fact]
    public void BulletDamage_RecordsKillerCityOnVictim()
    {
        using var simulation = new GameSimulation();
        var victim = simulation.CreatePlayerEntity(new Vector2(10 * 48f, 10 * 48f));
        var attacker = simulation.CreateNetworkPlayerEntity(new Vector2(12 * 48f, 10 * 48f), playerId: 2, cityId: 3);
        var bullet = GameplayEntityFactory.CreateBullet(
            simulation.World,
            BulletKind.Laser,
            new Vector2(10 * 48f + 24f, 10 * 48f + 24f),
            direction: 0,
            attacker);

        ref var bulletRef = ref simulation.World.Get<BulletRef>(bullet);
        bulletRef.CollisionGraceSeconds = 0f;

        ref var victimHealth = ref simulation.World.Get<Health>(victim);
        victimHealth.Current = 1;

        BulletCollisionSystem.Resolve(
            simulation.World,
            simulation.TileMap,
            applyDamageToNetworkPlayers: true);

        ref var life = ref simulation.World.Get<TankLifeState>(victim);
        Assert.Equal((byte)3, life.KillerCityId);
    }
}
