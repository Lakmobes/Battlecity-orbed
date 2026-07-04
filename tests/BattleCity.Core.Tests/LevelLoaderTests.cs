using BattleCity.Core.Collision;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;

using Arch.Core;

using Xunit;

namespace BattleCity.Core.Tests;

public class CityLayoutParserTests
{
    [Fact]
    public void ParseFile_LoadsBuenosAiresDemoLayout()
    {
        var path = CityLayoutPaths.FindLegacyCityLayout("Buenos Aires", "demo");
        Assert.NotNull(path);

        var layout = CityLayoutParser.ParseFile(path!, "Buenos Aires");

        Assert.Equal("Buenos Aires", layout.CityName);
        Assert.Equal(40, layout.Buildings.Count);
    }

    [Fact]
    public void TryParseLine_ResolvesMenuIndexToTypeCode()
    {
        Assert.True(CityLayoutParser.TryParseLine("2 291 304", out var placement));
        Assert.Equal(2, placement.MenuIndex);
        Assert.Equal(291, placement.GridX);
        Assert.Equal(304, placement.GridY);
        Assert.Equal(BuildingCatalog.MenuTypeCodes[1], placement.TypeCode);
    }

    [Fact]
    public void TryParseLine_RejectsInvalidRows()
    {
        Assert.False(CityLayoutParser.TryParseLine("", out _));
        Assert.False(CityLayoutParser.TryParseLine("0 10 10", out _));
        Assert.False(CityLayoutParser.TryParseLine("99 10 10", out _));
        Assert.False(CityLayoutParser.TryParseLine("abc 1 2", out _));
    }
}

public class BuildingPlacementTests
{
    [Fact]
    public void GridAnchorToWorldPosition_MatchesLegacyCollisionOffset()
    {
        var world = BuildingPlacement.GridAnchorToWorldPosition(291, 304);

        Assert.Equal((291 - GameConstants.BuildingCollisionOffset) * GameConstants.TileSize, world.X);
        Assert.Equal((304 - GameConstants.BuildingCollisionOffset) * GameConstants.TileSize, world.Y);
    }

    [Fact]
    public void FactoryUsesRaisedPlatformCollision()
    {
        var spriteTopLeft = BuildingPlacement.GridAnchorToWorldPosition(10, 10);
        var bounds = BuildingCollision.GetPlayerBlockingBounds(109, spriteTopLeft);

        Assert.Equal(spriteTopLeft.X, bounds.Left);
        Assert.Equal(spriteTopLeft.Y, bounds.Top);
        Assert.Equal(GameConstants.BuildingCollisionSize, bounds.Width);
        Assert.Equal(BuildingCollision.RaisedBlockingHeightPixels, bounds.Height);

        var platform = BuildingCollision.GetDrivePlatformBounds(spriteTopLeft);
        Assert.Equal(spriteTopLeft.Y + BuildingCollision.RaisedBlockingHeightPixels, platform.Top);
        Assert.Equal(BuildingCollision.PlatformHeightPixels, platform.Height);
    }

    [Fact]
    public void ResearchUsesFullBuildingCollision()
    {
        var spriteTopLeft = BuildingPlacement.GridAnchorToWorldPosition(10, 10);
        var bounds = BuildingCollision.GetPlayerBlockingBounds(401, spriteTopLeft);

        Assert.Equal(spriteTopLeft.X, bounds.Left);
        Assert.Equal(spriteTopLeft.Y, bounds.Top);
        Assert.Equal(GameConstants.BuildingCollisionSize, bounds.Width);
        Assert.Equal(GameConstants.BuildingCollisionSize, bounds.Height);
    }

    [Fact]
    public void PlayerCanDriveOntoFactoryPlatform()
    {
        using var world = World.Create();
        var map = TileMap.CreateEmpty();
        var spriteTopLeft = BuildingPlacement.GridAnchorToWorldPosition(50, 50);
        var platformTopLeft = new System.Numerics.Vector2(
            spriteTopLeft.X + (GameConstants.BuildingCollisionSize - GameConstants.TileSize) / 2f,
            spriteTopLeft.Y + BuildingCollision.RaisedBlockingHeightPixels);
        var (offsetX, offsetY, width, height) = BuildingCollision.GetPlayerColliderShape(109);

        world.Create(
            new Transform2D { Position = spriteTopLeft },
            new BuildingRef { MenuIndex = 5, TypeCode = 109 },
            new Collider
            {
                OffsetX = offsetX,
                OffsetY = offsetY,
                Width = width,
                Height = height,
                Layer = CollisionLayer.Building,
            });

        var player = world.Create(
            new Transform2D { Position = platformTopLeft },
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
            player,
            platformTopLeft,
            world.Get<Collider>(player));

        Assert.Equal(PlayerCollisionResult.None, result);
    }

    [Fact]
    public void PlayerCannotDriveIntoFactoryStructure()
    {
        using var world = World.Create();
        var map = TileMap.CreateEmpty();
        var spriteTopLeft = BuildingPlacement.GridAnchorToWorldPosition(50, 50);
        var structureTopLeft = new System.Numerics.Vector2(
            spriteTopLeft.X + (GameConstants.BuildingCollisionSize - GameConstants.TileSize) / 2f,
            spriteTopLeft.Y);
        var (offsetX, offsetY, width, height) = BuildingCollision.GetPlayerColliderShape(109);

        world.Create(
            new Transform2D { Position = spriteTopLeft },
            new BuildingRef { MenuIndex = 5, TypeCode = 109 },
            new Collider
            {
                OffsetX = offsetX,
                OffsetY = offsetY,
                Width = width,
                Height = height,
                Layer = CollisionLayer.Building,
            });

        var player = world.Create(
            new Transform2D { Position = structureTopLeft },
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
            player,
            structureTopLeft,
            world.Get<Collider>(player));

        Assert.Equal(PlayerCollisionResult.Blocking, result);
    }

    [Fact]
    public void BuildingSprites_UseLegacySheetRows()
    {
        Assert.Equal((0, 288), BuildingSprites.GetSourceOrigin(200));
        Assert.Equal((0, 576), BuildingSprites.GetSourceOrigin(400));
    }
}

public class LevelLoaderTests
{
    [Fact]
    public void SpawnBuildings_CreatesCollidersAndSprites()
    {
        var layout = LevelLoader.LoadLegacyCity("Buenos Aires", "demo");

        using var simulation = new GameSimulation();
        simulation.LoadCityLayout(layout);

        var query = new Arch.Core.QueryDescription().WithAll<BuildingRef, SpriteRef, Collider>();
        var count = 0;

        simulation.World.Query(
            in query,
            (ref Collider collider, ref SpriteRef sprite) =>
            {
                count++;
                Assert.Equal(CollisionLayer.Building, collider.Layer);
                Assert.Equal(BuildingSprites.TextureKey, sprite.TextureKey);
                Assert.Equal(BuildingSprites.SpriteSize, sprite.Width);
            });

        Assert.Equal(layout.Buildings.Count, count);
    }
}
