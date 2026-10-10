using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

public static class ControlsMenuLayout
{
    public const int ActionCount = 16;
    public const int ResetIndex = 16;
    public const int BackIndex = 17;
    public const int ItemCount = 18;

    private const int RowHeight = 36;
    private const int PanelWidth = 920;
    private const int Columns = 2;

    public static Rectangle Panel
    {
        get
        {
            const int rows = ActionCount / Columns;
            var height = 78 + (rows * RowHeight) + 64;
            return new Rectangle(
                (UiLayout.LogicalWidth - PanelWidth) / 2,
                (UiLayout.LogicalHeight - height) / 2,
                PanelWidth,
                height);
        }
    }

    public static Rectangle RowBounds(int index)
    {
        var panel = Panel;
        if (index >= ActionCount)
        {
            var buttonWidth = (panel.Width - 48) / 2;
            var x = panel.X + 16 + ((index - ActionCount) * (buttonWidth + 16));
            return new Rectangle(x, panel.Bottom - 52, buttonWidth, 36);
        }

        var column = index % Columns;
        var row = index / Columns;
        var width = (panel.Width - 36) / Columns;
        return new Rectangle(
            panel.X + 12 + (column * (width + 12)),
            panel.Y + 58 + (row * RowHeight),
            width,
            RowHeight - 4);
    }

    public static bool TryHit(int x, int y, out int index)
    {
        var point = new Point(x, y);
        for (var i = 0; i < ItemCount; i++)
        {
            if (RowBounds(i).Contains(point))
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }
}
