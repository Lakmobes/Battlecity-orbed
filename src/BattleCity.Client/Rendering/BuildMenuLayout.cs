using BattleCity.Core.City;
using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;

namespace BattleCity.Client.Rendering;

/// <summary>Build menu layout and hit testing (legacy CDrawing::DrawBuildMenu / CInput build menu).</summary>
public static class BuildMenuLayout
{
    public const int MenuWidth = 220;
    public const int LineHeight = 18;

    public readonly record struct Entry(int BuildSlot, string Label, int TopY, int BottomY);

    public readonly record struct Layout(int MenuX, int TopY, int BottomY, IReadOnlyList<Entry> Entries);

    public static Layout Create(int menuAnchorX, int menuAnchorY, int screenWidth, int screenHeight, CityBuildState build)
    {
        var entries = BuildEntries(menuAnchorY, build);
        var contentHeight = entries.Count > 0 ? entries[0].BottomY - entries[^1].TopY : LineHeight;
        var menuY = entries.Count > 0 ? entries[0].BottomY : Math.Min(menuAnchorY, screenHeight - LineHeight);
        var topY = entries.Count > 0 ? entries[^1].TopY : menuY - LineHeight;

        if (topY < 8)
        {
            menuY = 8 + contentHeight;
            entries = BuildEntries(menuY, build);
            topY = entries.Count > 0 ? entries[^1].TopY : 8;
        }

        var menuX = Math.Clamp(menuAnchorX, 16, screenWidth - MenuWidth);
        return new Layout(menuX, topY, menuY + 4, entries);
    }

    public static bool TryHitTest(
        int menuAnchorX,
        int menuAnchorY,
        int screenWidth,
        int screenHeight,
        CityBuildState build,
        int mouseX,
        int mouseY,
        out int buildSlot)
    {
        buildSlot = 0;
        var layout = Create(menuAnchorX, menuAnchorY, screenWidth, screenHeight, build);

        if (mouseX < layout.MenuX - 26
            || mouseX > layout.MenuX + MenuWidth
            || mouseY < layout.TopY
            || mouseY >= layout.BottomY)
        {
            return false;
        }

        foreach (var entry in layout.Entries)
        {
            if (mouseY >= entry.TopY && mouseY < entry.BottomY)
            {
                buildSlot = entry.BuildSlot;
                return true;
            }
        }

        return false;
    }

    public static bool ContainsPoint(
        int menuAnchorX,
        int menuAnchorY,
        int screenWidth,
        int screenHeight,
        CityBuildState build,
        int mouseX,
        int mouseY)
    {
        var layout = Create(menuAnchorX, menuAnchorY, screenWidth, screenHeight, build);
        return mouseX >= layout.MenuX - 26
            && mouseX <= layout.MenuX + MenuWidth
            && mouseY >= layout.TopY
            && mouseY < layout.BottomY;
    }

    private static List<Entry> BuildEntries(int menuAnchorY, CityBuildState build)
    {
        var entries = new List<Entry>();
        var drawY = menuAnchorY;

        // Legacy CDrawing::DrawBuildMenu: Demolish at the bottom, then items stacked upward.
        entries.Add(new Entry(-1, "Demolish", drawY, drawY + LineHeight));

        for (var i = BuildingCatalog.MenuNames.Count - 1; i >= 0; i--)
        {
            if (!CityBuildPermissions.IsVisibleInMenu(build, i))
            {
                continue;
            }

            drawY -= LineHeight;
            entries.Add(new Entry(i + 1, BuildingCatalog.MenuNames[i], drawY, drawY + LineHeight));
        }

        return entries;
    }
}
