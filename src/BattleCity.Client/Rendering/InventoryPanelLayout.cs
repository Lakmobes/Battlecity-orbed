using BattleCity.Client.Assets;
using BattleCity.Core.Ecs.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using BattleCity.Shared.Data;

namespace BattleCity.Client.Rendering;

/// <summary>Legacy inventory slot layout from <c>legacy/client/CDrawing.cpp::DrawInventory</c>.</summary>
public static class InventoryPanelLayout
{
    private static readonly int[] ColumnOffsets = [7, 42, 77];
    private static readonly int[] RowOffsets = [267, 302, 337, 372];

    public const int IconSize = 32;

    public static (int X, int Y) GetSlotScreenPosition(int panelX, ItemType type)
    {
        var index = (int)type;
        var column = index % ColumnOffsets.Length;
        var row = index / ColumnOffsets.Length;
        return (panelX + ColumnOffsets[column], RowOffsets[row]);
    }
}
