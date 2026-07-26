using System.Numerics;



using Arch.Core;



using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;



using Xunit;



namespace BattleCity.Core.Tests;



public class InputSystemTests

{

    [Fact]

    public void ComputeLegacyVelocity_IsZeroWhenNotMoving()

    {

        var velocity = InputSystem.ComputeLegacyVelocity(0, 0, GameConstants.MovementSpeedPlayer);

        Assert.Equal(Vector2.Zero, velocity);

    }



    [Fact]

    public void ComputeLegacyVelocity_MovesAlongFacingDirection()
    {
        var velocity = InputSystem.ComputeLegacyVelocity(
            InputSystem.ToTravelDirection(0),
            1,
            GameConstants.MovementSpeedPlayer);

        Assert.True(velocity.LengthSquared() > 0f);
        Assert.Equal(0f, velocity.X, precision: 3);
        Assert.True(velocity.Y < 0f);
    }



    [Fact]

    public void ComputeLegacyVelocity_MatchesLegacyPerFrameSpeedAtSixtyFps()

    {

        var velocity = InputSystem.ComputeLegacyVelocity(
            InputSystem.ToTravelDirection(0),
            1,
            GameConstants.MovementSpeedPlayer);

        var perFrame = MathF.Abs(velocity.Y * (1f / 60f));

        Assert.InRange(perFrame, 5.5f, 7f);

    }



    [Fact]
    public void ToTravelDirection_MovesForwardWithSpriteFacing()
    {
        var velocity = InputSystem.ComputeLegacyVelocity(
            InputSystem.ToTravelDirection(0),
            1,
            GameConstants.MovementSpeedPlayer);

        Assert.True(velocity.Y < 0f, "Sprite facing 0 should move up on screen when moving forward.");
    }

    [Fact]
    public void MuzzleOffset_UsesTravelDirection_MatchesBulletTravel()
    {
        const int spriteFacing = 0;
        var travelDirection = InputSystem.ToTravelDirection(spriteFacing);
        var muzzleOffset = WeaponGeometry.GetMuzzleOffset(travelDirection);

        var velocity = InputSystem.ComputeLegacyVelocity(travelDirection, 1, GameConstants.MovementSpeedPlayer);
        var bulletVelocity = BulletSystem.ComputeBulletVelocity(BulletKind.Laser, travelDirection, 1f / 60f);
        var moveDir = Vector2.Normalize(velocity);
        var shotDir = Vector2.Normalize(bulletVelocity);

        Assert.Equal(moveDir.X, shotDir.X, precision: 2);
        Assert.Equal(moveDir.Y, shotDir.Y, precision: 2);

        var tankTopLeft = new Vector2(100f, 100f);
        var muzzle = WeaponGeometry.GetMuzzleWorldPosition(tankTopLeft, travelDirection);
        Assert.Equal(tankTopLeft.X + muzzleOffset.X, muzzle.X, precision: 2);
        Assert.Equal(tankTopLeft.Y + muzzleOffset.Y, muzzle.Y, precision: 2);
        Assert.InRange(muzzle.Y, tankTopLeft.Y - 15f, tankTopLeft.Y + 15f);
    }

    [Fact]
    public void BarrelDirection_MatchesMovementAndBulletTravel()
    {
        const int spriteFacing = 8;
        var barrelDirection = InputSystem.ToTravelDirection(spriteFacing);
        var velocity = InputSystem.ComputeLegacyVelocity(barrelDirection, 1, GameConstants.MovementSpeedPlayer);
        var bulletVelocity = BulletSystem.ComputeBulletVelocity(BulletKind.Laser, barrelDirection, 1f / 60f);

        Assert.True(velocity.LengthSquared() > 0f);
        Assert.True(bulletVelocity.LengthSquared() > 0f);

        var moveDir = Vector2.Normalize(velocity);
        var shotDir = Vector2.Normalize(bulletVelocity);

        Assert.Equal(moveDir.X, shotDir.X, precision: 2);
        Assert.Equal(moveDir.Y, shotDir.Y, precision: 2);
    }

    [Fact]
    public void InputSystem_AppliesTurnAndMovement()

    {

        using var world = World.Create();

        var entity = world.Create(

            new InputControlled(),

            new InputCommand { Turn = 1, Move = 1 },

            new Transform2D { Position = Vector2.Zero },

            new Velocity(),

            new TankFacing { Direction = 0, TurnCooldownSeconds = 0f },

            new TankLifeState(),

            new TankStatus(),

            new SpriteRef

            {

                TextureKey = "Sprites/Tanks",

                SourceX = 0,

                SourceY = 0,

                Width = GameConstants.TileSize,

                Height = GameConstants.TileSize,

            });



        InputSystem.Update(world, TankFacing.TurnIntervalSeconds);

        MovementSystem.UpdateNonBullets(world, TankFacing.TurnIntervalSeconds);



        ref var facing = ref world.Get<TankFacing>(entity);

        ref var transform = ref world.Get<Transform2D>(entity);



        Assert.Equal(1, facing.Direction);

        Assert.True(transform.Position.LengthSquared() > 0f);

    }



    [Fact]

    public void InputSystem_DoesNotSnapFacingToMouseAim()

    {

        using var world = World.Create();

        var entity = world.Create(

            new InputControlled(),

            new InputCommand { AimDeltaX = 100f, AimDeltaY = 0f },

            new Transform2D(),

            new Velocity(),

            new TankFacing { Direction = 0, TurnCooldownSeconds = 0f },

            new TankLifeState(),

            new TankStatus(),

            new SpriteRef

            {

                TextureKey = "Sprites/Tanks",

                SourceX = 0,

                SourceY = 0,

                Width = GameConstants.TileSize,

                Height = GameConstants.TileSize,

            });



        InputSystem.Update(world, 0.016f);



        ref var facing = ref world.Get<TankFacing>(entity);

        Assert.Equal(0, facing.Direction);

    }

}


