using System.Numerics;

using Arch.Core;

using BattleCity.Core.Collision;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.Ecs.Systems;

public static class CollisionSystem
{
    private static readonly QueryDescription ColliderQuery =
        new QueryDescription()
            .WithAll<Transform2D, Collider, Velocity>()
            .WithNone<BulletRef>();

    public static void Resolve(World world, TileMap map)
    {
        world.Query(
            in ColliderQuery,
            (Entity entity, ref Transform2D transform, ref Collider collider, ref Velocity velocity) =>
            {
                var previous = transform.PreviousPosition;
                var delta = transform.Position - previous;

                if (delta == Vector2.Zero)
                {
                    ApplyMapEdgeClamp(ref transform, ref velocity, entity, world);
                    return;
                }

                var result = CollisionQueries.CheckPlayerCollision(
                    world,
                    map,
                    entity,
                    transform.Position,
                    collider);

                if (result == PlayerCollisionResult.None)
                {
                    return;
                }

                if (result is PlayerCollisionResult.LeftMapEdge
                    or PlayerCollisionResult.RightMapEdge
                    or PlayerCollisionResult.TopMapEdge
                    or PlayerCollisionResult.BottomMapEdge)
                {
                    ApplyMapEdgeClamp(ref transform, ref velocity, entity, world);
                    return;
                }

                ResolveBlocking(world, map, entity, ref transform, ref collider, ref velocity, previous, delta);
            });
    }

    private static void ResolveBlocking(
        World world,
        TileMap map,
        Entity entity,
        ref Transform2D transform,
        ref Collider collider,
        ref Velocity velocity,
        Vector2 previous,
        Vector2 delta)
    {
        var isPatrol = world.Has<PatrolBehavior>(entity);

        transform.Position = previous + new Vector2(delta.X, 0f);
        var xBlocked = CollisionQueries.CheckPlayerCollision(
            world,
            map,
            entity,
            transform.Position,
            collider) != PlayerCollisionResult.None;

        if (xBlocked)
        {
            transform.Position = previous;
            if (isPatrol)
            {
                velocity.Value = new Vector2(-velocity.Value.X, velocity.Value.Y);
            }
            else
            {
                velocity.Value = new Vector2(0f, velocity.Value.Y);
            }
        }

        var xResolved = transform.Position;
        transform.Position = xResolved + new Vector2(0f, delta.Y);
        var yBlocked = CollisionQueries.CheckPlayerCollision(
            world,
            map,
            entity,
            transform.Position,
            collider) != PlayerCollisionResult.None;

        if (yBlocked)
        {
            transform.Position = xResolved;
            if (isPatrol)
            {
                velocity.Value = new Vector2(velocity.Value.X, -velocity.Value.Y);
            }
            else
            {
                velocity.Value = new Vector2(velocity.Value.X, 0f);
            }
        }
    }

    private static void ApplyMapEdgeClamp(
        ref Transform2D transform,
        ref Velocity velocity,
        Entity entity,
        World world)
    {
        var max = GameConstants.WorldSizePixels - GameConstants.TileSize;
        var isPatrol = world.Has<PatrolBehavior>(entity);
        var x = transform.Position.X;
        var y = transform.Position.Y;

        if (x < 0f || x > max)
        {
            if (isPatrol)
            {
                velocity.Value = new Vector2(-velocity.Value.X, velocity.Value.Y);
            }
            else
            {
                velocity.Value = new Vector2(0f, velocity.Value.Y);
            }
        }

        if (y < 0f || y > max)
        {
            if (isPatrol)
            {
                velocity.Value = new Vector2(velocity.Value.X, -velocity.Value.Y);
            }
            else
            {
                velocity.Value = new Vector2(velocity.Value.X, 0f);
            }
        }

        transform.Position = new Vector2(
            Math.Clamp(x, 0, max),
            Math.Clamp(y, 0, max));
    }
}
