using System.Numerics;

using BattleCity.Core.Collision;

using Xunit;

namespace BattleCity.Core.Tests;

public sealed class BuildingCornerSealTests
{
    [Fact]
    public void DiagonalCorners_BlockABulletThatWouldSlipThrough()
    {
        var first = new AxisAlignedBox(0, 0, 144, 144);
        var second = new AxisAlignedBox(144, 144, 144, 144);
        var buildings = new[] { first, second };

        Assert.True(BuildingCornerSeal.TryGetSharedCorner(first, second, out var corner));
        Assert.Equal(144f, corner.X);
        Assert.Equal(144f, corner.Y);

        var from = new Vector2(140, 150);
        var to = new Vector2(150, 140);
        var bullet = new AxisAlignedBox(to.X - 2, to.Y - 2, 4, 4);
        var previous = new AxisAlignedBox(from.X - 2, from.Y - 2, 4, 4);

        Assert.True(BuildingCornerSeal.BlocksSegment(buildings, bullet, previous, from, to));
    }

    [Fact]
    public void EdgeNeighbors_AreNotSealed()
    {
        var first = new AxisAlignedBox(0, 0, 144, 144);
        var second = new AxisAlignedBox(144, 0, 144, 144);

        Assert.False(BuildingCornerSeal.TryGetSharedCorner(first, second, out _));
    }
}
