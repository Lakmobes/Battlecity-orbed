using System.Numerics;

using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;

using Xunit;

namespace BattleCity.Core.Tests;

public class TankPlacementTests
{
    [Fact]
    public void GetTileFromTopLeft_UsesTankCenter()
    {
        var (gridX, gridY) = TankPlacement.GetTileFromTopLeft(new Vector2(100f, 200f));

        Assert.Equal((int)((100f + GameConstants.TileSize / 2f) / GameConstants.TileSize), gridX);
        Assert.Equal((int)((200f + GameConstants.TileSize / 2f) / GameConstants.TileSize), gridY);
    }
}
