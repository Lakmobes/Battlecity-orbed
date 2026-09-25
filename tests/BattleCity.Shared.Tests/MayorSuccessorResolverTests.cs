using BattleCity.Shared.Gameplay;

using Xunit;

namespace BattleCity.Shared.Tests;

public class MayorSuccessorResolverTests
{
    [Fact]
    public void TryResolve_PrefersDesignatedTeammate()
    {
        var roster = new (byte, byte)[]
        {
            (1, 27),
            (2, 27),
            (3, 27),
        };

        Assert.True(MayorSuccessorResolver.TryResolve(
            cityId: 27,
            excludedPlayerId: 1,
            designatedSuccessorId: 3,
            roster,
            out var successor));
        Assert.Equal((byte)3, successor);
    }

    [Fact]
    public void TryResolve_FallsBackWhenDesignatedMissing()
    {
        var roster = new (byte, byte)[]
        {
            (1, 27),
            (2, 27),
        };

        Assert.True(MayorSuccessorResolver.TryResolve(
            cityId: 27,
            excludedPlayerId: 1,
            designatedSuccessorId: 9,
            roster,
            out var successor));
        Assert.Equal((byte)2, successor);
    }

    [Fact]
    public void TryResolve_IgnoresOtherCities()
    {
        var roster = new (byte, byte)[]
        {
            (1, 27),
            (2, 18),
        };

        Assert.False(MayorSuccessorResolver.TryResolve(
            cityId: 27,
            excludedPlayerId: 1,
            designatedSuccessorId: 2,
            roster,
            out _));
    }
}
