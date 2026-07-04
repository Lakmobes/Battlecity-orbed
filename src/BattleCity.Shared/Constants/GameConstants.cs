namespace BattleCity.Shared.Constants;

/// <summary>
/// Gameplay constants shared by client and server.
/// Values marked synchronized in legacy CConstants.h must stay identical on both sides.
/// </summary>
public static class GameConstants
{
    public const int TileSize = 48;
    public const int MapSize = 512;
    public const int WorldSizePixels = MapSize * TileSize;

    public const int CostBuilding = 500_000;
    public const int DamageLaser = 5;
    public const int DamageMine = 19;
    public const int DamageRocket = 8;

    public const int DistanceMaxFromBuilding = 11;
    public const int DistanceMaxFromCommandCenter = 29;

    public const string CitiesFolder = "cities";
    public const string CityFileExtension = ".city";

    public const int MaxCities = 64;
    public const int MaxHealth = 40;
    public const int MaxPlayers = 64;
    public const int MaxPlayersPerCity = 4;

    public const float MovementSpeedAdmin = 1.0f;
    public const float MovementSpeedFlare = 0.10f;
    public const float MovementSpeedBullet = 0.80f;
    public const float MovementSpeedPlayer = 0.38f;

    public const int TimerChangeTank = 1000;
    public const int TimerCloak = 5000;
    public const int TimerDemolish = 3000;
    public const int TimerDfg = 5000;
    public const int TimerReloadSurfaces = 3000;
    public const int TimerRespawn = 10_000;
    public const int TimerShootAdmin = 50;
    public const int TimerShootLaser = 650;
    public const int TimerShootRocket = 650;
    public const int TimerShootFlare = 500;
    public const int TimerUnderAttack = 3000;
    public const int TimerTurretTurn = 250;
    public const int TimerTurretStartup = 2000;
    public const int TurretTargetRangePixels = 360;
    public const int TurretAnimationIntervalMs = 50;
    public const int DamageTurretBullet = 4;
    public const int DamagePlasmaTurretBullet = 5;
    public const int TurretMaxHealth = 32;
    public const int SleeperTurretMaxHealth = 16;
    public const int PlasmaTurretMaxHealth = 40;

    public const int SectorSize = 16;
    public const int MaxSectors = MapSize / SectorSize;

    /// <summary>Player collision inset applied on each side of the 48px sprite (legacy/CCollision.cpp).</summary>
    public const int PlayerCollisionInset = 8;

    /// <summary>Building collision box size in pixels (legacy/CCollision.cpp).</summary>
    public const int BuildingCollisionSize = 144;

    /// <summary>Building collision offset from grid origin in pixels.</summary>
    public const int BuildingCollisionOffset = 2;
}
