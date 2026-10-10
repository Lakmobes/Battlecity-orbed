using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Input;

public enum GameAction
{
    MoveForward,
    MoveBackward,
    TurnLeft,
    TurnRight,
    Fire,
    FireFlare,
    UseCloak,
    DropItem,
    UseMedKit,
    DropBomb,
    DropOrb,
    PickUp,
    InventoryPrevious,
    InventoryNext,
    ToggleMiniMap,
    CameraPan,
}

/// <summary>Player-chosen keyboard bindings. Saved under LocalAppData/BattleCity/controls.cfg.</summary>
public sealed class KeyBindings
{
    public const int ActionCount = 16;

    public static KeyBindings Current { get; } = Load();

    private readonly Keys[] _primary = new Keys[ActionCount];
    private readonly Keys[] _alt = new Keys[ActionCount];

    private KeyBindings()
    {
        ResetToDefaults(save: false);
    }

    public static IReadOnlyList<GameAction> Actions { get; } =
    [
        GameAction.MoveForward,
        GameAction.MoveBackward,
        GameAction.TurnLeft,
        GameAction.TurnRight,
        GameAction.Fire,
        GameAction.FireFlare,
        GameAction.UseCloak,
        GameAction.DropItem,
        GameAction.UseMedKit,
        GameAction.DropBomb,
        GameAction.DropOrb,
        GameAction.PickUp,
        GameAction.InventoryPrevious,
        GameAction.InventoryNext,
        GameAction.ToggleMiniMap,
        GameAction.CameraPan,
    ];

    public Keys Primary(GameAction action) => _primary[(int)action];

    public Keys Alt(GameAction action) => _alt[(int)action];

    public void Assign(GameAction action, Keys key)
    {
        if (key is Keys.None or Keys.Escape or Keys.Enter)
        {
            return;
        }

        Release(key);
        _primary[(int)action] = key;
        _alt[(int)action] = Keys.None;
        Save();
    }

    public void ResetToDefaults() => ResetToDefaults(save: true);

    public string Title(GameAction action) => action switch
    {
        GameAction.MoveForward => "Move forward",
        GameAction.MoveBackward => "Move back",
        GameAction.TurnLeft => "Turn left",
        GameAction.TurnRight => "Turn right",
        GameAction.Fire => "Fire",
        GameAction.FireFlare => "Flare",
        GameAction.UseCloak => "Cloak",
        GameAction.DropItem => "Drop item",
        GameAction.UseMedKit => "Medkit",
        GameAction.DropBomb => "Bomb",
        GameAction.DropOrb => "Orb",
        GameAction.PickUp => "Pick up",
        GameAction.InventoryPrevious => "Inventory prev",
        GameAction.InventoryNext => "Inventory next",
        GameAction.ToggleMiniMap => "Minimap",
        GameAction.CameraPan => "Camera look",
        _ => action.ToString(),
    };

    public string FormatKeys(GameAction action)
    {
        var primary = FormatKey(_primary[(int)action]);
        var alt = _alt[(int)action];
        return alt == Keys.None ? primary : $"{primary}, {FormatKey(alt)}";
    }

    public static string FormatKey(Keys key) => key switch
    {
        Keys.None => "-",
        Keys.OemOpenBrackets => "[",
        Keys.OemCloseBrackets => "]",
        Keys.OemComma => ",",
        Keys.OemPeriod => ".",
        Keys.OemMinus => "-",
        Keys.OemPlus => "+",
        Keys.LeftShift => "Left Shift",
        Keys.RightShift => "Right Shift",
        Keys.LeftControl => "Left Ctrl",
        Keys.RightControl => "Right Ctrl",
        Keys.LeftAlt => "Left Alt",
        Keys.RightAlt => "Right Alt",
        Keys.Space => "Space",
        _ => key.ToString(),
    };

    private void Release(Keys key)
    {
        for (var i = 0; i < ActionCount; i++)
        {
            if (_primary[i] == key)
            {
                _primary[i] = _alt[i];
                _alt[i] = Keys.None;
            }
            else if (_alt[i] == key)
            {
                _alt[i] = Keys.None;
            }
        }
    }

    private void ResetToDefaults(bool save)
    {
        Set(GameAction.MoveForward, Keys.Up, Keys.W);
        Set(GameAction.MoveBackward, Keys.Down, Keys.S);
        Set(GameAction.TurnLeft, Keys.Left, Keys.A);
        Set(GameAction.TurnRight, Keys.Right, Keys.E);
        Set(GameAction.Fire, Keys.LeftShift, Keys.RightShift);
        Set(GameAction.FireFlare, Keys.LeftControl, Keys.RightControl);
        Set(GameAction.UseCloak, Keys.C, Keys.None);
        Set(GameAction.DropItem, Keys.D, Keys.None);
        Set(GameAction.UseMedKit, Keys.H, Keys.None);
        Set(GameAction.DropBomb, Keys.B, Keys.None);
        Set(GameAction.DropOrb, Keys.O, Keys.None);
        Set(GameAction.PickUp, Keys.U, Keys.None);
        Set(GameAction.InventoryPrevious, Keys.OemOpenBrackets, Keys.None);
        Set(GameAction.InventoryNext, Keys.OemCloseBrackets, Keys.None);
        Set(GameAction.ToggleMiniMap, Keys.M, Keys.None);
        Set(GameAction.CameraPan, Keys.Tab, Keys.None);
        if (save)
        {
            Save();
        }
    }

    private void Set(GameAction action, Keys primary, Keys alt)
    {
        _primary[(int)action] = primary;
        _alt[(int)action] = alt;
    }

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BattleCity",
            "controls.cfg");

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var lines = new string[ActionCount * 2];
            for (var i = 0; i < ActionCount; i++)
            {
                var action = (GameAction)i;
                lines[i * 2] = $"{action}={_primary[i]}";
                lines[(i * 2) + 1] = $"{action}Alt={_alt[i]}";
            }

            File.WriteAllLines(FilePath, lines);
        }
        catch (IOException)
        {
            // Bindings still apply for this session if the file cannot be written.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static KeyBindings Load()
    {
        var bindings = new KeyBindings();
        if (!File.Exists(FilePath))
        {
            return bindings;
        }

        try
        {
            foreach (var line in File.ReadAllLines(FilePath))
            {
                var split = line.Split('=', 2);
                if (split.Length != 2 || !Enum.TryParse<Keys>(split[1], out var key))
                {
                    continue;
                }

                var name = split[0];
                var alt = name.EndsWith("Alt", StringComparison.Ordinal);
                if (alt)
                {
                    name = name[..^3];
                }

                if (!Enum.TryParse<GameAction>(name, out var action))
                {
                    continue;
                }

                if (alt)
                {
                    bindings._alt[(int)action] = key;
                }
                else
                {
                    bindings._primary[(int)action] = key;
                }
            }
        }
        catch (IOException)
        {
            return new KeyBindings();
        }

        return bindings;
    }
}
