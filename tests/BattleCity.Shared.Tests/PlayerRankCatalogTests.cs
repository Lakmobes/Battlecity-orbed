using BattleCity.Shared.Gameplay;

using Xunit;

namespace BattleCity.Shared.Tests;

public class PlayerRankCatalogTests
{
    [Theory]
    [InlineData(0, "Private")]
    [InlineData(99, "Private")]
    [InlineData(100, "Corporal")]
    [InlineData(500, "Sergeant Major")]
    [InlineData(500000, "King")]
    public void GetRank_MatchesLegacyThresholds(int points, string expectedRank)
    {
        Assert.Equal(expectedRank, PlayerRankCatalog.GetRank(points));
    }

    [Fact]
    public void FormatChatName_PrefixesRank()
    {
        Assert.Equal("Captain Demo", PlayerRankCatalog.FormatChatName("Demo", 3000));
        Assert.Equal("Admin Demo", PlayerRankCatalog.FormatChatName("Demo", 3000, isAdmin: true));
    }
}
