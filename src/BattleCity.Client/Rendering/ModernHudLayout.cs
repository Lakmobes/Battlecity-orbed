using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

/// <summary>Overlay HUD layout for the modern full-screen in-game UI.</summary>
public static class ModernHudLayout
{
    public const int TopBarHeight = 64;
    public const int TopBarPadding = 14;
    public const int HealthBarWidth = 160;
    public const int HealthBarHeight = 26;
    public const int InventorySlotSize = 44;
    public const int InventorySlotSpacing = 10;
    public const int InventoryStartX = TopBarPadding + HealthBarWidth + 20;

    public const int CompassSize = 96;
    public const int CompassMargin = 20;

    public const int StatusPanelPadding = 12;
    public const int StatusLineHeight = 20;

    public const int ChatAreaHeight = 140;

    public static Rectangle TopBar =>
        new(0, 0, UiLayout.LogicalWidth, TopBarHeight);

    public static Rectangle CompassBounds =>
        new(
            UiLayout.LogicalWidth - CompassSize - CompassMargin,
            TopBarHeight + CompassMargin,
            CompassSize,
            CompassSize);

    public static int InventorySlotY =>
        TopBarPadding + (TopBarHeight - TopBarPadding * 2 - InventorySlotSize) / 2;

    public static int GetInventorySlotX(int slotIndex) =>
        InventoryStartX + slotIndex * (InventorySlotSize + InventorySlotSpacing);

    public static Rectangle StatusPanel(int lineCount)
    {
        var height = lineCount * StatusLineHeight + StatusPanelPadding * 2;
        var y = UiLayout.LogicalHeight - ChatAreaHeight - height - 12;
        return new Rectangle(12, y, 340, height);
    }

    public static int MiniMapMargin => TopBarHeight + 12;

    public static int ScreenCenterX => UiLayout.WorldViewportWidth / 2;

    public static int ScreenCenterY => UiLayout.WorldViewportHeight / 2;
}
