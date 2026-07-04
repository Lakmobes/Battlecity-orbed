using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ai;
using BattleCity.Core.Collision;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

public static class GameplayEntityFactory
{
    public static Entity CreateExplosion(World world, ExplosionKind kind, Vector2 position)
    {
        var (sourceX, sourceY, width, height) = ExplosionSprites.GetFrameRect(kind, 0);
        var drawPosition = kind switch
        {
            ExplosionKind.Large => position - new Vector2(ExplosionSprites.LargeFrameSize / 2f, ExplosionSprites.LargeFrameSize / 2f),
            ExplosionKind.Small => position - new Vector2(ExplosionSprites.SmallFrameSize / 2f, ExplosionSprites.SmallFrameSize / 2f),
            _ => position,
        };

        return world.Create(
            new Transform2D { Position = drawPosition, PreviousPosition = drawPosition },
            new ExplosionRef { Kind = kind, AnimationFrame = 0, FrameTimerSeconds = 0f },
            new SpriteRef
            {
                TextureKey = ExplosionSprites.GetTextureKey(kind),
                SourceX = sourceX,
                SourceY = sourceY,
                Width = width,
                Height = height,
            });
    }

    public static Entity CreateBullet(
        World world,
        BulletKind kind,
        Vector2 position,
        int direction,
        Entity owner)
    {
        var (sourceX, sourceY) = BulletSprites.GetSourceOrigin(kind, 0);

        return world.Create(
            new Transform2D { Position = position, PreviousPosition = position },
            new Velocity { Value = Vector2.Zero },
            new BulletRef
            {
                Kind = kind,
                Direction = direction,
                Owner = owner,
                AnimationFrame = 0,
                CollisionGraceSeconds = 0.08f,
            },
            new Lifetime { Remaining = BulletStats.GetInitialLife(kind) },
            new Damage { Value = BulletStats.GetDamage(kind) },
            new SpriteRef
            {
                TextureKey = BulletSprites.TextureKey,
                SourceX = sourceX,
                SourceY = sourceY,
                Width = BulletSprites.SpriteSize,
                Height = BulletSprites.SpriteSize,
            },
            new Collider
            {
                OffsetX = 0,
                OffsetY = 0,
                Width = 4,
                Height = 4,
                Layer = CollisionLayer.Bullet,
            });
    }

    public static Entity CreatePlacedItem(
        World world,
        ItemType type,
        int gridX,
        int gridY,
        bool active = true,
        int cityId = 0,
        ushort networkItemId = 0)
    {
        var position = IsTurretType(type)
            ? LegacyItemWorldPosition(gridX, gridY)
            : PlacedItemPlacement.GridToWorldPosition(gridX, gridY);
        var (sourceX, sourceY) = ItemSprites.GetWorldSpriteOrigin(type);
        var blocksMovement = type >= ItemType.Wall;
        var fuseTimer = type == ItemType.Bomb && active
            ? EconomyConstants.TimerBomb / 1000f
            : 0f;

        var entity = world.Create(
            new Transform2D { Position = position, PreviousPosition = position },
            new PlacedItemRef
            {
                Type = type,
                GridX = gridX,
                GridY = gridY,
                Active = active,
                CityId = cityId,
                FuseTimerSeconds = fuseTimer,
            },
            new CityAffiliation { CityId = cityId },
            new SpriteRef
            {
                TextureKey = ItemSprites.TextureKey,
                SourceX = sourceX,
                SourceY = sourceY,
                Width = ItemSprites.WorldSpriteSize,
                Height = ItemSprites.WorldSpriteSize,
            });

        if (blocksMovement)
        {
            world.Add(
                entity,
                new Collider
                {
                    OffsetX = GameConstants.PlayerCollisionInset,
                    OffsetY = GameConstants.PlayerCollisionInset,
                    Width = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                    Height = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                    Layer = CollisionLayer.Item,
                });
        }

        if (IsTurretType(type))
        {
            ConfigureTurret(world, entity, type, active);
        }
        else if (ItemHealth.IsDamageable(type))
        {
            var maxHealth = ItemHealth.GetMaxHealth(type);
            world.Add(
                entity,
                new Health
                {
                    Current = maxHealth,
                    Max = maxHealth,
                });
        }

        if (networkItemId > 0)
        {
            world.Add(entity, new NetworkItemRef { ItemId = networkItemId });
        }

        return entity;
    }

    public static void ConfigureTurret(World world, Entity entity, ItemType type, bool active)
    {
        world.Add(
            entity,
            new TurretState
            {
                StartupDelaySeconds = active ? GameConstants.TimerTurretStartup / 1000f : 0f,
                AnimationFrame = 0,
            });

        world.Add(
            entity,
            new Health
            {
                Current = TurretStats.GetMaxHealth(type),
                Max = TurretStats.GetMaxHealth(type),
            });

        ref var sprite = ref world.Get<SpriteRef>(entity);
        sprite.TextureKey = TurretSprites.BaseTextureKey;
    }

    public static AxisAlignedBox GetLegacyItemBulletBounds(int gridX, int gridY)
    {
        var tileSize = GameConstants.TileSize;
        return new AxisAlignedBox(
            gridX * tileSize - tileSize,
            gridY * tileSize - tileSize,
            tileSize,
            tileSize);
    }

    public static Vector2 LegacyItemWorldPosition(int gridX, int gridY) =>
        new(
            gridX * GameConstants.TileSize - 24,
            gridY * GameConstants.TileSize - 24);

    private static bool IsTurretType(ItemType type) =>
        type is ItemType.Turret or ItemType.Sleeper or ItemType.Plasma;
}
