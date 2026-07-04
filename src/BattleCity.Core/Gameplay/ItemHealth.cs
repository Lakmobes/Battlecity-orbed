using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

/// <summary>Legacy item life values from <c>legacy/server/CItem.cpp</c>.</summary>
public static class ItemHealth
{
    public static bool IsDamageable(ItemType type) => type >= ItemType.Wall;

    public static int GetMaxHealth(ItemType type) =>
        type switch
        {
            ItemType.Wall => 40,
            ItemType.Turret => 32,
            ItemType.Sleeper => 16,
            ItemType.Plasma => 40,
            _ => 0,
        };
}
