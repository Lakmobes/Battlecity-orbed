namespace BattleCity.Client.Rendering;

/// <summary>Centralized HUD layout in logical pixels (legacy 800×600 design space).</summary>
public static class UiLayout
{
    public const int LogicalWidth = DisplaySettings.LogicalWidth;

    public const int LogicalHeight = DisplaySettings.LogicalHeight;

    /// <summary>Legacy right interface rail width.</summary>
    public const int PanelWidth = 200;

    public const int WorldViewportWidth = LogicalWidth - PanelWidth;

    public const int WorldViewportHeight = LogicalHeight;
}
