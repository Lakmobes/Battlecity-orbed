using BattleCity.Core.City;
using BattleCity.Core.Collision;
using BattleCity.Core.Levels;
using BattleCity.Shared.Constants;

using Xunit;

namespace BattleCity.Core.Tests;

public class CommandCenterSpawnTests
{
    [Fact]
    public void GetDrivePlatformSpawnPosition_PlacesTankOnSouthernRow()
    {
        var spriteTopLeft = BuildingPlacement.GridAnchorToWorldPosition(12, 14);
        var spawn = CommandCenterLookup.GetDrivePlatformSpawnPosition(spriteTopLeft);
        var platform = BuildingCollision.GetDrivePlatformBounds(spriteTopLeft);

        Assert.InRange(spawn.X, platform.Left, platform.Right - GameConstants.TileSize);
        Assert.InRange(spawn.Y, platform.Top, platform.Bottom - GameConstants.TileSize);
        Assert.Equal(platform.Top + (platform.Height - GameConstants.TileSize) / 2f, spawn.Y);
    }
}
