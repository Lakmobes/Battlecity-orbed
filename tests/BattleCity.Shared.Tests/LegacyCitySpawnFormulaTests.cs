using BattleCity.Shared.Constants;
using BattleCity.Shared.Gameplay;

using Xunit;

namespace BattleCity.Shared.Tests;

public class LegacyCitySpawnFormulaTests
{
    [Theory]
    [InlineData(0, 22992, 22992)]
    [InlineData(27, 13776, 13776)]
    [InlineData(63, 1488, 1488)]
    public void GetPixels_MatchesLegacyCMapFormula(int cityIndex, int expectedX, int expectedY)
    {
        // Verified against legacy/server/CMap.cpp:
        // (512*48) - (32 + (citIndex % 8 * 64) + 1) * 48
        var (x, y) = LegacyCitySpawnFormula.GetPixels(cityIndex);
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
    }

    [Fact]
    public void GetPixels_ClampsOutOfRangeIndex()
    {
        Assert.Equal(LegacyCitySpawnFormula.GetPixels(0), LegacyCitySpawnFormula.GetPixels(-1));
        Assert.Equal(LegacyCitySpawnFormula.GetPixels(63), LegacyCitySpawnFormula.GetPixels(999));
    }

    [Fact]
    public void TileSize_MatchesLegacy48()
    {
        Assert.Equal(48, GameConstants.TileSize);
        Assert.Equal(512, GameConstants.MapSize);
    }
}
