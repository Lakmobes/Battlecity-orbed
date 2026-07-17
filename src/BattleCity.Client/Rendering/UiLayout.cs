namespace BattleCity.Client.Rendering;

/// <summary>Centralized HUD layout in logical pixels.</summary>
public static class UiLayout
{
    public const int LogicalWidth = DisplaySettings.LogicalWidth;

    public const int LogicalHeight = DisplaySettings.LogicalHeight;

    /// <summary>Legacy right interface rail width (0 = full-screen world with overlay HUD).</summary>
    public const int PanelWidth = 0;

    public const int WorldViewportWidth = LogicalWidth - PanelWidth;

    public const int WorldViewportHeight = LogicalHeight;
}
