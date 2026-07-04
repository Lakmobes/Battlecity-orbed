using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

/// <summary>Layout for the online meeting room (800x600 logical).</summary>
public static class MeetingRoomLayout
{
    public const int RowHeight = 34;
    public const int HeaderHeight = 24;
    public const int PanelPadding = 10;

    public static readonly Rectangle CitiesPanel = new(16, 56, 360, 468);
    public static readonly Rectangle ChatPanel = new(384, 56, 400, 468);
    public static readonly Rectangle RefreshButton = new(16, 536, 120, 28);
    public static readonly Rectangle QuitHint = new(384, 536, 400, 28);

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
