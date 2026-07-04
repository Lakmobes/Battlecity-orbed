using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.Ecs.Systems;

public static class CityAlertSystem
{
    private const float ArrowFlashIntervalSeconds = 0.5f;

    private static readonly QueryDescription PlayerAlertQuery =
        new QueryDescription().WithAll<InputControlled, CityAffiliation, CityAlertState>();

    public static void Update(World world, float deltaSeconds)
    {
        world.Query(
            in PlayerAlertQuery,
            (ref CityAlertState alert) =>
            {
                if (alert.IsUnderAttack)
                {
                    alert.UnderAttackRemainingSeconds -= deltaSeconds;
                    if (alert.UnderAttackRemainingSeconds <= 0f)
                    {
                        alert.IsUnderAttack = false;
                        alert.UnderAttackRemainingSeconds = 0f;
                    }
                }

                alert.ArrowFlashTimerSeconds -= deltaSeconds;
                if (alert.ArrowFlashTimerSeconds <= 0f)
                {
                    alert.ArrowFlashTimerSeconds = ArrowFlashIntervalSeconds;
                    if (alert.IsUnderAttack)
                    {
                        alert.FlashArrowVisible = !alert.FlashArrowVisible;
                    }
                    else
                    {
                        alert.FlashArrowVisible = false;
                    }
                }
            });
    }

    public static void TriggerForCity(World world, int cityId)
    {
        world.Query(
            in PlayerAlertQuery,
            (ref CityAffiliation city, ref CityAlertState alert) =>
            {
                if (city.CityId != cityId)
                {
                    return;
                }

                alert.IsUnderAttack = true;
                alert.UnderAttackRemainingSeconds = GameConstants.TimerUnderAttack / 1000f;
                alert.FlashArrowVisible = true;
            });
    }

    public static bool TryGetBulletOwnerCity(World world, Entity bulletEntity, out int ownerCityId)
    {
        ownerCityId = 0;
        if (!world.Has<BulletRef>(bulletEntity))
        {
            return false;
        }

        var owner = world.Get<BulletRef>(bulletEntity).Owner;
        if (!world.IsAlive(owner) || !world.Has<CityAffiliation>(owner))
        {
            return false;
        }

        ownerCityId = world.Get<CityAffiliation>(owner).CityId;
        return true;
    }
}
