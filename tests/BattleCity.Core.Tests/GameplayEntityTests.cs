using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ai;
using BattleCity.Core.Collision;
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
    public void PlacedTurret_SitsOnTileGrid()
    {
        using var world = World.Create();
        var turret = GameplayEntityFactory.CreatePlacedItem(world, ItemType.Turret, 10, 12);
        ref var transform = ref world.Get<Transform2D>(turret);
        Assert.Equal(new Vector2(10 * 48f, 12 * 48f), transform.Position);
        Assert.Equal(new Vector2(10 * 48f + 24f, 12 * 48f + 24f), TurretTargeting.GetTurretWorldCenter(10, 12));
    }

    [Fact]
    public void DroppingWall_PlacesOnTankTileAndNudgesTankLeft()
    {
        using var simulation = new GameSimulation();
        var tankTopLeft = new Vector2(10 * 48f, 10 * 48f);
        var player = simulation.CreatePlayerEntity(tankTopLeft);

        Assert.True(ItemDropActions.TryDropForEntity(
            simulation.World,
            player,
            tankTopLeft,
            ItemType.Wall,
            active: true,
            out var gridX,
            out var gridY,
            tileMap: simulation.TileMap));
        Assert.Equal(10, gridX);
        Assert.Equal(10, gridY);

        ref var transform = ref simulation.World.Get<Transform2D>(player);
        Assert.Equal(tankTopLeft + new Vector2(-48f, 0f), transform.Position);
    }

    [Fact]
    public void DroppingWall_FailsWhenTileOccupied()
    {
        using var simulation = new GameSimulation();
        var tankTopLeft = new Vector2(10 * 48f, 10 * 48f);
        var player = simulation.CreatePlayerEntity(tankTopLeft);
        GameplayEntityFactory.CreatePlacedItem(simulation.World, ItemType.Wall, 10, 10);

        Assert.False(ItemDropActions.TryDropForEntity(
            simulation.World,
            player,
            tankTopLeft,
            ItemType.Wall,
            active: true,
            out _,
            out _,
            tileMap: simulation.TileMap));
    }

    [Fact]
    public void DroppingWall_AllowsTileDirectlyBelowExistingWall()
    {
        using var simulation = new GameSimulation();
        GameplayEntityFactory.CreatePlacedItem(simulation.World, ItemType.Wall, 10, 10);
        GameplayEntityFactory.CreatePlacedItem(simulation.World, ItemType.Wall, 11, 10);
        GameplayEntityFactory.CreatePlacedItem(simulation.World, ItemType.Wall, 12, 10);

        var tankTopLeft = new Vector2(10 * 48f, 11 * 48f);
        var player = simulation.CreatePlayerEntity(tankTopLeft);

        Assert.True(ItemDropActions.CanPlaceItem(simulation.World, player, 10, 11, ItemType.Wall));
        Assert.True(ItemDropActions.CanPlaceItem(simulation.World, player, 11, 11, ItemType.Wall));
        Assert.True(ItemDropActions.CanPlaceItem(simulation.World, player, 12, 11, ItemType.Wall));

        Assert.True(ItemDropActions.TryDropForEntity(
            simulation.World,
            player,
            tankTopLeft,
            ItemType.Wall,
            active: true,
            out var gridX,
            out var gridY,
            tileMap: simulation.TileMap));
        Assert.Equal(10, gridX);
        Assert.Equal(11, gridY);
    }

    [Fact]
    public void DroppingBomb_AllowedWhenTileOccupied()
    {
        using var simulation = new GameSimulation();
        var tankTopLeft = new Vector2(10 * 48f, 10 * 48f);
        var player = simulation.CreatePlayerEntity(tankTopLeft);
        GameplayEntityFactory.CreatePlacedItem(simulation.World, ItemType.Wall, 10, 10);

        Assert.True(ItemDropActions.TryDropForEntity(
            simulation.World,
            player,
            tankTopLeft,
            ItemType.Bomb,
            active: false,
            out var gridX,
            out var gridY,
            tileMap: simulation.TileMap));
        Assert.Equal(10, gridX);
        Assert.Equal(10, gridY);
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
    public void ItemDropSystem_PicksUpFactoryItemMatchingLoadedCityId()
    {
        using var simulation = new GameSimulation();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Buenos Aires",
            SourcePath = "buenosaires.city",
            Buildings = [],
        });
        Assert.True(CityCatalog.TryGetId("Buenos Aires", out var cityId));

        var tankTopLeft = new Vector2(10 * 48f, 10 * 48f);
        var player = simulation.CreatePlayerEntity(tankTopLeft);
        Assert.Equal(cityId, simulation.World.Get<CityAffiliation>(player).CityId);

        var item = GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Flare,
            10,
            10,
            active: false,
            cityId: cityId);

        ref var input = ref simulation.World.Get<InputCommand>(player);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(player);
        inventory.Flare = 0;
        input.PickUpItemPressed = true;

        ItemDropSystem.Update(simulation.World);

        Assert.False(simulation.World.IsAlive(item));
        Assert.Equal(1, inventory.Flare);
    }

    [Fact]
    public void BombSystem_DamagesTankTouchingBlast()
    {
        using var simulation = new GameSimulation();
        var bot = simulation.CreateBotEntity(new Vector2(10 * 48f, 10 * 48f), cityId: 1);
        GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Bomb,
            10,
            10,
            active: true,
            cityId: 0);

        simulation.Tick(EconomyConstants.TimerBomb / 1000f + 0.1f);

        ref var health = ref simulation.World.Get<Health>(bot);
        Assert.True(health.Current < GameConstants.MaxHealth);
    }

    [Fact]
    public void CreatePlayerEntity_SecondPlayerDoesNotReceiveCityOrb()
    {
        using var simulation = new GameSimulation();
        var first = simulation.CreatePlayerEntity(new Vector2(100f, 100f));
        ref var firstInventory = ref simulation.World.Get<PlayerInventory>(first);
        firstInventory.Orb = 1;

        var second = simulation.CreatePlayerEntity(new Vector2(200f, 200f));

        Assert.Equal(1, simulation.World.Get<PlayerInventory>(first).Orb);
        Assert.Equal(0, simulation.World.Get<PlayerInventory>(second).Orb);
    }

    [Fact]
    public void CreatePlayerEntity_OnlineCityOverrideStartsWithoutOrb()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(new Vector2(100f, 100f), cityId: 5);
        Assert.Equal(5, simulation.World.Get<CityAffiliation>(player).CityId);
        Assert.Equal(0, simulation.World.Get<PlayerInventory>(player).Orb);
    }

    [Fact]
    public void CreateStarterLoadout_StartsWithSingleRocket()
    {
        var inventory = PlayerInventory.CreateStarterLoadout();
        Assert.Equal(1, inventory.Rocket);
        Assert.Equal(1, inventory.Flare);
        Assert.Equal(1, inventory.Cloak);
        Assert.Equal(0, inventory.MedKit);
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

    [Fact]
    public void BulletCollision_HitsWallDuringMuzzleGrace()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(new Vector2(10 * 48f, 10 * 48f));
        var wall = GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Wall,
            12,
            10,
            cityId: 0);
        ref var wallHealth = ref simulation.World.Get<Health>(wall);
        var healthBefore = wallHealth.Current;

        // Spawn inside the wall tile while grace is still active (point-blank against a wall).
        var bullet = GameplayEntityFactory.CreateBullet(
            simulation.World,
            BulletKind.Laser,
            new Vector2(12 * 48f + 20f, 10 * 48f + 20f),
            direction: 8,
            player);
        Assert.True(simulation.World.Get<BulletRef>(bullet).CollisionGraceSeconds > 0f);

        BulletCollisionSystem.Resolve(simulation.World, simulation.TileMap);

        Assert.False(simulation.World.IsAlive(bullet));
        wallHealth = ref simulation.World.Get<Health>(wall);
        Assert.True(wallHealth.Current < healthBefore);
    }
}
