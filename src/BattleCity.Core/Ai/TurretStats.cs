using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ai;

public static class TurretStats
{
    public static int GetMaxHealth(ItemType type) =>
        type switch
        {
            ItemType.Turret => GameConstants.TurretMaxHealth,
            ItemType.Sleeper => GameConstants.SleeperTurretMaxHealth,
            ItemType.Plasma => GameConstants.PlasmaTurretMaxHealth,
            _ => GameConstants.TurretMaxHealth,
        };

    public static int GetBulletDamage(ItemType type) =>
        type switch
        {
            ItemType.Plasma => GameConstants.DamagePlasmaTurretBullet,
            _ => GameConstants.DamageTurretBullet,
        };

    public static bool IsBurning(ItemType type, int currentHealth)
    {
        if (currentHealth <= 0)
        {
            return false;
        }

        return type switch
        {
            ItemType.Turret => currentHealth < 9,
            ItemType.Sleeper => currentHealth < 17,
            ItemType.Plasma => currentHealth < 21,
            _ => false,
        };
    }

    public static bool IsTurretType(ItemType type) =>
        type is ItemType.Turret or ItemType.Sleeper or ItemType.Plasma;

    public static BulletKind GetBulletKind(ItemType type) =>
        type == ItemType.Plasma ? BulletKind.Plasma : BulletKind.Laser;
}
