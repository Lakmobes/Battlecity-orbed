namespace BattleCity.Client.Rendering;

/// <summary>Horizontal top-bar inventory slot layout.</summary>
public static class InventoryPanelLayout
{
    public const int IconSize = ModernHudLayout.InventorySlotSize;

    public static (int X, int Y) GetSlotScreenPosition(int slotIndex, int slotCount) =>
        (ModernHudLayout.GetInventorySlotX(slotIndex, slotCount), ModernHudLayout.InventorySlotY);
}
