using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Systems;

public static class ExplosionSystem
{
    private const float FrameDurationSeconds = 1f / 30f;

    private static readonly QueryDescription Query =
        new QueryDescription().WithAll<ExplosionRef, Transform2D, SpriteRef>();

    public static void Update(World world, float deltaSeconds)
    {
        var expired = new List<Entity>();

        world.Query(
            in Query,
            (Entity entity, ref ExplosionRef explosion, ref SpriteRef sprite) =>
            {
                explosion.FrameTimerSeconds -= deltaSeconds;
                if (explosion.FrameTimerSeconds > 0f)
                {
                    return;
                }

                explosion.AnimationFrame++;
                explosion.FrameTimerSeconds = FrameDurationSeconds;

                var maxFrames = ExplosionSprites.GetFrameCount(explosion.Kind);
                if (explosion.AnimationFrame >= maxFrames)
                {
                    expired.Add(entity);
                    return;
                }

                ApplyFrame(ref explosion, ref sprite);
            });

        foreach (var entity in expired)
        {
            world.Destroy(entity);
        }
    }

    private static void ApplyFrame(ref ExplosionRef explosion, ref SpriteRef sprite)
    {
        var (sourceX, sourceY, width, height) = ExplosionSprites.GetFrameRect(explosion.Kind, explosion.AnimationFrame);
        sprite.SourceX = sourceX;
        sprite.SourceY = sourceY;
        sprite.Width = width;
        sprite.Height = height;
    }
}
