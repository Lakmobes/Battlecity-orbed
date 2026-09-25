using BattleCity.Shared.Data;
using BattleCity.Shared.Gameplay;

using Xunit;

namespace BattleCity.Shared.Tests;

public class ItemDamageSyncTests
{
    [Theory]
    [InlineData(ItemType.Wall, 20, true)]
    [InlineData(ItemType.Wall, 21, false)]
    [InlineData(ItemType.Turret, 8, true)]
    [InlineData(ItemType.Turret, 9, false)]
    [InlineData(ItemType.Sleeper, 16, true)]
    [InlineData(ItemType.Plasma, 20, true)]
    [InlineData(ItemType.Bomb, 1, false)]
    public void ShouldBroadcastItemLife_MatchesLegacyThresholds(ItemType type, int health, bool expected)
    {
        Assert.Equal(expected, ItemDamageSync.ShouldBroadcastItemLife(type, health));
    }

    [Fact]
    public void LegacyBurnLifeValue_IsOne()
    {
        Assert.Equal(1, ItemDamageSync.LegacyBurnLifeValue);
    }
}
