using System.Numerics;

using Arch.Core;

using BattleCity.Core.Collision;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Core.Tests;

public class AxisAlignedBoxTests
{
    [Fact]
    public void Intersects_DetectsOverlap()
    {
        var a = new AxisAlignedBox(0, 0, 32, 32);
        var b = new AxisAlignedBox(16, 16, 32, 32);

        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void Intersects_RejectsSeparatedBoxes()
    {
        var a = new AxisAlignedBox(0, 0, 32, 32);
        var b = new AxisAlignedBox(64, 0, 32, 32);

        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void FromCollider_MatchesLegacyPlayerInset()
    {
        var box = AxisAlignedBox.FromCollider(
            new Vector2(100f, 200f),
            new Collider
            {
                OffsetX = GameConstants.PlayerCollisionInset,
                OffsetY = GameConstants.PlayerCollisionInset,
                Width = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Height = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Layer = CollisionLayer.Player,
            });

        Assert.Equal(108f, box.Left);
        Assert.Equal(208f, box.Top);
        Assert.Equal(32f, box.Width);
        Assert.Equal(32f, box.Height);
    }
}

public class TerrainCollisionTests
{
    [Fact]
    public void OpenTerrain_DoesNotBlockPlayerBounds()
    {
        var map = TileMap.CreateEmpty();
        var bounds = new AxisAlignedBox(100f, 100f, 32f, 32f);

        Assert.False(TerrainCollision.IsBlocking(map, bounds));
    }

    [Fact]
    public void RockTileAtCorner_BlocksPlayerBounds()
    {
        var map = TileMap.CreateEmpty();
        map.Terrain[2, 2] = TerrainTileType.Rock;

        var bounds = new AxisAlignedBox(96f, 96f, 32f, 32f);

        Assert.True(TerrainCollision.IsBlocking(map, bounds));
    }

    [Fact]
    public void LavaAndCityCenter_BlockPlayersOnly()
    {
        Assert.True(TerrainCollision.IsBlockingTile(TerrainTileType.Lava));
        Assert.True(TerrainCollision.IsBlockingTile(TerrainTileType.Rock));
        Assert.True(TerrainCollision.IsBlockingTile(TerrainTileType.CityCenter));
        Assert.False(TerrainCollision.IsBlockingTile(TerrainTileType.Open));
        Assert.False(TerrainCollision.IsBlockingTileForBullet(TerrainTileType.Lava));
        Assert.True(TerrainCollision.IsBlockingTileForBullet(TerrainTileType.Rock));
        Assert.False(TerrainCollision.IsBlockingTileForBullet(TerrainTileType.CityCenter));
    }

    [Fact]
    public void LavaDoesNotBlockBullets()
    {
        var map = TileMap.CreateEmpty();
        map.Terrain[2, 2] = TerrainTileType.Lava;

        var bounds = new AxisAlignedBox(96f, 96f, 32f, 32f);

        Assert.True(TerrainCollision.IsBlocking(map, bounds));
        Assert.False(TerrainCollision.IsBlockingForBullet(map, bounds));
    }
}

public class CollisionSystemTests
{
    [Fact]
    public void CheckPlayerCollision_DetectsRockTile()
    {
        var map = TileMap.CreateEmpty();
        map.Terrain[5, 5] = TerrainTileType.Rock;

        using var world = World.Create();
        var entity = world.Create(
            new Transform2D(),
            new Collider
            {
                OffsetX = GameConstants.PlayerCollisionInset,
                OffsetY = GameConstants.PlayerCollisionInset,
                Width = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Height = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Layer = CollisionLayer.Player,
            });

        var result = CollisionQueries.CheckPlayerCollision(
            world,
            map,
            entity,
            new Vector2(230f, 230f),
            world.Get<Collider>(entity));

        Assert.Equal(PlayerCollisionResult.Blocking, result);
    }

    [Fact]
    public void Resolve_BlocksMovementIntoRock()
    {
        var map = TileMap.CreateEmpty();
        map.Terrain[5, 5] = TerrainTileType.Rock;

        using var simulation = new GameSimulation { TileMap = map };
        var entity = simulation.CreatePatrolEntity(
            new Vector2(200f, 230f),
            new Vector2(300f, 0f));

        simulation.Tick(0.1f);

        ref var transform = ref simulation.World.Get<Transform2D>(entity);
        Assert.Equal(200f, transform.Position.X, precision: 1);
    }

    [Fact]
    public void Resolve_PlayerTanksDoNotOverlap()
    {
        var map = TileMap.CreateEmpty();

        using var simulation = new GameSimulation { TileMap = map };
        simulation.CreatePlayerEntity(new Vector2(100f, 100f));
        var patrol = simulation.CreatePatrolEntity(new Vector2(200f, 100f), new Vector2(-240f, 0f));

        simulation.Tick(0.05f);

        ref var patrolTransform = ref simulation.World.Get<Transform2D>(patrol);
        Assert.True(patrolTransform.Position.X >= 132f);
    }

    [Fact]
    public void Resolve_BuildingObstacleBlocksPlayer()
    {
        var map = TileMap.CreateEmpty();

        using var simulation = new GameSimulation { TileMap = map };
        simulation.CreateBuildingObstacle(
            new Vector2(200f, 200f),
            GameConstants.BuildingCollisionSize,
            GameConstants.BuildingCollisionSize);

        var player = simulation.CreatePlayerEntity(new Vector2(160f, 220f));
        ref var velocity = ref simulation.World.Get<Velocity>(player);
        velocity.Value = new Vector2(120f, 0f);

        simulation.Tick(0.2f);

        ref var transform = ref simulation.World.Get<Transform2D>(player);
        Assert.True(transform.Position.X < 200f);
    }

    [Fact]
    public void MapEdge_ReversesPatrolVelocity()
    {
        using var world = World.Create();
        var map = TileMap.CreateEmpty();
        var max = GameConstants.WorldSizePixels - GameConstants.TileSize;
        var entity = world.Create(
            new Transform2D
            {
                Position = new Vector2(max + 100f, 0f),
                PreviousPosition = new Vector2(max, 0f),
            },
            new Collider
            {
                OffsetX = GameConstants.PlayerCollisionInset,
                OffsetY = GameConstants.PlayerCollisionInset,
                Width = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Height = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Layer = CollisionLayer.Player,
            },
            new Velocity { Value = new Vector2(100f, 0f) },
            new PatrolBehavior());

        CollisionSystem.Resolve(world, map);

        ref var velocity = ref world.Get<Velocity>(entity);
        ref var transform = ref world.Get<Transform2D>(entity);

        Assert.Equal(max, transform.Position.X);
        Assert.True(velocity.Value.X < 0f);
    }

    [Fact]
    public void Resolve_ClampsPatrolEntityAtMapEdge()
    {
        var map = TileMap.CreateEmpty();
        var max = GameConstants.WorldSizePixels - GameConstants.TileSize;

        using var simulation = new GameSimulation { TileMap = map };
        var patrol = simulation.CreatePatrolEntity(
            new Vector2(max - 1f, 100f),
            new Vector2(200f, 0f));

        simulation.Tick(0.5f);

        ref var transform = ref simulation.World.Get<Transform2D>(patrol);
        ref var velocity = ref simulation.World.Get<Velocity>(patrol);

        Assert.Equal(max, transform.Position.X);
        Assert.True(velocity.Value.X < 0f);
    }
}
