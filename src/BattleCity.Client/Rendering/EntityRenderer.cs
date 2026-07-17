using Arch.Core;

using BattleCity.Core.Ai;
using BattleCity.Core.City;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Rendering;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public sealed class EntityRenderer
{
    private static readonly QueryDescription Query =
        new QueryDescription().WithAll<Transform2D, SpriteRef>();

    private static readonly QueryDescription TurretQuery =
        new QueryDescription().WithAll<Transform2D, PlacedItemRef, TurretState, SpriteRef>();

    private static readonly QueryDescription BulletQuery =
        new QueryDescription().WithAll<Transform2D, SpriteRef, BulletRef>();

    private static readonly QueryDescription BuildingQuery =
        new QueryDescription().WithAll<Transform2D, SpriteRef, BuildingRef, BuildingState>();

    private static readonly QueryDescription ExplosionQuery =
        new QueryDescription().WithAll<Transform2D, SpriteRef, ExplosionRef>();

    private readonly Assets.AssetService _assets;
    private readonly List<DrawableEntity> _buildingDrawList = new(capacity: 128);
    private readonly List<DrawableEntity> _actorDrawList = new(capacity: 128);
    private readonly List<TurretDrawEntry> _turretDrawList = new(capacity: 32);
    private readonly List<DrawableEntity> _bulletDrawList = new(capacity: 64);
    private readonly List<DrawableEntity> _explosionDrawList = new(capacity: 32);

    public EntityRenderer(Assets.AssetService assets)
    {
        _assets = assets;
    }

    public void CollectDrawables(World world, CityBuildState? cityBuild = null, float animationTime = 0f, int observerCityId = 0)
    {
        CollectDrawablesInternal(world, cityBuild, animationTime, observerCityId);
    }

    public void Draw(SpriteBatch spriteBatch, World world, CityBuildState? cityBuild = null, float animationTime = 0f, int observerCityId = 0)
    {
        CollectDrawablesInternal(world, cityBuild, animationTime, observerCityId);
        DrawBuildings(spriteBatch);
        DrawActors(spriteBatch);
    }

    public void DrawBuildings(SpriteBatch spriteBatch)
    {
        EntityDrawSorter.Sort(_buildingDrawList);
        foreach (var building in _buildingDrawList)
        {
            DrawDrawable(spriteBatch, building.Transform, building.Sprite);
        }
    }

    public void DrawActors(SpriteBatch spriteBatch)
    {
        EntityDrawSorter.Sort(_actorDrawList);
        _turretDrawList.Sort(static (a, b) => a.SortDepth.CompareTo(b.SortDepth));

        var turretIndex = 0;
        var entityIndex = 0;
        while (entityIndex < _actorDrawList.Count || turretIndex < _turretDrawList.Count)
        {
            if (turretIndex >= _turretDrawList.Count ||
                (entityIndex < _actorDrawList.Count &&
                 _actorDrawList[entityIndex].SortDepth <= _turretDrawList[turretIndex].SortDepth))
            {
                var drawable = _actorDrawList[entityIndex++];
                DrawDrawable(spriteBatch, drawable.Transform, drawable.Sprite);
            }
            else
            {
                var turret = _turretDrawList[turretIndex++];
                DrawTurret(spriteBatch, turret.Transform, turret.Item, turret.State);
            }
        }

        foreach (var bullet in _bulletDrawList)
        {
            DrawBullet(spriteBatch, bullet.Transform, bullet.Sprite);
        }

        foreach (var explosion in _explosionDrawList)
        {
            DrawDrawable(spriteBatch, explosion.Transform, explosion.Sprite);
        }
    }

    private void CollectDrawablesInternal(World world, CityBuildState? cityBuild, float animationTime, int observerCityId)
    {
        _buildingDrawList.Clear();
        _actorDrawList.Clear();
        _turretDrawList.Clear();
        _bulletDrawList.Clear();
        _explosionDrawList.Clear();

        world.Query(
            in BuildingQuery,
            (ref Transform2D transform, ref SpriteRef sprite, ref BuildingRef building, ref BuildingState state) =>
            {
                var drawSprite = sprite;
                if (ResearchVisuals.IsResearchInProgress(cityBuild, building.TypeCode, state.Population, out _))
                {
                    var frameOffset = ResearchVisuals.GetAnimationFrameOffset(animationTime);
                    drawSprite = new SpriteRef
                    {
                        TextureKey = sprite.TextureKey,
                        SourceX = sprite.SourceX + frameOffset,
                        SourceY = sprite.SourceY,
                        Width = sprite.Width,
                        Height = sprite.Height,
                    };
                }

                BuildingDrawSlices.AddDrawables(_buildingDrawList, in transform, in drawSprite, building.TypeCode);
            });

        world.Query(
            in Query,
            (Entity entity, ref Transform2D transform, ref SpriteRef sprite) =>
            {
                if (world.Has<TurretState>(entity) || world.Has<BulletRef>(entity) || world.Has<BuildingRef>(entity))
                {
                    return;
                }

                if (world.Has<TankLifeState>(entity))
                {
                    ref readonly var life = ref world.Get<TankLifeState>(entity);
                    if (life.IsDead)
                    {
                        return;
                    }
                }

                var drawTransform = transform;
                if (world.Has<PlacedItemRef>(entity))
                {
                    // Legacy CDrawing: tileY + 10 for non-turret items (turrets cancel via -10).
                    drawTransform.Position += new System.Numerics.Vector2(0f, ItemSprites.WorldDrawOffsetY);
                }

                _actorDrawList.Add(new DrawableEntity(
                    DrawableEntity.ComputeSortDepth(in drawTransform, in sprite),
                    drawTransform,
                    sprite));
            });

        world.Query(
            in TurretQuery,
            (ref Transform2D transform, ref PlacedItemRef item, ref TurretState turret, ref SpriteRef sprite) =>
            {
                if (!ShouldDrawTurret(in item, in turret, observerCityId))
                {
                    return;
                }

                _turretDrawList.Add(new TurretDrawEntry(
                    DrawableEntity.ComputeSortDepth(in transform, in sprite) + TurretSprites.VerticalDrawOffset,
                    transform,
                    item,
                    turret));
            });

        world.Query(
            in BulletQuery,
            (ref Transform2D transform, ref SpriteRef sprite) =>
            {
                _bulletDrawList.Add(new DrawableEntity(
                    DrawableEntity.ComputeSortDepth(in transform, in sprite),
                    transform,
                    sprite));
            });

        world.Query(
            in ExplosionQuery,
            (ref Transform2D transform, ref SpriteRef sprite) =>
            {
                _explosionDrawList.Add(new DrawableEntity(
                    DrawableEntity.ComputeSortDepth(in transform, in sprite),
                    transform,
                    sprite));
            });
    }

    private void DrawBullet(SpriteBatch spriteBatch, Transform2D transform, SpriteRef sprite)
    {
        var texture = _assets.LoadTexture(sprite.TextureKey);
        var legacySource = new Rectangle(sprite.SourceX, sprite.SourceY, sprite.Width, sprite.Height);
        WorldSpriteMetrics.DrawLegacySprite(
            spriteBatch,
            texture,
            transform.Position.X,
            transform.Position.Y,
            legacySource,
            Color.White);
    }

    private static bool ShouldDrawTurret(in PlacedItemRef item, in TurretState turret, int observerCityId)
    {
        if (!item.Active)
        {
            return false;
        }

        // Sleepers are always visible to the owning city; enemies only see them once woken.
        if (item.Type == ItemType.Sleeper && !turret.HasTarget && item.CityId != observerCityId)
        {
            return false;
        }

        return true;
    }

    private void DrawTurret(
        SpriteBatch spriteBatch,
        Transform2D transform,
        PlacedItemRef item,
        TurretState turret)
    {
        var drawX = (int)transform.Position.X;
        var drawY = (int)transform.Position.Y
            + WorldSpriteMetrics.Scaled(TurretSprites.VerticalDrawOffset);
        var row = TurretSprites.GetSheetRow(item.Type);
        var baseTexture = _assets.LoadTexture(TurretSprites.BaseTextureKey);
        var headTexture = _assets.LoadTexture(TurretSprites.HeadTextureKey);
        var spriteSize = TurretSprites.SpriteSize;

        var baseSource = new Rectangle(
            turret.AnimationFrame * spriteSize,
            row * spriteSize,
            spriteSize,
            spriteSize);
        var headOrientation = TurretTargeting.AngleDegreesToHeadOrientation(turret.AimAngleDegrees);
        var headSource = new Rectangle(
            headOrientation * spriteSize,
            row * spriteSize,
            spriteSize,
            spriteSize);

        WorldSpriteMetrics.DrawLegacySprite(spriteBatch, baseTexture, drawX, drawY, baseSource, Color.White);
        WorldSpriteMetrics.DrawLegacySprite(spriteBatch, headTexture, drawX, drawY, headSource, Color.White);
    }

    private void DrawDrawable(SpriteBatch spriteBatch, Transform2D transform, SpriteRef sprite)
    {
        var texture = _assets.LoadTexture(sprite.TextureKey);
        var legacySource = new Rectangle(sprite.SourceX, sprite.SourceY, sprite.Width, sprite.Height);
        WorldSpriteMetrics.DrawLegacySprite(
            spriteBatch,
            texture,
            transform.Position.X,
            transform.Position.Y,
            legacySource,
            Color.White);
    }

    private readonly record struct TurretDrawEntry(
        int SortDepth,
        Transform2D Transform,
        PlacedItemRef Item,
        TurretState State);
}
