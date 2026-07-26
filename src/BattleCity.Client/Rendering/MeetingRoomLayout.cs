using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

/// <summary>Layout for the online meeting room (logical 1920×1080).</summary>
public static class MeetingRoomLayout
{
    public const int RowHeight = 32;
    public const int HeaderHeight = 26;
    public const int PanelPadding = 14;
    public const int PanelTop = 88;
    public const int PanelBottomMargin = 88;
    public const int FooterReserved = 40;

    public static Rectangle CitiesPanel =>
        new(32, PanelTop, UiLayout.LogicalWidth / 2 - 48, UiLayout.LogicalHeight - PanelTop - PanelBottomMargin);

    public static Rectangle ChatPanel =>
        new(UiLayout.LogicalWidth / 2 + 16, PanelTop, UiLayout.LogicalWidth / 2 - 48, UiLayout.LogicalHeight - PanelTop - PanelBottomMargin);

    public static Rectangle RefreshButton =>
        new(32, UiLayout.LogicalHeight - 64, 130, 28);

    public static Rectangle QuitHint =>
        new(UiLayout.LogicalWidth / 2 + 16, UiLayout.LogicalHeight - 64, UiLayout.LogicalWidth / 2 - 48, 28);

    public static int CityListTop => CitiesPanel.Y + PanelPadding + HeaderHeight;

    public static int MaxVisibleCityRows =>
        Math.Max(1, (CitiesPanel.Height - PanelPadding * 2 - HeaderHeight - FooterReserved) / RowHeight);

    public static Rectangle GetCityRowBounds(int index) =>
        new(
            CitiesPanel.X + PanelPadding,
            CityListTop + index * RowHeight,
            CitiesPanel.Width - PanelPadding * 2,
            RowHeight);

    public static bool TryGetCityIndexAt(int x, int y, int cityCount, out int index)
    {
        index = -1;
        if (cityCount <= 0)
        {
            return false;
        }

        var visible = Math.Min(cityCount, MaxVisibleCityRows);
        for (var i = 0; i < visible; i++)
        {
            if (GetCityRowBounds(i).Contains(x, y))
            {
                index = i;
                return true;
            }
        }

        return false;
    }
}
