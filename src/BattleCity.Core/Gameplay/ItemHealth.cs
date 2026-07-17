using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

/// <summary>Legacy item life values from <c>legacy/server/CItem.cpp</c>.</summary>
public static class ItemHealth
{
    /// <summary>Any world-placed item can be destroyed by laser/bazooka.</summary>
    public static bool IsDamageable(ItemType type) => ItemCatalog.IsPlaceable(type);

    public static int GetMaxHealth(ItemType type) =>
        type switch
        {
            ItemType.Wall => 40,
            ItemType.Turret => 32,
            ItemType.Sleeper => 16,
            ItemType.Plasma => 40,
            // Soft placeables die in one laser hit (DamageLaser = 5).
            ItemType.Mine => 5,
            ItemType.Bomb => 5,
            ItemType.Orb => 5,
            ItemType.Dfg => 5,
            _ => 0,
        };
}
