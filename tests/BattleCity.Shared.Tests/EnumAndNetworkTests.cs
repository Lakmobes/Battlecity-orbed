using BattleCity.Shared;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Shared.Tests;

public class EnumAndNetworkTests
{
    [Fact]
    public void LegacyVersionMatchesOriginalRelease()
    {
        Assert.Equal("3.5.7", NetworkConstants.LegacyVersion);
        Assert.Equal(NetworkConstants.LegacyVersion, GameInfo.LegacyVersion);
    }

    [Fact]
    public void TcpPortMatchesLegacy()
    {
        Assert.Equal(5643, NetworkConstants.TcpPort);
    }

    [Fact]
    public void ClientGameStatesMatchLegacyOrder()
    {
        Assert.Equal(0, (int)ClientGameState.Empty);
        Assert.Equal(1, (int)ClientGameState.Login);
        Assert.Equal(4, (int)ClientGameState.Game);
        Assert.Equal(10, (int)ClientGameState.Interview);
    }

    [Fact]
    public void PlayerConnectionStatesMatchLegacyServer()
    {
        Assert.Equal(0, (int)PlayerConnectionState.Disconnected);
        Assert.Equal(5, (int)PlayerConnectionState.Game);
        Assert.Equal(6, (int)PlayerConnectionState.Apply);
    }

    [Fact]
    public void SoundIdsIncludeCloakAndFlareFromLegacyClientHeader()
    {
        Assert.Equal(13, (int)SoundId.Cloak);
        Assert.Equal(14, (int)SoundId.Flare);
    }
}
