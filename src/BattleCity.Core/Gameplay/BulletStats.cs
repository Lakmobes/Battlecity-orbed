using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

public static class BulletStats
{
    public static float GetInitialLife(BulletKind kind) =>
        kind switch
        {
            BulletKind.Laser => 260f,
            BulletKind.Rocket => 340f,
            BulletKind.Flare => 2500f,
            BulletKind.Plasma => 340f,
            _ => 260f,
        };

    public static int GetDamage(BulletKind kind) =>
        kind switch
        {
            BulletKind.Laser => GameConstants.DamageLaser,
            BulletKind.Rocket => GameConstants.DamageRocket,
            BulletKind.Flare => GameConstants.DamageLaser,
            BulletKind.Plasma => GameConstants.DamagePlasmaTurretBullet,
            _ => GameConstants.DamageLaser,
        };

    public static float GetMoveFactor(BulletKind kind) =>
        kind == BulletKind.Flare
            ? GameConstants.MovementSpeedFlare
            : GameConstants.MovementSpeedBullet;
}
