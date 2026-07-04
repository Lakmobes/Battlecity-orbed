using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.Ecs.Systems;

public static class TankStatusSystem
{
    private static readonly QueryDescription StatusQuery =
        new QueryDescription().WithAll<TankStatus>();

    public static void Update(World world, float deltaSeconds)
    {
        world.Query(
            in StatusQuery,
            (ref TankStatus status) =>
            {
                if (status.IsCloaked)
                {
                    status.CloakRemainingSeconds -= deltaSeconds;
                    if (status.CloakRemainingSeconds <= 0f)
                    {
                        status.IsCloaked = false;
                        status.CloakRemainingSeconds = 0f;
                    }
                }

                if (status.IsFrozen)
                {
                    status.FrozenRemainingSeconds -= deltaSeconds;
                    if (status.FrozenRemainingSeconds <= 0f)
                    {
                        status.IsFrozen = false;
                        status.FrozenRemainingSeconds = 0f;
                    }
                }
            });
    }

    public static void ActivateCloak(ref TankStatus status)
    {
        status.IsCloaked = true;
        status.CloakRemainingSeconds = GameConstants.TimerCloak / 1000f;
    }

    public static void ActivateFreeze(ref TankStatus status)
    {
        status.IsFrozen = true;
        status.FrozenRemainingSeconds = GameConstants.TimerDfg / 1000f;
    }
}
