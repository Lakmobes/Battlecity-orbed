using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

/// <summary>Shared 1920×1080 menu / lobby palette and metrics.</summary>
public static class MenuTheme
{
    public static readonly Color Backdrop = new(12, 16, 28);
    public static readonly Color HeaderBar = new(18, 24, 40, 230);
    public static readonly Color FooterBar = new(8, 10, 16, 210);

    public static readonly Color PanelFill = new(16, 22, 38, 235);
    public static readonly Color PanelBorder = new(90, 140, 220, 160);
    public static readonly Color PanelBorderStrong = new(255, 214, 70, 200);

    public static readonly Color ButtonIdleFill = new(20, 26, 40, 210);
    public static readonly Color ButtonFocusFill = new(48, 36, 12, 230);
    public static readonly Color ButtonIdleBorder = new(80, 100, 140, 180);
    public static readonly Color ButtonFocusBorder = new(255, 214, 70);

    public static readonly Color FieldIdleFill = new(10, 14, 24, 180);
    public static readonly Color FieldFocusFill = new(36, 28, 10, 210);
    public static readonly Color FieldIdleBorder = new(70, 90, 130, 140);
    public static readonly Color FieldFocusBorder = new(255, 214, 70, 220);

    public static readonly Color TextPrimary = new(240, 242, 248);
    public static readonly Color TextSecondary = new(210, 214, 230);
    public static readonly Color TextMuted = new(150, 155, 175);
    public static readonly Color TextAccent = new(255, 214, 70);
    public static readonly Color TextDanger = new(255, 140, 120);

    public static readonly Color RowSelected = new(48, 40, 18, 200);
    public static readonly Color RowHover = new(40, 52, 80, 140);

    public const int HeaderHeight = 72;
    public const int FooterHeight = 64;
    public const int FormFieldHeight = 36;
    public const int FormFieldGap = 10;
    public const int MenuButtonHeight = 56;
    public const int MenuButtonGap = 14;

    public static float FocusPulse(float timeSeconds) =>
        1f + 0.025f * MathF.Sin(timeSeconds * 6f);
}
