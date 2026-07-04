using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.Ecs.Systems;

public static class InputSystem
{
    private static readonly QueryDescription Query = new QueryDescription()
        .WithAll<InputControlled, InputCommand, Transform2D, Velocity, TankFacing, TankLifeState, TankStatus, SpriteRef>();

    public static void Update(World world, float deltaSeconds)
    {
        world.Query(
            in Query,
            (Entity entity, ref InputCommand input, ref Transform2D transform, ref Velocity velocity, ref TankFacing facing, ref TankLifeState life, ref TankStatus status, ref SpriteRef sprite) =>
            {
                if (life.IsDead)
                {
                    velocity.Value = Vector2.Zero;
                    return;
                }

                if (status.IsFrozen)
                {
                    velocity.Value = Vector2.Zero;
                    return;
                }

                ApplyTurn(ref input, ref facing, deltaSeconds);
                ApplyMovement(ref input, ref facing, ref velocity);
                ApplySpriteFrame(ref facing, ref sprite);
                transform.RotationDegrees = facing.Direction * (360f / TankFacing.DirectionCount);
            });
    }

    /// <summary>Legacy pixels per second (legacy/client/CPlayer.cpp uses TimePassed * MoveFactor).</summary>
    public static Vector2 ComputeLegacyVelocity(int direction, int move, float moveFactor)
    {
        if (move == 0)
        {
            return Vector2.Zero;
        }

        var facingRadians = LegacyDirectionToRadians(direction);
        var speed = moveFactor * 1000f;
        var velocity = new Vector2(
            (float)(Math.Sin(facingRadians) * move * speed),
            (float)(Math.Cos(facingRadians) * move * speed));

        const float maxSpeed = 20f * 60f;
        velocity.X = Math.Clamp(velocity.X, -maxSpeed, maxSpeed);
        velocity.Y = Math.Clamp(velocity.Y, -maxSpeed, maxSpeed);
        return velocity;
    }

    public static float LegacyDirectionToRadians(int direction)
    {
        var legacyFacing = -direction + 32;
        return legacyFacing / 16f * MathF.PI;
    }

    /// <summary>
    /// Tank sprites use a half-turn offset from the legacy velocity index
    /// (legacy/client/CPlayer.cpp collision slide uses fDir+16).
    /// </summary>
    public static int ToTravelDirection(int spriteFacing) =>
        (spriteFacing + TankFacing.DirectionCount / 2) % TankFacing.DirectionCount;

    public static int ToSpriteFacing(int travelDirection) =>
        (travelDirection + TankFacing.DirectionCount / 2) % TankFacing.DirectionCount;

    private static void ApplyTurn(ref InputCommand input, ref TankFacing facing, float deltaSeconds)
    {
        if (input.Turn == 0)
        {
            facing.TurnCooldownSeconds = Math.Max(0f, facing.TurnCooldownSeconds - deltaSeconds);
            return;
        }

        facing.TurnCooldownSeconds -= deltaSeconds;
        if (facing.TurnCooldownSeconds > 0f)
        {
            return;
        }

        facing.Direction += input.Turn;
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

    private static void ApplyMovement(ref InputCommand input, ref TankFacing facing, ref Velocity velocity)
    {
        velocity.Value = ComputeLegacyVelocity(
            ToTravelDirection(facing.Direction),
            input.Move,
            GameConstants.MovementSpeedPlayer);
    }

    private static void ApplySpriteFrame(ref TankFacing facing, ref SpriteRef sprite)
    {
        sprite.SourceX = facing.Direction / 2 * GameConstants.TileSize;
    }
}
