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

public static class MineSystem
{
    private static readonly QueryDescription TankQuery =
        new QueryDescription().WithAll<Transform2D, TankLifeState, Health, CityAffiliation>();

    private static readonly QueryDescription MineQuery =
        new QueryDescription().WithAll<PlacedItemRef, Transform2D>();

    public static void Update(World world, SimulationAudioBuffer? audio = null)
    {
        world.Query(
            in TankQuery,
            (Entity tankEntity, ref Transform2D transform, ref TankLifeState life, ref Health health, ref CityAffiliation city) =>
            {
                if (life.IsDead || health.Current <= 0)
                {
                    return;
                }

                var tankCenter = TurretTargeting.GetTankCenter(transform.Position);
                if (!TryFindTriggeredMine(world, tankCenter, city.CityId, out var mineEntity))
                {
                    return;
                }

                DetonateMine(world, mineEntity, tankEntity, ref health, ref life, audio);
            });
    }

    private static bool TryFindTriggeredMine(
        World world,
        Vector2 tankCenter,
        int tankCityId,
        out Entity mineEntity)
    {
        mineEntity = default;
        var found = false;
        Entity foundMine = default;

        world.Query(
            in MineQuery,
            (Entity entity, ref PlacedItemRef item, ref Transform2D transform) =>
            {
                if (found || item.Type != ItemType.Mine || !item.Active || item.CityId == tankCityId)
                {
                    return;
                }

                var mineBounds = GetMineTriggerBounds(item.GridX, item.GridY);
                if (mineBounds.ContainsPoint(tankCenter))
                {
                    found = true;
                    foundMine = entity;
                }
            });

        mineEntity = foundMine;
        return found;
    }

    private static void DetonateMine(
        World world,
        Entity mineEntity,
        Entity tankEntity,
        ref Health health,
        ref TankLifeState life,
        SimulationAudioBuffer? audio)
    {
        ref var mine = ref world.Get<PlacedItemRef>(mineEntity);
        ref var mineTransform = ref world.Get<Transform2D>(mineEntity);
        var explosionCenter = new Vector2(
            mineTransform.Position.X + GameConstants.TileSize / 2f,
            mineTransform.Position.Y + GameConstants.TileSize / 2f);

        mine.Active = false;
        life.KillerCityId = (byte)mine.CityId;
        health.Current = Math.Max(0, health.Current - GameConstants.DamageMine);
        GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Small, explosionCenter);
        audio?.Play(SoundId.Explode, explosionCenter);

        if (health.Current <= 0)
        {
            life.IsDead = true;
            life.RespawnTimerSeconds = GameConstants.TimerRespawn / 1000f;
            audio?.Play(SoundId.Die, explosionCenter);
        }

        world.Destroy(mineEntity);
    }

    public static AxisAlignedBox GetMineTriggerBounds(int gridX, int gridY)
    {
        var inset = GameConstants.PlayerCollisionInset;
        var origin = PlacedItemPlacement.GridToWorldPosition(gridX, gridY);
        return new AxisAlignedBox(
            origin.X + inset,
            origin.Y + inset,
            GameConstants.TileSize - inset * 2,
            GameConstants.TileSize - inset * 2);
    }
}
