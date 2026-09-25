using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

/// <summary>Shared 1920×1080 menu / lobby palette and metrics.</summary>
public static class MenuTheme
{
    public static readonly Color Backdrop = new(10, 14, 22);
    public static readonly Color HeaderBar = new(14, 18, 28, 240);
    public static readonly Color FooterBar = new(8, 10, 16, 230);

    public static readonly Color PanelFill = new(14, 18, 28, 240);
    public static readonly Color PanelBorder = new(70, 110, 170, 180);
    public static readonly Color PanelInnerLine = new(255, 255, 255, 18);

    public static readonly Color ButtonIdleFill = new(18, 24, 36, 220);
    public static readonly Color ButtonFocusFill = new(32, 28, 14, 240);
    public static readonly Color ButtonIdleBorder = new(55, 75, 110, 160);
    public static readonly Color ButtonFocusBorder = new(230, 190, 70, 230);
    public static readonly Color SelectionAccent = new(230, 190, 70);

    public static readonly Color FieldIdleFill = new(8, 12, 20, 200);
    public static readonly Color FieldFocusFill = new(28, 24, 12, 220);
    public static readonly Color FieldIdleBorder = new(50, 70, 105, 150);
    public static readonly Color FieldFocusBorder = new(230, 190, 70, 220);

    public static readonly Color TextPrimary = new(236, 238, 244);
    public static readonly Color TextSecondary = new(188, 194, 210);
    public static readonly Color TextMuted = new(130, 136, 156);
    public static readonly Color TextAccent = new(240, 205, 90);
    public static readonly Color TextDanger = new(255, 140, 120);

    public static readonly Color RowSelected = new(40, 34, 16, 220);
    public static readonly Color RowHover = new(28, 38, 58, 160);

    public const int HeaderHeight = 72;
    public const int FooterHeight = 64;
    public const int FormFieldHeight = 40;
    public const int FormFieldGap = 12;
    public const int MenuButtonHeight = 56;
    public const int MenuButtonGap = 12;
    public const int SelectionBarWidth = 4;
}
