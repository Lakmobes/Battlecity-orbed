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
    private const float HomeHoldDistancePixels = 64f;

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

                Vector2 steerTarget;
                float stopDistance;
                if (hasTarget)
                {
                    steerTarget = targetCenter;
                    stopDistance = StopDistancePixels;
                }
                else if (bot.Role == BotRoles.Attack && bot.HasGoal)
                {
                    steerTarget = new Vector2(bot.GoalX, bot.GoalY);
                    stopDistance = StopDistancePixels * 2f;
                }
                else
                {
                    // Defend / idle: return toward home pad.
                    steerTarget = new Vector2(bot.HomeX, bot.HomeY);
                    stopDistance = HomeHoldDistancePixels;
                }

                var desiredDirection = TurretTargeting.WorldPositionToLegacyDirection(botCenter, steerTarget);
                TryTurnToward(ref facing, desiredDirection, deltaSeconds);

                var distance = Vector2.Distance(botCenter, steerTarget);
                if (distance > stopDistance &&
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

    /// <param name="reportNetworkShot">
    /// When set, bots with <see cref="NetworkIdentity"/> also enqueue a network shot for clients.
    /// </param>
    public static void UpdateFiring(
        World world,
        float deltaSeconds,
        SimulationAudioBuffer? audio = null,
        Action<byte, ushort, ushort, byte>? reportNetworkShot = null)
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

                if (reportNetworkShot is not null
                    && world.Has<NetworkIdentity>(entity))
                {
                    var playerId = world.Get<NetworkIdentity>(entity).PlayerId;
                    reportNetworkShot(
                        playerId,
                        (ushort)Math.Clamp((int)muzzle.X, 0, ushort.MaxValue),
                        (ushort)Math.Clamp((int)muzzle.Y, 0, ushort.MaxValue),
                        (byte)facing.Direction);
                }
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
