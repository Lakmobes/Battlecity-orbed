using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.Collision;

public static class CollisionQueries
{
    public static PlayerCollisionResult CheckPlayerCollision(
        World world,
        TileMap map,
        Entity entity,
        Vector2 position,
        in Collider collider)
    {
        var max = GameConstants.WorldSizePixels - GameConstants.TileSize;

        if (position.X < 0)
        {
            return PlayerCollisionResult.LeftMapEdge;
        }

        if (position.X > max)
        {
            return PlayerCollisionResult.RightMapEdge;
        }

        if (position.Y < 0)
        {
            return PlayerCollisionResult.TopMapEdge;
        }

        if (position.Y > max)
        {
            return PlayerCollisionResult.BottomMapEdge;
        }

        var bounds = AxisAlignedBox.FromCollider(position, collider);

        if (TerrainCollision.IsBlocking(map, bounds))
        {
            return PlayerCollisionResult.Blocking;
        }

        if (IntersectsAnyEntity(world, entity, bounds, collider.Layer))
        {
            return PlayerCollisionResult.Blocking;
        }

        return PlayerCollisionResult.None;
    }

    public static bool IntersectsAnyEntity(
        World world,
        Entity self,
        AxisAlignedBox bounds,
        CollisionLayer layer)
    {
        var query = new QueryDescription().WithAll<Transform2D, Collider>();
        var blocked = false;

        world.Query(
            in query,
            (Entity other, ref Transform2D transform, ref Collider otherCollider) =>
            {
                if (blocked || other == self)
                {
                    return;
                }

                if (world.Has<TankLifeState>(other) && world.Get<TankLifeState>(other).IsDead)
                {
                    return;
                }

                if (!LayersBlock(layer, otherCollider.Layer))
                {
                    return;
                }

                if (otherCollider.Layer == CollisionLayer.Building && world.Has<BuildingRef>(other))
                {
                    ref var building = ref world.Get<BuildingRef>(other);
                    if (BuildingCollision.BlocksPlayerMovement(building.TypeCode, transform.Position, bounds))
                    {
                        blocked = true;
                    }
                }
                else
                {
                    var otherBounds = AxisAlignedBox.FromCollider(transform.Position, otherCollider);
                    if (bounds.Intersects(otherBounds))
                    {
                        blocked = true;
                    }
                }
            });

        return blocked;
    }

    public static bool LayersBlock(CollisionLayer a, CollisionLayer b)
    {
        if (a == CollisionLayer.None || b == CollisionLayer.None)
        {
            return false;
        }

        // Tanks pass through each other (legacy CheckPlayerCollision never blocked on other players).
        return a == CollisionLayer.Player && b == CollisionLayer.Building
            || a == CollisionLayer.Building && b == CollisionLayer.Player
            || a == CollisionLayer.Player && b == CollisionLayer.Item
            || a == CollisionLayer.Item && b == CollisionLayer.Player;
    }

    public static bool IntersectsBlockingCollider(World world, Entity self, AxisAlignedBox bounds)
    {
        var query = new QueryDescription().WithAll<Transform2D, Collider>();
        var blocked = false;

        world.Query(
            in query,
            (Entity other, ref Transform2D transform, ref Collider collider) =>
            {
                if (blocked || other == self)
                {
                    return;
                }

                if (collider.Layer is not (CollisionLayer.Building or CollisionLayer.Item))
                {
                    return;
                }

                if (collider.Layer == CollisionLayer.Building && world.Has<BuildingRef>(other))
                {
                    ref var building = ref world.Get<BuildingRef>(other);
                    var tileCenter = new Vector2(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
                    if (BuildingCollision.BlocksItemPlacement(tileCenter, building.TypeCode, transform.Position))
                    {
                        blocked = true;
                    }
                }
                else
                {
                    var otherBounds = AxisAlignedBox.FromCollider(transform.Position, collider);
                    if (bounds.Intersects(otherBounds))
                    {
                        blocked = true;
                    }
                }
            });

        return blocked;
    }

    /// <summary>True when <paramref name="bounds"/> overlaps another item collider (not buildings).</summary>
    public static bool IntersectsItemCollider(
        World world,
        Entity self,
        AxisAlignedBox bounds,
        Entity ignoreOwner = default)
    {
        var query = new QueryDescription().WithAll<Transform2D, Collider>();
        var blocked = false;

        world.Query(
            in query,
            (Entity other, ref Transform2D transform, ref Collider collider) =>
            {
                if (blocked
                    || other == self
                    || other == ignoreOwner
                    || collider.Layer != CollisionLayer.Item)
                {
                    return;
                }

                var otherBounds = AxisAlignedBox.FromCollider(transform.Position, collider);
                if (bounds.Intersects(otherBounds))
                {
                    blocked = true;
                }
            });

        return blocked;
    }
}
