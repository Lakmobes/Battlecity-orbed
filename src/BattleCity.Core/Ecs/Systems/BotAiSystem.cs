using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ai;
using BattleCity.Core.Audio;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;

using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Systems;

public static class BotAiSystem
{
    private const int FireAlignmentTolerance = 2;
    private const float StopDistancePixels = 96f;

    private static readonly QueryDescription BotQuery =
        new QueryDescription().WithAll<BotController, Transform2D, TankFacing, Velocity, CityAffiliation, Health, TankLifeState, TankStatus, SpriteRef>();

    public static void UpdateMovement(World world, float deltaSeconds)
    {
        world.Query(
            in BotQuery,
            (ref BotController bot, ref Transform2D transform, ref TankFacing facing, ref Velocity velocity, ref CityAffiliation city, ref Health health, ref TankLifeState life, ref TankStatus status, ref SpriteRef sprite) =>
            {
                if (life.IsDead || health.Current <= 0 || status.IsFrozen)
                {
                    velocity.Value = Vector2.Zero;
                    return;
                }

                facing.TurnCooldownSeconds = Math.Max(0f, facing.TurnCooldownSeconds - deltaSeconds);

                var botCenter = TurretTargeting.GetTankCenter(transform.Position);
                var hasTarget = TurretTargeting.TryFindNearestEnemy(
                    world,
                    city.CityId,
                    botCenter,
                    bot.AggroRangePixels,
                    out _,
                    out var targetCenter);

                if (!hasTarget)
                {
                    velocity.Value = Vector2.Zero;
                    return;
                }

                var desiredDirection = TurretTargeting.WorldPositionToLegacyDirection(botCenter, targetCenter);
                TryTurnToward(ref facing, desiredDirection, deltaSeconds);

                var distance = Vector2.Distance(botCenter, targetCenter);
                if (distance > StopDistancePixels &&
                    TurretTargeting.DirectionDifference(facing.Direction, desiredDirection) <= 4)
                {
                    velocity.Value = InputSystem.ComputeLegacyVelocity(
                        InputSystem.ToTravelDirection(facing.Direction),
                        1,
                        GameConstants.MovementSpeedPlayer);
                }
                else
                {
                    velocity.Value = Vector2.Zero;
                }

                sprite.SourceX = facing.Direction / 2 * GameConstants.TileSize;
            });
    }

    public static void UpdateFiring(World world, float deltaSeconds, SimulationAudioBuffer? audio = null)
    {
        world.Query(
            in BotQuery,
            (Entity entity, ref BotController bot, ref Transform2D transform, ref TankFacing facing, ref CityAffiliation city, ref Health health, ref TankLifeState life, ref TankStatus status) =>
            {
                if (life.IsDead || health.Current <= 0 || status.IsFrozen)
                {
                    return;
                }

                bot.FireCooldownSeconds = Math.Max(0f, bot.FireCooldownSeconds - deltaSeconds);

                var botCenter = TurretTargeting.GetTankCenter(transform.Position);
                if (!TurretTargeting.TryFindNearestEnemy(
                        world,
                        city.CityId,
                        botCenter,
                        bot.AggroRangePixels,
                        out _,
                        out var targetCenter))
                {
                    return;
                }

                var desiredDirection = TurretTargeting.WorldPositionToLegacyDirection(botCenter, targetCenter);
                if (bot.FireCooldownSeconds > 0f ||
                    TurretTargeting.DirectionDifference(facing.Direction, desiredDirection) > FireAlignmentTolerance)
                {
                    return;
                }

                var travelDirection = InputSystem.ToTravelDirection(facing.Direction);
                var muzzle = WeaponGeometry.GetMuzzleWorldPosition(
                    transform.Position,
                    travelDirection);
                GameplayEntityFactory.CreateBullet(
                    world,
                    BulletKind.Laser,
                    muzzle,
                    travelDirection,
                    entity);
                GameplayEntityFactory.CreateExplosion(world, ExplosionKind.MuzzleFlash, muzzle);
                audio?.Play(SoundId.Laser, muzzle);
                bot.FireCooldownSeconds = GameConstants.TimerShootLaser / 1000f;
            });
    }

    private static void TryTurnToward(ref TankFacing facing, int desiredDirection, float deltaSeconds)
    {
        if (facing.Direction == desiredDirection)
        {
            return;
        }

        if (facing.TurnCooldownSeconds > 0f)
        {
            return;
        }

        var forward = (desiredDirection - facing.Direction + TankFacing.DirectionCount) % TankFacing.DirectionCount;
        var backward = (facing.Direction - desiredDirection + TankFacing.DirectionCount) % TankFacing.DirectionCount;
        facing.Direction += forward <= backward ? 1 : -1;

        if (facing.Direction < 0)
        {
            facing.Direction = TankFacing.DirectionCount - 1;
        }
        else if (facing.Direction >= TankFacing.DirectionCount)
        {
            facing.Direction = 0;
        }

        facing.TurnCooldownSeconds = TankFacing.TurnIntervalSeconds;
    }
}
