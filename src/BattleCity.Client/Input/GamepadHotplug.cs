namespace BattleCity.Client.Input;

/// <summary>Detects gamepad connect/disconnect and clears pointer mode on unplug.</summary>
public static class GamepadHotplug
{
    public static void Apply(
        bool wasConnected,
        bool isConnected,
        ref bool pointerMode,
        out bool justConnected,
        out bool justDisconnected)
    {
        justConnected = isConnected && !wasConnected;
        justDisconnected = !isConnected && wasConnected;
        if (justDisconnected)
        {
            pointerMode = false;
        }
    }
}
