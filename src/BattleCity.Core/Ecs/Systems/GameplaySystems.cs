using System.Numerics;

using Arch.Core;

using BattleCity.Core.Collision;
using BattleCity.Core.Audio;
using BattleCity.Core.City;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Systems;

public static class ItemDropSystem
{
    private static readonly QueryDescription PlayerQuery =
        new QueryDescription().WithAll<InputControlled, InputCommand, Transform2D, TankFacing, TankLifeState, PlayerInventory, TankStatus>();

    public static void Update(
        World world,
        TileMap? tileMap = null,
        SimulationAudioBuffer? audio = null,
        bool suppressNetworkedActions = false,
        CityBuildState? cityBuild = null)
    {
        world.Query(
            in PlayerQuery,
            (Entity entity, ref InputCommand input, ref Transform2D transform, ref TankFacing facing, ref TankLifeState life, ref PlayerInventory inventory, ref TankStatus status) =>
            {
                if (life.IsDead)
                {
                    return;
                }

                if (status.IsFrozen)
                {
                    return;
                }

                if (input.CycleInventoryPreviousPressed)
                {
                    inventory.CycleSelection(-1);
                }

                if (input.CycleInventoryNextPressed)
                {
                    inventory.CycleSelection(1);
                }

                if (suppressNetworkedActions)
                {
                    return;
                }

                if (input.UseCloakPressed && inventory.TryConsume(ItemType.Cloak))
                {
                    TankStatusSystem.ActivateCloak(ref status);
                    audio?.Play(SoundId.Cloak, transform.Position);
                }

                if (input.DropSelectedItemPressed)
                {
                    if (TryDropSelectedItem(world, entity, ref inventory, transform.Position, cityBuild, tileMap))
                    {
                        audio?.Play(SoundId.Click, transform.Position);
                    }
                }

                if (input.DropBombPressed && inventory.GetCount(ItemType.Bomb) > 0)
                {
                    if (TryDropItem(world, entity, transform.Position, ItemType.Bomb, active: true, cityBuild, tileMap))
                    {
                        inventory.TryConsume(ItemType.Bomb);
                        audio?.Play(SoundId.Click, transform.Position);
                    }
                }

                if (input.DropOrbPressed && inventory.GetCount(ItemType.Orb) > 0)
                {
                    if (TryDropItem(world, entity, transform.Position, ItemType.Orb, active: false, cityBuild, tileMap))
                    {
                        inventory.TryConsume(ItemType.Orb);
                        audio?.Play(SoundId.Click, transform.Position);
                    }
                }

                if (input.UseMedKitPressed && inventory.TryConsume(ItemType.MedKit))
                {
                    if (world.Has<Health>(entity))
                    {
                        ref var health = ref world.Get<Health>(entity);
                        health.Current = health.Max;
                        audio?.Play(SoundId.Click, transform.Position);
                    }
                }

                if (input.PickUpItemPressed)
                {
                    if (ItemPickupActions.TryFindItemAtTank(world, entity, out var itemEntity, out var itemType, out _)
                        && ItemPickupActions.TryPickUp(world, entity, ref inventory, itemEntity, itemType))
                    {
                        audio?.Play(SoundId.Click, transform.Position);
                    }
                }
            });
    }

    private static bool TryDropSelectedItem(
        World world,
        Entity owner,
        ref PlayerInventory inventory,
        Vector2 tankTopLeft,
        CityBuildState? cityBuild,
        TileMap? tileMap)
    {
        if (!ItemCatalog.IsPlaceable(inventory.SelectedItemType))
        {
            return false;
        }

        if (inventory.GetCount(inventory.SelectedItemType) <= 0)
        {
            return false;
        }

        // Planted bombs stay until armed with B; other placeables activate immediately.
        var active = inventory.SelectedItemType != ItemType.Bomb;
        if (!TryDropItem(world, owner, tankTopLeft, inventory.SelectedItemType, active, cityBuild, tileMap))
        {
            return false;
        }

        inventory.TryConsume(inventory.SelectedItemType);
        inventory.SelectNextAvailablePlaceable();
        return true;
    }

    private static bool TryDropItem(
        World world,
        Entity owner,
        Vector2 tankTopLeft,
        ItemType type,
        bool active,
        CityBuildState? cityBuild,
        TileMap? tileMap) =>
        ItemDropActions.TryDropForEntity(
            world,
            owner,
            tankTopLeft,
            type,
            active,
            out _,
            out _,
            cityBuild: cityBuild,
            tileMap: tileMap);
}

public static class BulletCollisionSystem
{
    private static readonly QueryDescription BulletQuery =
        new QueryDescription().WithAll<BulletRef, Transform2D, Collider, Damage, Lifetime>();

    public static void Resolve(
        World world,
        TileMap map,
        SimulationAudioBuffer? audio = null,
        Action<Entity, int, int>? onHealthChanged = null,
        bool applyDamageToNetworkPlayers = true)
    {
        var hits = new List<Entity>();

        world.Query(
            in BulletQuery,
            (Entity bulletEntity, ref BulletRef bullet, ref Transform2D transform, ref Collider collider, ref Damage damage) =>
            {
                if (hits.Contains(bulletEntity))
                {
                    return;
                }

                if (bullet.CollisionGraceSeconds > 0f)
                {
                    return;
                }

                var bounds = AxisAlignedBox.FromCollider(transform.Position, collider);
                var impactPoint = new Vector2(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);

                if (TerrainCollision.IsBlockingForBullet(map, bounds))
                {
                    hits.Add(bulletEntity);
                    GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Small, impactPoint);
                    audio?.Play(SoundId.Explode, impactPoint);
                    return;
                }

                if (TryHitPlacedItem(world, bulletEntity, bounds, damage.Value, hits, audio))
                {
                    return;
                }

                // Buildings are handled by TryHitBuilding (population damage). Do not hard-kill
                // bullets on building colliders — that blocked shots into items on factory bays.
                if (TryHitBuilding(world, bulletEntity, bounds, hits, audio))
                {
                    return;
                }

                if (CollisionQueries.IntersectsItemCollider(world, bulletEntity, bounds))
                {
                    hits.Add(bulletEntity);
                    GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Small, impactPoint);
                    audio?.Play(SoundId.Explode, impactPoint);
                    return;
                }

                ApplyDamageToTargets(
                    world,
                    bulletEntity,
                    bullet.Owner,
                    bounds,
                    damage.Value,
                    hits,
                    audio,
                    onHealthChanged,
                    applyDamageToNetworkPlayers);
            });

        foreach (var bulletEntity in hits)
        {
            world.Destroy(bulletEntity);
        }
    }

    private static void ApplyDamageToTargets(
        World world,
        Entity bulletEntity,
        Entity owner,
        AxisAlignedBox bounds,
        int damage,
        List<Entity> hits,
        SimulationAudioBuffer? audio,
        Action<Entity, int, int>? onHealthChanged,
        bool applyDamageToNetworkPlayers)
    {
        var targetQuery = new QueryDescription().WithAll<Transform2D, Collider, Health>();
        var inset = Shared.Constants.GameConstants.PlayerCollisionInset;
        var expanded = new AxisAlignedBox(
            bounds.Left - inset,
            bounds.Top - inset,
            bounds.Width + inset * 2,
            bounds.Height + inset * 2);

        world.Query(
            in targetQuery,
            (Entity target, ref Transform2D transform, ref Collider collider, ref Health health) =>
            {
                if (target == owner || target == bulletEntity || hits.Contains(bulletEntity))
                {
                    return;
                }

                if (!applyDamageToNetworkPlayers
                    && world.Has<NetworkIdentity>(target)
                    && !world.Has<InputControlled>(target))
                {
                    return;
                }

                if (world.Has<TankLifeState>(target) && world.Get<TankLifeState>(target).IsDead)
                {
                    return;
                }

                var targetBounds = AxisAlignedBox.FromCollider(transform.Position, collider);
                if (!expanded.Intersects(targetBounds))
                {
                    return;
                }

                var targetCenter = new Vector2(
                    targetBounds.Left + targetBounds.Width / 2f,
                    targetBounds.Top + targetBounds.Height / 2f);
                var previousHealth = health.Current;
                health.Current = Math.Max(0, health.Current - damage);
                if (world.Has<TankLifeState>(target))
                {
                    ref var life = ref world.Get<TankLifeState>(target);
                    life.KillerCityId = EntityCityLookup.GetCityId(world, owner);
                }

                onHealthChanged?.Invoke(target, previousHealth, health.Current);
                audio?.Play(SoundId.Hit, targetCenter);
                if (health.Current <= 0)
                {
                    GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Small, targetCenter);
                }

                hits.Add(bulletEntity);
            });
    }

    private static bool TryHitPlacedItem(
        World world,
        Entity bulletEntity,
        AxisAlignedBox bulletBounds,
        int damage,
        List<Entity> hits,
        SimulationAudioBuffer? audio)
    {
        var itemQuery = new QueryDescription().WithAll<PlacedItemRef, Transform2D, Health>();
        var impactPoint = new Vector2(
            bulletBounds.Left + bulletBounds.Width / 2f,
            bulletBounds.Top + bulletBounds.Height / 2f);
        var hit = false;

        world.Query(
            in itemQuery,
            (Entity entity, ref PlacedItemRef item, ref Transform2D transform, ref Health health) =>
            {
                if (hit || !ItemHealth.IsDamageable(item.Type))
                {
                    return;
                }

                // Full tile at the item's world position (walls use GridToWorldPosition, not legacy -48 offset).
                var itemBounds = new AxisAlignedBox(
                    transform.Position.X,
                    transform.Position.Y,
                    GameConstants.TileSize,
                    GameConstants.TileSize);

                if (!itemBounds.Intersects(bulletBounds))
                {
                    return;
                }

                hit = true;
                health.Current = Math.Max(0, health.Current - damage);
                MaybeTriggerUnderAttack(world, bulletEntity, item.CityId);
                audio?.Play(SoundId.Hit, impactPoint);
                GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Small, impactPoint);

                if (health.Current <= 0)
                {
                    world.Destroy(entity);
                }

                hits.Add(bulletEntity);
            });

        return hit;
    }

    private static bool TryHitBuilding(
        World world,
        Entity bulletEntity,
        AxisAlignedBox bulletBounds,
        List<Entity> hits,
        SimulationAudioBuffer? audio)
    {
        var buildingQuery = new QueryDescription().WithAll<BuildingRef, BuildingState, Transform2D>();
        var impactPoint = new Vector2(
            bulletBounds.Left + bulletBounds.Width / 2f,
            bulletBounds.Top + bulletBounds.Height / 2f);
        var hit = false;

        world.Query(
            in buildingQuery,
            (Entity entity, ref BuildingRef building, ref BuildingState state) =>
            {
                if (hit)
                {
                    return;
                }

                var buildingBounds = GetBuildingBulletBounds(building);
                if (!buildingBounds.Intersects(bulletBounds))
                {
                    return;
                }

                hit = true;
                if (BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    hits.Add(bulletEntity);
                    GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Small, impactPoint);
                    audio?.Play(SoundId.Explode, impactPoint);
                    return;
                }

                if (!BuildingCatalog.IsResearch(building.TypeCode))
                {
                    state.Population = Math.Max(0, state.Population - GameConstants.DamageLaser);
                    MaybeTriggerUnderAttack(world, bulletEntity, defendedCityId: 0);
                    if (state.Population <= 0)
                    {
                        world.Destroy(entity);
                    }
                }

                hits.Add(bulletEntity);
                GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Small, impactPoint);
                audio?.Play(SoundId.Explode, impactPoint);
            });

        return hit;
    }

    private static AxisAlignedBox GetBuildingBulletBounds(BuildingRef building)
    {
        var spriteTopLeft = BuildingPlacement.GridAnchorToWorldPosition(
            building.GridAnchorX,
            building.GridAnchorY);
        return BuildingCollision.GetPlayerBlockingBounds(building.TypeCode, spriteTopLeft);
    }

    private static void MaybeTriggerUnderAttack(World world, Entity bulletEntity, int defendedCityId)
    {
        if (!CityAlertSystem.TryGetBulletOwnerCity(world, bulletEntity, out var attackerCityId)
            || attackerCityId == defendedCityId)
        {
            return;
        }

        CityAlertSystem.TriggerForCity(world, defendedCityId);
    }
}
