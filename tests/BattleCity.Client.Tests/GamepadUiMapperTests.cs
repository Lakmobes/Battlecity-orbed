using BattleCity.Client.Input;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using Xunit;

namespace BattleCity.Client.Tests;

public class GamepadUiMapperTests
{
    [Fact]
    public void ApplyCameraPanFromStick_DeadzoneIgnored()
    {
        var left = false;
        var right = false;
        var up = false;
        var down = false;

        GamepadUiMapper.ApplyCameraPanFromStick(new Vector2(0.1f, -0.1f), ref left, ref right, ref up, ref down);

        Assert.False(left || right || up || down);
    }

    [Fact]
    public void ApplyCameraPanFromStick_MapsAxes()
    {
        var left = false;
        var right = false;
        var up = false;
        var down = false;

        GamepadUiMapper.ApplyCameraPanFromStick(new Vector2(0.8f, 0.8f), ref left, ref right, ref up, ref down);

        Assert.False(left);
        Assert.True(right);
        Assert.True(up);
        Assert.False(down);
    }

    [Fact]
    public void MovePointer_AppliesDeadzoneAndInvertsY()
    {
        var moved = GamepadUiMapper.MovePointer(
            new Vector2(100f, 100f),
            new Vector2(0f, 1f),
            deltaSeconds: 1f,
            width: 800,
            height: 600);

        Assert.Equal(100f, moved.X);
        Assert.True(moved.Y < 100f);
    }

    [Fact]
    public void MovePointer_ClampsToScreen()
    {
        var moved = GamepadUiMapper.MovePointer(
            new Vector2(10f, 10f),
            new Vector2(-1f, 1f),
            deltaSeconds: 10f,
            width: 800,
            height: 600);

        Assert.Equal(0f, moved.X);
        Assert.Equal(0f, moved.Y);
    }

    [Fact]
    public void WasPressed_RequiresConnectedPadAndEdge()
    {
        var previous = ConnectedPad();
        var current = ConnectedPad(buttons: Buttons.Start);

        Assert.True(GamepadUiMapper.WasPressed(current, previous, Buttons.Start));
        Assert.False(GamepadUiMapper.WasPressed(current, current, Buttons.Start));
        Assert.False(GamepadUiMapper.WasPressed(default, previous, Buttons.Start));
    }

    private static GamePadState ConnectedPad(Buttons buttons = 0) =>
        new(
            new GamePadThumbSticks(Vector2.Zero, Vector2.Zero),
            new GamePadTriggers(0f, 0f),
            new GamePadButtons(buttons),
            new GamePadDPad(ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
}
