using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

/// <summary>Overlay HUD layout for the modern full-screen in-game UI.</summary>
public static class ModernHudLayout
{
    public const int TopBarPadding = 14;
    public const int InventorySlotSize = 64;
    public const int InventorySlotSpacing = 10;
    public const int HealthBarWidth = 520;
    public const int HealthBarHeight = 20;
    public const int HealthBarGap = 10;

    public const int TopBarHeight =
        TopBarPadding + InventorySlotSize + HealthBarGap + HealthBarHeight + TopBarPadding;

    public const int CompassSize = 112;
    public const int CompassInnerSize = 64;
    public const int CompassMargin = 20;

    /// <summary>Inset past the nine-slice border so text sits inside the frame.</summary>
    public const int StatusPanelPadding = HudSpriteNames.PanelBorder + 8;

    public const int StatusLineHeight = 20;
    public const int StatusPanelWidth = 400;

    public const int ChatAreaHeight = 148;

    public const int HamburgerSize = 44;
    public const int HamburgerMargin = 16;

    public static Rectangle TopBar =>
        new(0, 0, UiLayout.LogicalWidth, TopBarHeight);

    public static Rectangle CompassBounds =>
        new(
            UiLayout.LogicalWidth - CompassSize - CompassMargin,
            TopBarHeight + CompassMargin,
            CompassSize,
            CompassSize);

    public static Rectangle CompassInnerBounds
    {
        get
        {
            var outer = CompassBounds;
            var inset = (CompassSize - CompassInnerSize) / 2;
            return new Rectangle(outer.X + inset, outer.Y + inset, CompassInnerSize, CompassInnerSize);
        }
    }

    public static Rectangle HamburgerBounds =>
        new(HamburgerMargin, (TopBarHeight - HamburgerSize) / 2, HamburgerSize, HamburgerSize);

    public static int InventorySlotY => TopBarPadding;

    public static int HealthBarY =>
        TopBarPadding + InventorySlotSize + HealthBarGap;

    public static int GetCenteredRowStartX(int slotCount)
    {
        if (slotCount <= 0)
        {
            return (UiLayout.LogicalWidth - HealthBarWidth) / 2;
        }

        var rowWidth = slotCount * InventorySlotSize + (slotCount - 1) * InventorySlotSpacing;
        return (UiLayout.LogicalWidth - rowWidth) / 2;
    }

    public static int GetInventorySlotX(int slotIndex, int slotCount) =>
        GetCenteredRowStartX(slotCount) + slotIndex * (InventorySlotSize + InventorySlotSpacing);

    public static Rectangle StatusPanel(int lineCount)
    {
        var height = lineCount * StatusLineHeight + StatusPanelPadding * 2;
        var y = UiLayout.LogicalHeight - ChatAreaHeight - height - 12;
        return new Rectangle(12, y, StatusPanelWidth, height);
    }

    public static int MiniMapMargin => TopBarHeight + 12;

    public static int ScreenCenterX => UiLayout.WorldViewportWidth / 2;

    public static int ScreenCenterY => UiLayout.WorldViewportHeight / 2;
}
