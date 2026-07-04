using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Systems;

public static class BulletSystem
{
    private static readonly QueryDescription Query =
        new QueryDescription().WithAll<BulletRef, Transform2D, Velocity, Lifetime, SpriteRef>();

    public static void PrepareMovement(World world, float deltaSeconds)
    {
        world.Query(
            in Query,
            (ref BulletRef bullet, ref Velocity velocity) =>
            {
                velocity.Value = ComputeBulletVelocity(bullet.Kind, bullet.Direction, deltaSeconds);
            });
    }

    public static void UpdateAfterMovement(World world, float deltaSeconds)
    {
        var expired = new List<Entity>();

        world.Query(
            in Query,
            (Entity entity, ref BulletRef bullet, ref Transform2D transform, ref Lifetime lifetime, ref SpriteRef sprite) =>
            {
                lifetime.Remaining -= deltaSeconds * 1000f * GameConstants.MovementSpeedBullet;
                bullet.CollisionGraceSeconds = Math.Max(0f, bullet.CollisionGraceSeconds - deltaSeconds);

                bullet.AnimationFrame = (bullet.AnimationFrame + 1) % 4;
                var (sourceX, sourceY) = BulletSprites.GetSourceOrigin(bullet.Kind, bullet.AnimationFrame);
                sprite.SourceX = sourceX;
                sprite.SourceY = sourceY;

                if (lifetime.Remaining <= 0f || IsOffMap(transform.Position))
                {
                    expired.Add(entity);
                }
            });

        foreach (var entity in expired)
        {
            world.Destroy(entity);
        }
    }

    public static Vector2 ComputeBulletVelocity(BulletKind kind, int direction, float deltaSeconds)
    {
        var radians = InputSystem.LegacyDirectionToRadians(direction);
        var moveFactor = BulletStats.GetMoveFactor(kind);
        var speed = moveFactor * deltaSeconds * 1000f;
        var velocity = new Vector2(
            MathF.Sin(radians) * speed,
            MathF.Cos(radians) * speed);

        const float maxStep = 20f;
        velocity.X = Math.Clamp(velocity.X, -maxStep, maxStep);
        velocity.Y = Math.Clamp(velocity.Y, -maxStep, maxStep);
        return velocity / deltaSeconds;
    }

    private static bool IsOffMap(Vector2 position) =>
        position.X < 0f
        || position.Y < 0f
        || position.X > GameConstants.WorldSizePixels
        || position.Y > GameConstants.WorldSizePixels;
}
