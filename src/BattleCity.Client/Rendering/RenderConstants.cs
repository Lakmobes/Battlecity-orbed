namespace BattleCity.Client.Rendering;

public static class RenderConstants
{
    public static int UiPanelWidth => UiLayout.PanelWidth;

    public static int DefaultWindowWidth => UiLayout.LogicalWidth;

    public static int DefaultWindowHeight => UiLayout.LogicalHeight;

    public const float MinZoom = 0.5f;

    public const float MaxZoom = 4f;

    public const float ZoomStep = 0.1f;

    public const int MiniMapRadiusTiles = 48;

    public const int MiniMapTilePixelSize = 5;
}
