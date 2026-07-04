using BattleCity.Core;
using BattleCity.Shared;

using Xunit;

namespace BattleCity.Core.Tests;

public class ScaffoldTests
{
    [Fact]
    public void CoreReferencesSharedVersionAndFixedTimestep()
    {
        Assert.Equal(GameInfo.Version, AssemblyMarker.Version);
        Assert.Equal(1f / 60f, AssemblyMarker.FixedTimestep, precision: 5);
    }

    [Fact]
    public void GameInfoHasTitle()
    {
        Assert.Equal("Battle City", GameInfo.Title);
    }
}
