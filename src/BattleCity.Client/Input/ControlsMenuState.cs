using BattleCity.Client.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

public sealed class ControlsMenuState
{
    private KeyboardState _previous;

    public bool IsOpen { get; private set; }

    public int Selected { get; private set; }

    public bool WaitingForKey { get; private set; }

    public void Open()
    {
        IsOpen = true;
        Selected = 0;
        WaitingForKey = false;
        _previous = Keyboard.GetState();
    }

    public void Close()
    {
        IsOpen = false;
        WaitingForKey = false;
    }

    public void Handle(UiInputState ui)
    {
        var keyboard = Keyboard.GetState();
        if (WaitingForKey)
        {
            if (WasPressed(keyboard, Keys.Escape))
            {
                WaitingForKey = false;
            }
            else
            {
                for (var code = 1; code < 255; code++)
                {
                    var key = (Keys)code;
                    if (!WasPressed(keyboard, key) || key is Keys.Escape or Keys.Enter)
                    {
                        continue;
                    }

                    KeyBindings.Current.Assign(KeyBindings.Actions[Selected], key);
                    WaitingForKey = false;
                    break;
                }
            }

            _previous = keyboard;
            return;
        }

        var mouse = new Point((int)ui.MouseLogicalPosition.X, (int)ui.MouseLogicalPosition.Y);
        if (ControlsMenuLayout.TryHit(mouse.X, mouse.Y, out var hover))
        {
            Selected = hover;
        }

        if (WasPressed(keyboard, Keys.Up) || WasPressed(keyboard, Keys.W))
        {
            Selected = (Selected - 1 + ControlsMenuLayout.ItemCount) % ControlsMenuLayout.ItemCount;
        }

        if (WasPressed(keyboard, Keys.Down) || WasPressed(keyboard, Keys.S))
        {
            Selected = (Selected + 1) % ControlsMenuLayout.ItemCount;
        }

        var confirm = WasPressed(keyboard, Keys.Enter) || (ui.MouseLeftClicked && ControlsMenuLayout.TryHit(mouse.X, mouse.Y, out _));
        if (confirm)
        {
            if (Selected == ControlsMenuLayout.ResetIndex)
            {
                KeyBindings.Current.ResetToDefaults();
            }
            else if (Selected == ControlsMenuLayout.BackIndex)
            {
                Close();
            }
            else
            {
                WaitingForKey = true;
            }
        }

        if (WasPressed(keyboard, Keys.Escape))
        {
            Close();
        }

        _previous = keyboard;
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previous.IsKeyDown(key);
}
