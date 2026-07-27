using Arch.Core;

using BattleCity.Core.Ai;
using BattleCity.Core.City;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Rendering;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Levels;
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
    private readonly List<DrawableEntity> _itemDrawList = new(capacity: 128);
    private readonly List<DrawableEntity> _tankDrawList = new(capacity: 32);
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
        _buildingDrawList.Sort(static (a, b) => a.SortDepth.CompareTo(b.SortDepth));
        foreach (var building in _buildingDrawList)
        {
            DrawDrawable(spriteBatch, building.Transform, building.Sprite);
        }
    }

    public void DrawActors(SpriteBatch spriteBatch)
    {
        // Legacy CDrawing: items (incl. turrets) then players — tanks always over bay stock.
        _itemDrawList.Sort(static (a, b) => a.SortDepth.CompareTo(b.SortDepth));
        _turretDrawList.Sort(static (a, b) => a.SortDepth.CompareTo(b.SortDepth));
        _tankDrawList.Sort(static (a, b) => a.SortDepth.CompareTo(b.SortDepth));

        var turretIndex = 0;
        var itemIndex = 0;
        while (itemIndex < _itemDrawList.Count || turretIndex < _turretDrawList.Count)
        {
            if (turretIndex >= _turretDrawList.Count ||
                (itemIndex < _itemDrawList.Count &&
                 _itemDrawList[itemIndex].SortDepth <= _turretDrawList[turretIndex].SortDepth))
            {
                var item = _itemDrawList[itemIndex++];
                DrawDrawable(spriteBatch, item.Transform, item.Sprite);
            }
            else
            {
                var turret = _turretDrawList[turretIndex++];
                DrawTurret(spriteBatch, turret.Transform, turret.Item, turret.State);
            }
        }

        foreach (var tank in _tankDrawList)
        {
            DrawDrawable(spriteBatch, tank.Transform, tank.Sprite);
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
        _ = cityBuild;
        _ = animationTime;

        _buildingDrawList.Clear();
        _itemDrawList.Clear();
        _tankDrawList.Clear();
        _turretDrawList.Clear();
        _bulletDrawList.Clear();
        _explosionDrawList.Clear();

        world.Query(
            in BuildingQuery,
            (ref Transform2D transform, ref SpriteRef sprite, ref BuildingRef building, ref BuildingState state) =>
            {
                var (sourceX, sourceY) = BuildingSprites.GetSourceOrigin(
                    building.TypeCode,
                    state.AnimationFrame);
                var current = new SpriteRef
                {
                    TextureKey = sprite.TextureKey,
                    SourceX = sourceX,
                    SourceY = sourceY,
                    Width = sprite.Width,
                    Height = sprite.Height,
                };
                BuildingDrawSlices.AddDrawables(
                    _buildingDrawList,
                    in transform,
                    in current,
                    building.TypeCode);
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
                    ref readonly var placed = ref world.Get<PlacedItemRef>(entity);
                    if (!ShouldDrawMineOrDfg(world, in placed, observerCityId))
                    {
                        return;
                    }

                    drawTransform.Position += new System.Numerics.Vector2(0f, ItemSprites.WorldDrawOffsetY);
                }

                // Legacy draws all tanks after items so bay stock never covers the hull.
                var drawList = world.Has<TankLifeState>(entity) ? _tankDrawList : _itemDrawList;

                if (world.Has<PlacedItemRef>(entity)
                    && ItemSprites.UsesItemSheetAnimation(world.Get<PlacedItemRef>(entity).Type))
                {
                    ref readonly var item = ref world.Get<PlacedItemRef>(entity);
                    var frame = ItemSprites.ResolveAnimationFrame(
                        item.Type,
                        item.Active,
                        ItemAnimationSystem.ElapsedSeconds);
                    var (x0, y0) = ItemSprites.GetWorldSpriteOrigin(item.Type, frame);
                    var current = new SpriteRef
                    {
                        TextureKey = ItemSprites.TextureKey,
                        SourceX = x0,
                        SourceY = y0,
                        Width = ItemSprites.WorldSpriteSize,
                        Height = ItemSprites.WorldSpriteSize,
                    };
                    drawList.Add(new DrawableEntity(
                        DrawableEntity.ComputeSortDepth(in drawTransform, in current),
                        drawTransform,
                        current));
                    return;
                }

                drawList.Add(new DrawableEntity(
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
        // Factory-bay stock must always render — pickups are otherwise invisible on the pad.
        if (!item.Active)
        {
            return true;
        }

        // Friendly city always sees its own turrets.
        if (item.CityId == observerCityId)
        {
            return true;
        }

        // Enemies only see turrets once they are in firing range (HasTarget).
        return turret.HasTarget;
    }

    /// <summary>
    /// Legacy DrawItems: active enemy mines/DFGs stay hidden unless the observer is driving over them.
    /// Inactive (factory bay) stock is always visible.
    /// </summary>
    private static bool ShouldDrawMineOrDfg(World world, in PlacedItemRef item, int observerCityId)
    {
        if (item.Type is not (ItemType.Mine or ItemType.Dfg))
        {
            return true;
        }

        if (!item.Active || item.CityId == observerCityId)
        {
            return true;
        }

        return ObserverOverlapsItem(world, item.GridX, item.GridY, observerCityId);
    }

    private static bool ObserverOverlapsItem(World world, int gridX, int gridY, int observerCityId)
    {
        var triggerBounds = MineSystem.GetMineTriggerBounds(gridX, gridY);
        var overlapping = false;
        var tankQuery = new QueryDescription().WithAll<Transform2D, TankLifeState, CityAffiliation>();
        world.Query(
            in tankQuery,
            (ref Transform2D transform, ref TankLifeState life, ref CityAffiliation city) =>
            {
                if (overlapping || life.IsDead || city.CityId != observerCityId)
                {
                    return;
                }

                var tankCenter = TurretTargeting.GetTankCenter(transform.Position);
                if (triggerBounds.ContainsPoint(tankCenter))
                {
                    overlapping = true;
                }
            });

        return overlapping;
    }

    private void DrawTurret(
        SpriteBatch spriteBatch,
        Transform2D transform,
        PlacedItemRef item,
        TurretState turret)
    {
        var drawX = (int)transform.Position.X;
        var drawY = (int)transform.Position.Y + TurretSprites.VerticalDrawOffset;
        var row = TurretSprites.GetSheetRow(item.Type);
        var baseTexture = _assets.LoadTexture(TurretSprites.BaseTextureKey);
        var headTexture = _assets.LoadTexture(TurretSprites.HeadTextureKey);
        var spriteSize = TurretSprites.SpriteSize;

        var baseSource = new Rectangle(
            turret.AnimationFrame * spriteSize,
            row * spriteSize,
            spriteSize,
            spriteSize);
        // Legacy CDrawing: head column is Angle/22.5 (16 dirs), not fireDirection/2.
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
