using BattleCity.Client.Input;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using Xunit;

namespace BattleCity.Client.Tests;

public class GamepadHotplugTests
{
    [Fact]
    public void Apply_ConnectTransition_ReportsJustConnected()
    {
        var pointerMode = true;
        GamepadHotplug.Apply(
            wasConnected: false,
            isConnected: true,
            ref pointerMode,
            out var justConnected,
            out var justDisconnected);

        Assert.True(justConnected);
        Assert.False(justDisconnected);
        Assert.True(pointerMode);
    }

    [Fact]
    public void Apply_DisconnectTransition_ClearsPointerMode()
    {
        var pointerMode = true;
        GamepadHotplug.Apply(
            wasConnected: true,
            isConnected: false,
            ref pointerMode,
            out var justConnected,
            out var justDisconnected);

        Assert.False(justConnected);
        Assert.True(justDisconnected);
        Assert.False(pointerMode);
    }

    [Fact]
    public void Apply_StableConnection_NoEdges()
    {
        var pointerMode = true;
        GamepadHotplug.Apply(
            wasConnected: true,
            isConnected: true,
            ref pointerMode,
            out var justConnected,
            out var justDisconnected);

        Assert.False(justConnected);
        Assert.False(justDisconnected);
        Assert.True(pointerMode);
    }
}

public class GamepadPhase5MapperTests
{
    [Fact]
    public void Sample_TriggerBelowThreshold_DoesNotFire()
    {
        var previous = ConnectedPad();
        var current = ConnectedPad(rightTrigger: 0.4f, leftTrigger: 0.4f);

        var sampled = GamepadGameplayMapper.Sample(current, previous);

        Assert.False(sampled.FireHeld);
        Assert.False(sampled.FireFlareHeld);
    }

    [Fact]
    public void Sample_HeldTrigger_StaysHeldWithoutPressActions()
    {
        var held = ConnectedPad(rightTrigger: 0.9f, buttons: Buttons.A);
        var sampled = GamepadGameplayMapper.Sample(held, held);

        Assert.True(sampled.FireHeld);
        Assert.False(sampled.DropSelectedItemPressed);
    }

    [Fact]
    public void Merge_KeyboardAndGamepadFireBothContribute()
    {
        var keyboard = new GameplayInputState { FireHeld = true, Turn = -1 };
        var gamepad = new GameplayInputState { FireHeld = true, Move = 1 };

        var merged = GameplayInputMerger.Merge(keyboard, gamepad);

        Assert.True(merged.FireHeld);
        Assert.Equal(-1, merged.Turn);
        Assert.Equal(1, merged.Move);
    }

    [Fact]
    public void ReadDriveAxes_ExactlyAtDeadzone_Ignored()
    {
        var barelyInside = GameplayGamepadMap.StickDeadzone * 0.99f;
        var (turn, move) = GamepadGameplayMapper.ReadDriveAxes(
            new Vector2(barelyInside, barelyInside),
            new GamePadDPad(ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

        Assert.Equal(0, turn);
        Assert.Equal(0, move);
    }

    private static GamePadState ConnectedPad(
        float leftTrigger = 0f,
        float rightTrigger = 0f,
        Buttons buttons = 0) =>
        new(
            new GamePadThumbSticks(Vector2.Zero, Vector2.Zero),
            new GamePadTriggers(leftTrigger, rightTrigger),
            new GamePadButtons(buttons),
            new GamePadDPad(ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
}
