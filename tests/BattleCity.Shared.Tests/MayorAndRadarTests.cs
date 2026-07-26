using BattleCity.Shared.Chat;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Gameplay;

using Xunit;

namespace BattleCity.Shared.Tests;

public class RadarChatRouterTests
{
    [Fact]
    public void RadarAndTeam_DeliversToSameCityOutsideRadar()
    {
        var delivers = RadarChatRouter.ShouldDeliver(
            RadarChatDeliveryMode.RadarAndTeam,
            senderX: 0,
            senderY: 0,
            senderCityId: 2,
            recipientX: ServerConstants.RadarSize + 100,
            recipientY: 0,
            recipientCityId: 2);

        Assert.True(delivers);
    }

    [Fact]
    public void RadarAndTeam_SkipsDifferentCityOutsideRadar()
    {
        var delivers = RadarChatRouter.ShouldDeliver(
            RadarChatDeliveryMode.RadarAndTeam,
            senderX: 0,
            senderY: 0,
            senderCityId: 0,
            recipientX: ServerConstants.RadarSize + 100,
            recipientY: 0,
            recipientCityId: 1);

        Assert.False(delivers);
    }

    [Fact]
    public void RadarOnly_DeliversToNearbyPlayerRegardlessOfCity()
    {
        var delivers = RadarChatRouter.ShouldDeliver(
            RadarChatDeliveryMode.RadarOnly,
            senderX: 100,
            senderY: 100,
            senderCityId: 0,
            recipientX: 200,
            recipientY: 100,
            recipientCityId: 3);

        Assert.True(delivers);
    }

    [Fact]
    public void RadarOnly_SkipsPlayerOutsideRadarEvenOnSameTeam()
    {
        var delivers = RadarChatRouter.ShouldDeliver(
            RadarChatDeliveryMode.RadarOnly,
            senderX: 0,
            senderY: 0,
            senderCityId: 0,
            recipientX: ServerConstants.RadarSize + 50,
            recipientY: 0,
            recipientCityId: 0);

        Assert.False(delivers);
    }
}

public class TankSpriteSelectorTests
{
    [Fact]
    public void GetSourceY_UsesMayorRowForFriendlyMayor()
    {
        Assert.Equal(TankSpriteSelector.TeamMayorRow, TankSpriteSelector.GetSourceY(0, 0, isMayor: true));
    }

    [Fact]
    public void GetSourceY_UsesEnemyRegularRowForOtherCity()
    {
        Assert.Equal(TankSpriteSelector.EnemyRegularRow, TankSpriteSelector.GetSourceY(0, 1, isMayor: false));
    }

    [Fact]
    public void GetSourceY_UsesEnemyMayorRowForOtherCityMayor()
    {
        Assert.Equal(TankSpriteSelector.EnemyMayorRow, TankSpriteSelector.GetSourceY(0, 1, isMayor: true));
    }

    [Fact]
    public void GetSourceY_UsesAdminRowRegardlessOfCityOrMayor()
    {
        Assert.Equal(
            TankSpriteSelector.AdminRow,
            TankSpriteSelector.GetSourceY(0, 1, isMayor: true, isAdmin: true));
    }

    [Fact]
    public void IsAdminAccount_MatchesAdminUsername()
    {
        Assert.True(TankSpriteSelector.IsAdminAccount("admin"));
        Assert.True(TankSpriteSelector.IsAdminAccount("Admin"));
        Assert.False(TankSpriteSelector.IsAdminAccount("player"));
    }
}
