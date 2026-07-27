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
        new QueryDescription().WithAll<InputControlled, InputCommand, Transform2D, TankFacing, TankLifeState, PlayerInventory, TankStatus, WeaponState, CityAffiliation>();

    public static void Update(
        World world,
        TileMap? tileMap = null,
        SimulationAudioBuffer? audio = null,
        bool suppressNetworkedActions = false,
        Func<int, CityBuildState?>? resolveCityBuild = null)
    {
        world.Query(
            in PlayerQuery,
            (Entity entity, ref InputCommand input, ref Transform2D transform, ref TankFacing facing, ref TankLifeState life, ref PlayerInventory inventory, ref TankStatus status, ref WeaponState weapons, ref CityAffiliation city) =>
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

                var cityBuild = resolveCityBuild?.Invoke(city.CityId);

                if (input.UseCloakPressed
                    && WeaponActions.TryConsumeCloak(ref weapons, ref inventory, cityBuild))
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
                    if (ItemPickupActions.TryFindItemAtTank(
                            world,
                            entity,
                            out var itemEntity,
                            out var itemType,
                            out _)
                        && ItemPickupActions.TryPickUp(world, entity, ref inventory, itemEntity, itemType))
                    {
                        audio?.Play(SoundId.Click, transform.Position);
                    }
                }
            });
    }

    /// <summary>Backward-compatible overload for tests/call sites that pass a single city build.</summary>
    public static void Update(
        World world,
        TileMap? tileMap,
        SimulationAudioBuffer? audio,
        bool suppressNetworkedActions,
        CityBuildState? cityBuild) =>
        Update(world, tileMap, audio, suppressNetworkedActions, _ => cityBuild);

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
        bool applyDamageToNetworkPlayers = true,
        int defendedCityId = 0)
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

                var bounds = AxisAlignedBox.FromCollider(transform.Position, collider);
                var impactPoint = new Vector2(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);

                // Grace only skips tank damage so the muzzle does not hit the shooter.
                // Terrain/walls/buildings must still collide on the first frames — otherwise
                // shots fired while pressed against a wall travel through during grace.
                if (TerrainCollision.IsBlockingForBullet(map, bounds))
                {
                    hits.Add(bulletEntity);
                    GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Small, impactPoint);
                    audio?.Play(SoundId.Explode, impactPoint);
                    return;
                }

                if (TryHitPlacedItem(
                        world,
                        bulletEntity,
                        bullet.Owner,
                        bounds,
                        transform.PreviousPosition,
                        collider,
                        damage.Value,
                        hits,
                        audio))
                {
                    return;
                }

                // Buildings are handled by TryHitBuilding (population damage). Do not hard-kill
                // bullets on building colliders — that blocked shots into items on factory bays.
                if (TryHitBuilding(
                        world,
                        bulletEntity,
                        bounds,
                        transform.PreviousPosition,
                        collider,
                        hits,
                        audio,
                        defendedCityId))
                {
                    return;
                }

                if (CollisionQueries.IntersectsItemCollider(world, bulletEntity, bounds, bullet.Owner))
                {
                    hits.Add(bulletEntity);
                    GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Small, impactPoint);
                    audio?.Play(SoundId.Explode, impactPoint);
                    return;
                }

                if (bullet.CollisionGraceSeconds > 0f)
                {
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
        Entity owner,
        AxisAlignedBox bulletBounds,
        Vector2 previousPosition,
        in Collider collider,
        int damage,
        List<Entity> hits,
        SimulationAudioBuffer? audio)
    {
        var itemQuery = new QueryDescription().WithAll<PlacedItemRef, Transform2D, Health>();
        var previousBounds = AxisAlignedBox.FromCollider(previousPosition, collider);
        var previousCenter = new Vector2(
            previousBounds.Left + previousBounds.Width / 2f,
            previousBounds.Top + previousBounds.Height / 2f);
        var currentCenter = new Vector2(
            bulletBounds.Left + bulletBounds.Width / 2f,
            bulletBounds.Top + bulletBounds.Height / 2f);
        var hit = false;

        world.Query(
            in itemQuery,
            (Entity entity, ref PlacedItemRef item, ref Transform2D transform, ref Health health) =>
            {
                // Turrets must not damage themselves when their own shot clips the tile.
                if (hit || entity == owner || !ItemHealth.IsDamageable(item.Type))
                {
                    return;
                }

                // Full tile at the item's world position (walls use GridToWorldPosition, not legacy -48 offset).
                var itemBounds = new AxisAlignedBox(
                    transform.Position.X,
                    transform.Position.Y,
                    GameConstants.TileSize,
                    GameConstants.TileSize);

                if (!itemBounds.Intersects(bulletBounds)
                    && !itemBounds.Intersects(previousBounds)
                    && itemBounds.TryGetSegmentEntryPoint(previousCenter, currentCenter) is null)
                {
                    return;
                }

                hit = true;
                var impactPoint = itemBounds.TryGetSegmentEntryPoint(previousCenter, currentCenter)
                    ?? itemBounds.ClosestPoint(currentCenter);
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
        Vector2 previousPosition,
        in Collider collider,
        List<Entity> hits,
        SimulationAudioBuffer? audio,
        int defendedCityId)
    {
        var buildingQuery = new QueryDescription().WithAll<BuildingRef, BuildingState, Transform2D>();
        var previousBounds = AxisAlignedBox.FromCollider(previousPosition, collider);
        var previousCenter = new Vector2(
            previousBounds.Left + previousBounds.Width / 2f,
            previousBounds.Top + previousBounds.Height / 2f);
        var currentCenter = new Vector2(
            bulletBounds.Left + bulletBounds.Width / 2f,
            bulletBounds.Top + bulletBounds.Height / 2f);
        var hit = false;
        var ownerIsTurret = IsTurretOwnedBullet(world, bulletEntity);

        world.Query(
            in buildingQuery,
            (Entity entity, ref BuildingRef building, ref BuildingState state) =>
            {
                if (hit)
                {
                    return;
                }

                var buildingBounds = BuildingCollision.GetBulletHitBounds(
                    building.TypeCode,
                    building.GridAnchorX,
                    building.GridAnchorY);
                if (!buildingBounds.Intersects(bulletBounds)
                    && !buildingBounds.Intersects(previousBounds)
                    && buildingBounds.TryGetSegmentEntryPoint(previousCenter, currentCenter) is null)
                {
                    return;
                }

                hit = true;
                var impactPoint = buildingBounds.TryGetSegmentEntryPoint(previousCenter, currentCenter)
                    ?? buildingBounds.ClosestPoint(currentCenter);

                // Legacy: bullets stop on buildings; populated buildings are immune.
                // Empty buildings (pop 0) are destroyed in one hit. Bombs ignore population.
                if (!ownerIsTurret
                    && !BuildingCatalog.IsCommandCenter(building.TypeCode)
                    && state.Population <= 0)
                {
                    MaybeTriggerUnderAttack(world, bulletEntity, defendedCityId);
                    BuildingPopulationSystem.DetachBeforeDestroy(world, entity);
                    world.Destroy(entity);
                }
                else if (!ownerIsTurret && !BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    MaybeTriggerUnderAttack(world, bulletEntity, defendedCityId);
                }

                hits.Add(bulletEntity);
                GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Small, impactPoint);
                audio?.Play(SoundId.Explode, impactPoint);
            });

        return hit;
    }

    private static bool IsTurretOwnedBullet(World world, Entity bulletEntity)
    {
        if (!world.IsAlive(bulletEntity) || !world.Has<BulletRef>(bulletEntity))
        {
            return false;
        }

        var owner = world.Get<BulletRef>(bulletEntity).Owner;
        return owner != default && world.IsAlive(owner) && world.Has<TurretState>(owner);
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
