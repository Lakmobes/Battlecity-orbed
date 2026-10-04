using BattleCity.Client.Assets;

using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

/// <summary>Overlay HUD layout for the modern full-screen in-game UI.</summary>
public static class ModernHudLayout
{
    public const int TopBarPadding = 14;
    public const int InventorySlotSize = 56;
    public const int InventorySlotSpacing = 6;
    public const int HealthBarWidth = 520;
    public const int HealthBarHeight = 28;
    public const int HealthBarGap = 8;

    public const int TopBarHeight =
        TopBarPadding + InventorySlotSize + HealthBarGap + HealthBarHeight + TopBarPadding;

    public const int CompassSize = 112;
    public const int CompassInnerSize = 58;
    public const int CompassMargin = 16;

    public const int RadarPanelSize = 264;
    public const int RadarMargin = 16;
    public const int MapButtonWidth = 148;
    public const int MapButtonHeight = 40;

    /// <summary>Inset past the nine-slice border so text sits inside the frame.</summary>
    public const int StatusPanelPadding = HudSpriteNames.PanelBorder + 8;

    public const int StatusLineHeight = 20;
    public const int StatusPanelWidth = 400;

    public const int ChatAreaHeight = 220;
    public const int ChatPanelWidth = 640;

    public const int HamburgerSize = 46;
    public const int MenuButtonWidth = 176;
    public const int HamburgerMargin = 16;

    /// <summary>0 = All, 1 = Team, 2 = System, 3 = Commands.</summary>
    public static int ChatTab { get; set; }

    public static Rectangle TopBar =>
        new(0, 0, UiLayout.LogicalWidth, TopBarHeight);

    public static Rectangle MapButtonBounds =>
        new(
            UiLayout.LogicalWidth - MapButtonWidth - RadarMargin,
            16,
            MapButtonWidth,
            MapButtonHeight);

    /// <summary>Proximity radar under the map button. Compass sits beneath it.</summary>
    public static Rectangle RadarBounds =>
        new(
            UiLayout.LogicalWidth - RadarPanelSize - RadarMargin,
            MapButtonBounds.Bottom + 8,
            RadarPanelSize,
            RadarPanelSize);

    public static Rectangle CompassBounds =>
        new(
            RadarBounds.X,
            RadarBounds.Bottom + 8,
            CompassSize,
            CompassSize);

    public static Rectangle OrbCompassBounds =>
        new(
            RadarBounds.Right - CompassSize,
            CompassBounds.Y,
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
        new(HamburgerMargin, 16, MenuButtonWidth, HamburgerSize);

    public static Rectangle InfoCard =>
        new(RadarBounds.X, CompassBounds.Bottom + 8, RadarBounds.Width, 168);

    public static Rectangle ChatPanel =>
        new(16, UiLayout.LogicalHeight - ChatAreaHeight, ChatPanelWidth, ChatAreaHeight);

    public static int InventorySlotY => TopBarPadding;

    public static int HealthBarY =>
        TopBarPadding + InventorySlotSize + HealthBarGap;

    public static Rectangle ChatTabBounds(int index)
    {
        var panel = ChatPanel;
        const int tabCount = 4;
        var width = (panel.Width - 20 - ((tabCount - 1) * 6)) / tabCount;
        return new Rectangle(panel.X + 8 + (index * (width + 6)), panel.Y + 6, width, 26);
    }

    /// <summary>HUD chrome clicks. Does not change gameplay state.</summary>
    public static bool TryHandleChromeClick(int x, int y, out bool toggleMiniMap)
    {
        toggleMiniMap = false;
        var point = new Point(x, y);
        if (MapButtonBounds.Contains(point))
        {
            toggleMiniMap = true;
            return true;
        }

        if (ChatPanel.Contains(point))
        {
            for (var i = 0; i < 4; i++)
            {
                if (ChatTabBounds(i).Contains(point))
                {
                    ChatTab = i;
                    break;
                }
            }

            return true;
        }

        return false;
    }

    public static int GetCenteredRowStartX(int slotCount)
    {
        if (slotCount <= 0)
        {
            return (UiLayout.LogicalWidth - HealthBarWidth) / 2;
        }

        var rowWidth = slotCount * InventorySlotSize + (slotCount - 1) * InventorySlotSpacing;
        var left = HamburgerBounds.Right + 20;
        var right = RadarBounds.Left - 20;
        var available = Math.Max(0, right - left);
        return left + Math.Max(0, (available - rowWidth) / 2);
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
