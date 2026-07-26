using System.Numerics;

using Arch.Core;

using BattleCity.Core.Audio;
using BattleCity.Core.Collision;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Core.Ecs.Systems;

public static class BombSystem
{
    private static readonly QueryDescription BombQuery =
        new QueryDescription().WithAll<PlacedItemRef, Transform2D>();

    public static void Update(
        World world,
        float deltaSeconds,
        SimulationAudioBuffer? audio = null,
        BombSimulationHooks hooks = default)
    {
        if (hooks.SuppressDetonation)
        {
            return;
        }

        var exploded = new List<Entity>();

        world.Query(
            in BombQuery,
            (Entity entity, ref PlacedItemRef item, ref Transform2D transform) =>
            {
                if (item.Type != ItemType.Bomb || !item.Active || item.FuseTimerSeconds <= 0f)
                {
                    return;
                }

                item.FuseTimerSeconds -= deltaSeconds;
                if (item.FuseTimerSeconds > 0f)
                {
                    return;
                }

                Detonate(world, entity, ref item, transform.Position, exploded, audio, hooks);
            });

        foreach (var entity in exploded)
        {
            if (world.IsAlive(entity))
            {
                world.Destroy(entity);
            }
        }
    }

    private static void Detonate(
        World world,
        Entity bombEntity,
        ref PlacedItemRef bomb,
        Vector2 bombTopLeft,
        List<Entity> exploded,
        SimulationAudioBuffer? audio,
        BombSimulationHooks hooks)
    {
        if (exploded.Contains(bombEntity))
        {
            return;
        }

        exploded.Add(bombEntity);

        var center = new Vector2(
            bombTopLeft.X + GameConstants.TileSize / 2f,
            bombTopLeft.Y + GameConstants.TileSize / 2f);
        var blastAnchorX = bomb.GridX + 1;
        var blastAnchorY = bomb.GridY + 1;

        hooks.ReportExplosion?.Invoke(new ServerExplosionPacket(
            (byte)Math.Clamp(bomb.CityId, 0, byte.MaxValue),
            (ushort)blastAnchorX,
            (ushort)blastAnchorY));

        if (world.Has<NetworkItemRef>(bombEntity))
        {
            hooks.ReportItemRemoved?.Invoke(world.Get<NetworkItemRef>(bombEntity).ItemId);
        }

        GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Large, center);
        audio?.Play(SoundId.Explode, center);

        ChainDetonateItems(world, blastAnchorX, blastAnchorY, exploded, audio, hooks);
        DamagePlacedItems(world, blastAnchorX, blastAnchorY, exploded, audio, hooks);
        DamageBuildings(world, blastAnchorX, blastAnchorY, audio, hooks);
        DamageTanks(world, center, GameConstants.TileSize * 2f, audio, bomb.CityId, hooks);
    }

    private static void ChainDetonateItems(
        World world,
        int blastAnchorX,
        int blastAnchorY,
        List<Entity> exploded,
        SimulationAudioBuffer? audio,
        BombSimulationHooks hooks)
    {
        var pending = new List<(Entity Entity, PlacedItemRef Item, Vector2 Position)>();

        world.Query(
            in BombQuery,
            (Entity entity, ref PlacedItemRef item, ref Transform2D transform) =>
            {
                if (item.Type != ItemType.Bomb || exploded.Contains(entity))
                {
                    return;
                }

                if (Math.Abs(item.GridX + 1 - blastAnchorX) < 2 && Math.Abs(item.GridY + 1 - blastAnchorY) < 2)
                {
                    pending.Add((entity, item, transform.Position));
                }
            });

        foreach (var (entity, item, position) in pending)
        {
            if (exploded.Contains(entity))
            {
                continue;
            }

            var chained = item;
            chained.Active = true;
            Detonate(world, entity, ref chained, position, exploded, audio, hooks);
        }
    }

    private static void DamagePlacedItems(
        World world,
        int blastAnchorX,
        int blastAnchorY,
        List<Entity> exploded,
        SimulationAudioBuffer? audio,
        BombSimulationHooks hooks)
    {
        var toDestroy = new List<Entity>();

        world.Query(
            in BombQuery,
            (Entity entity, ref PlacedItemRef item, ref Transform2D transform) =>
            {
                if (exploded.Contains(entity) || !ItemHealth.IsDamageable(item.Type))
                {
                    return;
                }

                if (Math.Abs(item.GridX + 1 - blastAnchorX) >= 2
                    || Math.Abs(item.GridY + 1 - blastAnchorY) >= 2)
                {
                    return;
                }

                var center = new Vector2(
                    transform.Position.X + GameConstants.TileSize / 2f,
                    transform.Position.Y + GameConstants.TileSize / 2f);
                GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Large, center);
                audio?.Play(SoundId.Explode, center);

                if (world.Has<NetworkItemRef>(entity))
                {
                    hooks.ReportItemRemoved?.Invoke(world.Get<NetworkItemRef>(entity).ItemId);
                }

                toDestroy.Add(entity);
            });

        foreach (var entity in toDestroy)
        {
            if (world.IsAlive(entity))
            {
                world.Destroy(entity);
            }
        }
    }

    private static void DamageBuildings(
        World world,
        int blastAnchorX,
        int blastAnchorY,
        SimulationAudioBuffer? audio,
        BombSimulationHooks hooks)
    {
        var buildingQuery = new QueryDescription().WithAll<BuildingRef, BuildingState, Transform2D>();
        var toDestroy = new List<Entity>();

        world.Query(
            in buildingQuery,
            (Entity entity, ref BuildingRef building, ref BuildingState state, ref Transform2D transform) =>
            {
                // Command centers are immune to bombs.
                if (BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    return;
                }

                if (Math.Abs(building.GridAnchorX - blastAnchorX) >= 3
                    || Math.Abs(building.GridAnchorY - blastAnchorY) >= 3)
                {
                    return;
                }

                var center = new Vector2(
                    transform.Position.X + GameConstants.BuildingCollisionSize / 2f,
                    transform.Position.Y + GameConstants.BuildingCollisionSize / 2f);
                GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Large, center);
                audio?.Play(SoundId.Explode, center);
                toDestroy.Add(entity);
            });

        foreach (var entity in toDestroy)
        {
            if (hooks.DestroyBuilding is not null)
            {
                hooks.DestroyBuilding(entity);
            }
            else
            {
                BuildingPopulationSystem.DetachBeforeDestroy(world, entity);
                if (world.IsAlive(entity))
                {
                    world.Destroy(entity);
                }
            }
        }
    }

    private static void DamageTanks(
        World world,
        Vector2 center,
        float radius,
        SimulationAudioBuffer? audio,
        int killerCityId,
        BombSimulationHooks hooks)
    {
        var tankQuery = new QueryDescription().WithAll<Transform2D, Health, Collider, TankLifeState>();
        var radiusSquared = radius * radius;

        world.Query(
            in tankQuery,
            (Entity entity, ref Transform2D transform, ref Health health, ref Collider collider, ref TankLifeState life) =>
            {
                if (life.IsDead)
                {
                    return;
                }

                var bounds = AxisAlignedBox.FromCollider(transform.Position, collider);
                var tankCenter = new Vector2(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
                if (Vector2.DistanceSquared(center, tankCenter) > radiusSquared)
                {
                    return;
                }

                var previousHealth = health.Current;
                life.KillerCityId = (byte)Math.Clamp(killerCityId, 0, byte.MaxValue);
                health.Current = Math.Max(0, health.Current - GameConstants.DamageMine);
                hooks.ReportHpChanged?.Invoke(entity, previousHealth, health.Current);
                audio?.Play(SoundId.Hit, tankCenter);

                if (health.Current <= 0
                    && world.Has<NetworkIdentity>(entity)
                    && hooks.ReportNetworkPlayerKilled is not null)
                {
                    hooks.ReportNetworkPlayerKilled(entity, life.KillerCityId);
                }
            });
    }
}
