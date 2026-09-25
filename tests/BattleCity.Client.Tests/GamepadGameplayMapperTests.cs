using BattleCity.Client.Input;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using Xunit;

namespace BattleCity.Client.Tests;

public class GamepadGameplayMapperTests
{
    [Fact]
    public void Sample_DisconnectedPad_ReturnsDefault()
    {
        var state = default(GamePadState);
        Assert.False(state.IsConnected);

        var sampled = GamepadGameplayMapper.Sample(state, state);

        Assert.Equal(0, sampled.Turn);
        Assert.Equal(0, sampled.Move);
        Assert.False(sampled.FireHeld);
    }

    [Theory]
    [InlineData(0f, 0f, 0, 0)]
    [InlineData(0.1f, 0.1f, 0, 0)]
    [InlineData(0.5f, 0f, 1, 0)]
    [InlineData(-0.5f, 0f, -1, 0)]
    [InlineData(0f, 0.5f, 0, 1)]
    [InlineData(0f, -0.5f, 0, -1)]
    [InlineData(0.8f, 0.8f, 1, 1)]
    public void ReadDriveAxes_AppliesDeadzoneAndStickAxes(
        float stickX,
        float stickY,
        int expectedTurn,
        int expectedMove)
    {
        var (turn, move) = GamepadGameplayMapper.ReadDriveAxes(
            new Vector2(stickX, stickY),
            new GamePadDPad(ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

        Assert.Equal(expectedTurn, turn);
        Assert.Equal(expectedMove, move);
    }

    [Fact]
    public void ReadDriveAxes_DpadMapsLikeArrows()
    {
        var (turn, move) = GamepadGameplayMapper.ReadDriveAxes(
            Vector2.Zero,
            new GamePadDPad(ButtonState.Pressed, ButtonState.Released, ButtonState.Pressed, ButtonState.Released));

        Assert.Equal(-1, turn);
        Assert.Equal(1, move);
    }

    [Fact]
    public void Sample_RightTriggerFires_LeftTriggerFlares()
    {
        var previous = ConnectedPad();
        var current = ConnectedPad(rightTrigger: 0.9f, leftTrigger: 0.9f);

        var sampled = GamepadGameplayMapper.Sample(current, previous);

        Assert.True(sampled.FireHeld);
        Assert.True(sampled.FireFlareHeld);
    }

    [Fact]
    public void Sample_FaceButtons_MapInventoryActionsOnPressEdge()
    {
        var previous = ConnectedPad();
        var current = ConnectedPad(buttons: Buttons.A | Buttons.B | Buttons.X | Buttons.Y);

        var sampled = GamepadGameplayMapper.Sample(current, previous);

        Assert.True(sampled.DropSelectedItemPressed);
        Assert.True(sampled.UseMedKitPressed);
        Assert.True(sampled.PickUpItemPressed);
        Assert.True(sampled.UseCloakPressed);
        Assert.False(sampled.DropBombPressed);
        Assert.False(sampled.DropOrbPressed);
    }

    [Fact]
    public void Sample_HeldFaceButtons_DoNotRetrigger()
    {
        var held = ConnectedPad(buttons: Buttons.A | Buttons.Y);
        var sampled = GamepadGameplayMapper.Sample(held, held);

        Assert.False(sampled.DropSelectedItemPressed);
        Assert.False(sampled.UseCloakPressed);
    }

    [Fact]
    public void Sample_Shoulders_CycleInventory()
    {
        var previous = ConnectedPad();
        var current = ConnectedPad(buttons: Buttons.LeftShoulder | Buttons.RightShoulder);

        var sampled = GamepadGameplayMapper.Sample(current, previous);

        Assert.True(sampled.CycleInventoryPreviousPressed);
        Assert.True(sampled.CycleInventoryNextPressed);
    }

    [Fact]
    public void Sample_ViewModifier_RoutesAAndXToBombAndOrb()
    {
        var previous = ConnectedPad(buttons: Buttons.Back);
        var current = ConnectedPad(buttons: Buttons.Back | Buttons.A | Buttons.X);

        var sampled = GamepadGameplayMapper.Sample(current, previous);

        Assert.True(sampled.DropBombPressed);
        Assert.True(sampled.DropOrbPressed);
        Assert.False(sampled.DropSelectedItemPressed);
        Assert.False(sampled.PickUpItemPressed);
    }

    private static GamePadState ConnectedPad(
        float leftTrigger = 0f,
        float rightTrigger = 0f,
        Buttons buttons = 0)
    {
        return new GamePadState(
            new GamePadThumbSticks(Vector2.Zero, Vector2.Zero),
            new GamePadTriggers(leftTrigger, rightTrigger),
            new GamePadButtons(buttons),
            new GamePadDPad(ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
    }
}

public class GameplayInputMergerTests
{
    [Fact]
    public void Merge_ORsHeldAndPressedFlags()
    {
        var keyboard = new GameplayInputState
        {
            FireHeld = true,
            UseCloakPressed = true,
            AimDeltaX = 12f,
            AimDeltaY = -3f,
        };
        var gamepad = new GameplayInputState
        {
            FireFlareHeld = true,
            DropBombPressed = true,
        };

        var merged = GameplayInputMerger.Merge(keyboard, gamepad);

        Assert.True(merged.FireHeld);
        Assert.True(merged.FireFlareHeld);
        Assert.True(merged.UseCloakPressed);
        Assert.True(merged.DropBombPressed);
        Assert.Equal(12f, merged.AimDeltaX);
        Assert.Equal(-3f, merged.AimDeltaY);
    }

    [Fact]
    public void Merge_ClampsCombinedTurnAndMove()
    {
        var keyboard = new GameplayInputState { Turn = 1, Move = 1 };
        var gamepad = new GameplayInputState { Turn = 1, Move = -1 };

        var merged = GameplayInputMerger.Merge(keyboard, gamepad);

        Assert.Equal(1, merged.Turn);
        Assert.Equal(0, merged.Move);
    }

    [Fact]
    public void Merge_OpposingAxesCancel()
    {
        var keyboard = new GameplayInputState { Turn = -1 };
        var gamepad = new GameplayInputState { Turn = 1 };

        var merged = GameplayInputMerger.Merge(keyboard, gamepad);

        Assert.Equal(0, merged.Turn);
    }
}
