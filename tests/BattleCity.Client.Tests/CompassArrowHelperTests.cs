using System.Numerics;

using BattleCity.Client.Rendering;

using Xunit;

namespace BattleCity.Client.Tests;

public class CompassArrowHelperTests
{
    [Theory]
    [InlineData(0f, -100f, 0)] // north
    [InlineData(100f, 0f, 2)] // east
    [InlineData(0f, 100f, 4)] // south
    [InlineData(-100f, 0f, 6)] // west
    public void ComputeArrowIndex_Cardinals(float dx, float dy, int expected)
    {
        var player = Vector2.Zero;
        var city = new Vector2(dx, dy);
        Assert.Equal(expected, CompassArrowHelper.ComputeArrowIndex(player, city));
    }

    [Theory]
    [InlineData(0, 2)] // N -> frame 2
    [InlineData(2, 0)] // E -> frame 0
    [InlineData(4, 6)] // S -> frame 6
    [InlineData(6, 4)] // W -> frame 4
    public void ToLegacyArrowFrame_MatchesImgArrowsLayout(int index, int frame)
    {
        Assert.Equal(frame, CompassArrowHelper.ToLegacyArrowFrame(index));
    }
}
