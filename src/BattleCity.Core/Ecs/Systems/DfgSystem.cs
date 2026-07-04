using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ai;
using BattleCity.Core.Audio;
using BattleCity.Core.Collision;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Systems;

public static class DfgSystem
{
    private static readonly QueryDescription TankQuery =
        new QueryDescription().WithAll<Transform2D, TankLifeState, CityAffiliation, TankStatus>();

    private static readonly QueryDescription DfgQuery =
        new QueryDescription().WithAll<PlacedItemRef, Transform2D>();

    public static void Update(World world, SimulationAudioBuffer? audio = null)
    {
        world.Query(
            in TankQuery,
            (Entity tankEntity, ref Transform2D transform, ref TankLifeState life, ref CityAffiliation city, ref TankStatus status) =>
            {
                if (life.IsDead || status.IsFrozen)
                {
                    return;
                }

                var tankCenter = TurretTargeting.GetTankCenter(transform.Position);
                if (!TryFindTriggeredDfg(world, tankCenter, city.CityId, out var dfgEntity))
                {
                    return;
                }

                TriggerDfg(world, dfgEntity, tankEntity, ref status, audio);
            });
    }

    private static bool TryFindTriggeredDfg(
        World world,
        Vector2 tankCenter,
        int tankCityId,
        out Entity dfgEntity)
    {
        dfgEntity = default;
        var found = false;
        Entity foundDfg = default;

        world.Query(
            in DfgQuery,
            (Entity entity, ref PlacedItemRef item, ref Transform2D transform) =>
            {
                if (found || item.Type != ItemType.Dfg || !item.Active || item.CityId == tankCityId)
                {
                    return;
                }

                var triggerBounds = MineSystem.GetMineTriggerBounds(item.GridX, item.GridY);
                if (triggerBounds.ContainsPoint(tankCenter))
                {
                    found = true;
                    foundDfg = entity;
                }
            });

        dfgEntity = foundDfg;
        return found;
    }

    private static void TriggerDfg(
        World world,
        Entity dfgEntity,
        Entity tankEntity,
        ref TankStatus status,
        SimulationAudioBuffer? audio)
    {
        ref var dfgTransform = ref world.Get<Transform2D>(dfgEntity);
        var center = new Vector2(
            dfgTransform.Position.X + GameConstants.TileSize / 2f,
            dfgTransform.Position.Y + GameConstants.TileSize / 2f);

        TankStatusSystem.ActivateFreeze(ref status);
        audio?.Play(SoundId.Buzz, center);
        world.Destroy(dfgEntity);
    }
}
