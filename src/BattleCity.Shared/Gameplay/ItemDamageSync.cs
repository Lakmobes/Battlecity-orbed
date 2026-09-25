using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Shared.Gameplay;

/// <summary>
/// Legacy <c>smItemLife</c> burn thresholds from <c>legacy/server/CBullet.cpp</c>.
/// The server sends <see cref="LegacyBurnLifeValue"/> when an item survives damage below these limits.
/// </summary>
public static class ItemDamageSync
{
    public const byte LegacyBurnLifeValue = 1;

    public static bool ShouldBroadcastItemLife(ItemType type, int healthAfterDamage)
    {
        if (healthAfterDamage <= 0)
        {
            return false;
        }

        return type switch
        {
            ItemType.Wall => healthAfterDamage < 21,
            ItemType.Turret => healthAfterDamage < 9,
            ItemType.Sleeper => healthAfterDamage < GameConstants.SleeperTurretMaxHealth + 1,
            ItemType.Plasma => healthAfterDamage < 21,
            _ => false,
        };
    }
}
