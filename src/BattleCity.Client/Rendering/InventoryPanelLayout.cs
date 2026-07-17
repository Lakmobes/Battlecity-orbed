using BattleCity.Shared.Data;

namespace BattleCity.Client.Rendering;

/// <summary>Horizontal top-bar inventory slot layout.</summary>
public static class InventoryPanelLayout
{
    public const int IconSize = ModernHudLayout.InventorySlotSize;

    public static (int X, int Y) GetSlotScreenPosition(int slotIndex) =>
        (ModernHudLayout.GetInventorySlotX(slotIndex), ModernHudLayout.InventorySlotY);
}
