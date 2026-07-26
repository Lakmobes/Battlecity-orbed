using Arch.Core;

using BattleCity.Core.Ecs.Components;

namespace BattleCity.Core.Ecs.Systems;

/// <summary>Cycles building atlas frames (slower than legacy 500ms for readability).</summary>
public static class BuildingAnimationSystem
{
    public const float FrameIntervalSeconds = 0.85f;
    private const int FrameCount = 6;

    private static readonly QueryDescription Query =
        new QueryDescription().WithAll<BuildingState>();

    public static void Update(World world, float deltaSeconds)
    {
        world.Query(
            in Query,
            (ref BuildingState state) =>
            {
                state.AnimationCooldownSeconds -= deltaSeconds;
                if (state.AnimationCooldownSeconds > 0f)
                {
                    return;
                }

                state.AnimationCooldownSeconds = FrameIntervalSeconds;
                state.AnimationFrame++;
                if (state.AnimationFrame >= FrameCount)
                {
                    state.AnimationFrame = 0;
                }
            });
    }
}
