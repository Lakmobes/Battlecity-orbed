using BattleCity.Shared.Gameplay;

using Xunit;

namespace BattleCity.Shared.Tests;

public class PromotionMessagesTests
{
    [Fact]
    public void Format_MatchesLegacyTemplate()
    {
        Assert.Equal(
            "Demo has been promoted to Captain!",
            PromotionMessages.Format("Demo", "Captain"));
    }

    [Theory]
    [InlineData(98, 2, true, "Corporal")]
    [InlineData(50, 1, false, "Private")]
    [InlineData(199, 1, true, "Sergeant")]
    public void GetRank_DetectsPromotionThresholds(int currentPoints, int delta, bool promotes, string expectedRank)
    {
        var oldRank = PlayerRankCatalog.GetRank(currentPoints);
        var newRank = PlayerRankCatalog.GetRank(currentPoints + delta);
        Assert.Equal(promotes, oldRank != newRank);
        Assert.Equal(expectedRank, newRank);
    }
}
