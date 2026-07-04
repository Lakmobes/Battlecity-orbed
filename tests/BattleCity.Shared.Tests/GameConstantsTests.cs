using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Shared.Tests;

public class GameConstantsTests
{
    [Theory]
    [InlineData(nameof(GameConstants.CostBuilding), 500_000)]
    [InlineData(nameof(GameConstants.DamageLaser), 5)]
    [InlineData(nameof(GameConstants.DamageMine), 19)]
    [InlineData(nameof(GameConstants.DamageRocket), 8)]
    [InlineData(nameof(GameConstants.MaxHealth), 40)]
    [InlineData(nameof(GameConstants.TimerCloak), 5000)]
    [InlineData(nameof(GameConstants.TimerDfg), 5000)]
    [InlineData(nameof(GameConstants.TimerRespawn), 10_000)]
    [InlineData(nameof(GameConstants.TimerShootLaser), 650)]
    [InlineData(nameof(GameConstants.TimerShootRocket), 650)]
    public void SynchronizedLegacyConstantsMatchClientAndServer(string _, int expected)
    {
        var actual = _ switch
        {
            nameof(GameConstants.CostBuilding) => GameConstants.CostBuilding,
            nameof(GameConstants.DamageLaser) => GameConstants.DamageLaser,
            nameof(GameConstants.DamageMine) => GameConstants.DamageMine,
            nameof(GameConstants.DamageRocket) => GameConstants.DamageRocket,
            nameof(GameConstants.MaxHealth) => GameConstants.MaxHealth,
            nameof(GameConstants.TimerCloak) => GameConstants.TimerCloak,
            nameof(GameConstants.TimerDfg) => GameConstants.TimerDfg,
            nameof(GameConstants.TimerRespawn) => GameConstants.TimerRespawn,
            nameof(GameConstants.TimerShootLaser) => GameConstants.TimerShootLaser,
            nameof(GameConstants.TimerShootRocket) => GameConstants.TimerShootRocket,
            _ => throw new ArgumentOutOfRangeException(nameof(_)),
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SynchronizedMovementSpeedsMatchLegacy()
    {
        Assert.Equal(0.10f, GameConstants.MovementSpeedFlare);
        Assert.Equal(0.80f, GameConstants.MovementSpeedBullet);
        Assert.Equal(0.38f, GameConstants.MovementSpeedPlayer);
    }

    [Fact]
    public void WorldLayoutMatchesLegacyMap()
    {
        Assert.Equal(48, GameConstants.TileSize);
        Assert.Equal(512, GameConstants.MapSize);
        Assert.Equal(24_576, GameConstants.WorldSizePixels);
        Assert.Equal(16, GameConstants.SectorSize);
        Assert.Equal(32, GameConstants.MaxSectors);
    }

    [Fact]
    public void PlayerLimitsMatchLegacy()
    {
        Assert.Equal(64, GameConstants.MaxCities);
        Assert.Equal(64, GameConstants.MaxPlayers);
        Assert.Equal(4, GameConstants.MaxPlayersPerCity);
    }

    [Fact]
    public void TerrainTileTypesMatchLegacyMapSquareConstants()
    {
        Assert.Equal(0, (int)TerrainTileType.Open);
        Assert.Equal(1, (int)TerrainTileType.Lava);
        Assert.Equal(2, (int)TerrainTileType.Rock);
        Assert.Equal(3, (int)TerrainTileType.CityCenter);
    }

    [Fact]
    public void ItemTypeIdsMatchLegacyConstants()
    {
        Assert.Equal(0, (int)ItemType.Cloak);
        Assert.Equal(1, (int)ItemType.Rocket);
        Assert.Equal(11, (int)ItemType.Plasma);
    }

    [Fact]
    public void ClientAndServerRadarSizesIntentionallyDiffer()
    {
        Assert.Equal(2400, ClientConstants.RadarSize);
        Assert.Equal(1800, ServerConstants.RadarSize);
        Assert.NotEqual(ClientConstants.RadarSize, ServerConstants.RadarSize);
    }
}
