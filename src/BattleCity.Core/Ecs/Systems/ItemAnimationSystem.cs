using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;

namespace BattleCity.Core.Ecs.Systems;

/// <summary>
/// Advances placed-item atlas frames. Lit bombs animate; inactive bombs stay on frame 0.
/// Turrets use a separate sheet and are skipped.
/// </summary>
public static class ItemAnimationSystem
{
    private static readonly QueryDescription Query =
        new QueryDescription().WithAll<PlacedItemRef, SpriteRef>().WithNone<TurretState>();

    private static float _elapsedSeconds;

    public static float ElapsedSeconds => _elapsedSeconds;

    public static void Update(World world, float deltaSeconds)
    {
        _elapsedSeconds += deltaSeconds;

        world.Query(
            in Query,
            (ref PlacedItemRef item, ref SpriteRef sprite) =>
            {
                if (!ItemSprites.UsesItemSheetAnimation(item.Type))
                {
                    return;
                }

                var frame = ItemSprites.ResolveAnimationFrame(item.Type, item.Active, _elapsedSeconds);
                var (sourceX, sourceY) = ItemSprites.GetWorldSpriteOrigin(item.Type, frame);
                sprite.SourceX = sourceX;
                sprite.SourceY = sourceY;
            });
    }

    public static void Reset() => _elapsedSeconds = 0f;
}
