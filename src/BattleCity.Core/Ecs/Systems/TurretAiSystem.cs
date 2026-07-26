using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ai;
using BattleCity.Core.Audio;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Systems;

public static class TurretAiSystem
{
    private static readonly QueryDescription Query =
        new QueryDescription().WithAll<PlacedItemRef, TurretState, Transform2D>();

    public static void Update(World world, float deltaSeconds, SimulationAudioBuffer? audio = null)
    {
        world.Query(
            in Query,
            (Entity entity, ref PlacedItemRef item, ref TurretState turret, ref Transform2D transform) =>
            {
                if (!TurretStats.IsTurretType(item.Type) || !item.Active)
                {
                    return;
                }

                turret.StartupDelaySeconds = Math.Max(0f, turret.StartupDelaySeconds - deltaSeconds);
                turret.TurnCooldownSeconds = Math.Max(0f, turret.TurnCooldownSeconds - deltaSeconds);
                turret.AnimationCooldownSeconds = Math.Max(0f, turret.AnimationCooldownSeconds - deltaSeconds);

                ClearDestroyedBullet(world, ref turret);

                var turretCenter = TurretTargeting.GetTurretWorldCenter(item.GridX, item.GridY);
                turret.HasTarget = TurretTargeting.TryFindNearestEnemy(
                    world,
                    item.CityId,
                    turretCenter,
                    GameConstants.TurretTargetRangePixels,
                    out var target,
                    out var targetCenter);

                turret.Target = target;

                if (turret.HasTarget)
                {
                    turret.AimAngleDegrees = TurretTargeting.ComputeAimAngleDegrees(
                        item.GridX,
                        item.GridY,
                        targetCenter);
                }
                else if (item.Type == ItemType.Sleeper)
                {
                    turret.AnimationFrame = 0;
                }

                if (world.Has<Health>(entity))
                {
                    ref readonly var health = ref world.Get<Health>(entity);
                    UpdateBurningAnimation(ref turret, in health, item.Type, deltaSeconds);
                }

                if (turret.StartupDelaySeconds > 0f || turret.TurnCooldownSeconds > 0f)
                {
                    return;
                }

                // Legacy resets lastturn every interval whether or not a shot is fired
                // (CItem.cpp). Only starting the timer on Fire let turrets re-shoot as soon
                // as the previous bullet died (~bullet lifetime) instead of on 250ms ticks.
                turret.TurnCooldownSeconds = GameConstants.TimerTurretTurn / 1000f;

                if (!turret.HasTarget)
                {
                    return;
                }

                if (turret.ActiveBullet != default && world.IsAlive(turret.ActiveBullet))
                {
                    return;
                }

                Fire(world, entity, ref item, ref turret, audio);
            });
    }

    private static void Fire(
        World world,
        Entity turretEntity,
        ref PlacedItemRef item,
        ref TurretState turret,
        SimulationAudioBuffer? audio)
    {
        var direction = TurretTargeting.AngleDegreesToLegacyDirection(turret.AimAngleDegrees);
        var muzzle = TurretTargeting.GetTurretMuzzlePosition(item.GridX, item.GridY, direction);
        var bulletKind = TurretStats.GetBulletKind(item.Type);
        var bullet = GameplayEntityFactory.CreateBullet(world, bulletKind, muzzle, direction, turretEntity);

        ref var damage = ref world.Get<Damage>(bullet);
        damage.Value = TurretStats.GetBulletDamage(item.Type);

        turret.ActiveBullet = bullet;
        audio?.Play(
            item.Type == ItemType.Plasma ? SoundId.BigTurret : SoundId.TurretFire,
            muzzle);
    }

    private static void ClearDestroyedBullet(World world, ref TurretState turret)
    {
        if (turret.ActiveBullet == default)
        {
            return;
        }

        if (!world.IsAlive(turret.ActiveBullet))
        {
            turret.ActiveBullet = default;
        }
    }

    private static void UpdateBurningAnimation(
        ref TurretState turret,
        in Health health,
        ItemType type,
        float deltaSeconds)
    {
        if (!TurretStats.IsBurning(type, health.Current))
        {
            turret.AnimationFrame = 0;
            return;
        }

        if (turret.AnimationCooldownSeconds > 0f)
        {
            return;
        }

        turret.AnimationFrame = turret.AnimationFrame == 2 ? 1 : 2;
        turret.AnimationCooldownSeconds = GameConstants.TurretAnimationIntervalMs / 1000f;
    }
}
