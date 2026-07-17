using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

/// <summary>Layout for the online meeting room.</summary>
public static class MeetingRoomLayout
{
    public const int RowHeight = 40;
    public const int HeaderHeight = 28;
    public const int PanelPadding = 14;

    public static Rectangle CitiesPanel =>
        new(24, 72, UiLayout.LogicalWidth / 2 - 36, UiLayout.LogicalHeight - 160);

    public static Rectangle ChatPanel =>
        new(UiLayout.LogicalWidth / 2 + 12, 72, UiLayout.LogicalWidth / 2 - 36, UiLayout.LogicalHeight - 160);

    public static Rectangle RefreshButton =>
        new(24, UiLayout.LogicalHeight - 72, 140, 32);

    public static Rectangle QuitHint =>
        new(UiLayout.LogicalWidth / 2 + 12, UiLayout.LogicalHeight - 72, UiLayout.LogicalWidth / 2 - 36, 32);

    public static int CityListTop => CitiesPanel.Y + PanelPadding + HeaderHeight;

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

        for (var i = 0; i < cityCount; i++)
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
