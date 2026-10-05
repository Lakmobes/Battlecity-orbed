using Arch.Core;

using BattleCity.Core.City;
using BattleCity.Core.Ecs.Components;

namespace BattleCity.Core.Ecs.Systems;

public static class CityOrbedNotificationSystem
{
    private const float OverlayDurationSeconds = 8f;

    private static readonly QueryDescription PlayerQuery =
        new QueryDescription().WithAll<InputControlled, CityAffiliation, CityOrbedState>();

    public static void Update(World world, float deltaSeconds)
    {
        world.Query(
            in PlayerQuery,
            (ref CityOrbedState orbed) =>
            {
                if (!orbed.ShowOverlay)
                {
                    return;
                }

                orbed.RemainingSeconds -= deltaSeconds;
                if (orbed.RemainingSeconds <= 0f)
                {
                    orbed.ShowOverlay = false;
                    orbed.RemainingSeconds = 0f;
                    orbed.Message = string.Empty;
                }
            });
    }

    public static void Trigger(
        World world,
        int victimCityId,
        int attackerCityId,
        string? victimCityName,
        string? attackerCityName,
        uint points = 0,
        uint orberCityPoints = 0)
    {
        var victimLabel = string.IsNullOrWhiteSpace(victimCityName) ? "Your city" : victimCityName;
        var attackerLabel = string.IsNullOrWhiteSpace(attackerCityName) ? "An enemy" : attackerCityName;

        world.Query(
            in PlayerQuery,
            (ref CityAffiliation city, ref CityOrbedState orbed) =>
            {
                if (city.CityId == victimCityId)
                {
                    orbed.ShowOverlay = true;
                    orbed.RemainingSeconds = OverlayDurationSeconds;
                    orbed.IsVictim = true;
                    orbed.Message = "Your city has been destroyed by an orb!";
                    return;
                }

                if (city.CityId == attackerCityId && attackerCityId != victimCityId)
                {
                    orbed.ShowOverlay = true;
                    orbed.RemainingSeconds = OverlayDurationSeconds;
                    orbed.IsVictim = false;
                    orbed.Message = OrbAwardMessages.Format(
                        attackerLabel,
                        victimLabel,
                        points,
                        orberCityPoints);
                }
            });
    }
}
