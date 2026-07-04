using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Shared.Tests;

public class RespawnPacketTests
{
    [Fact]
    public void ServerRespawnPacket_RoundTrips()
    {
        var original = new ServerRespawnPacket(playerId: 7);
        Span<byte> buffer = stackalloc byte[ServerRespawnPacket.Size];
        original.Write(buffer);

        var parsed = ServerRespawnPacket.Read(buffer);

        Assert.Equal(original.PlayerId, parsed.PlayerId);
    }

    [Fact]
    public void ServerRespawn_UsesLegacyMessageId()
    {
        Assert.Equal(83, (int)ServerMessageId.Respawn);
    }

    [Fact]
    public void ServerWarp_UsesLegacyMessageId()
    {
        Assert.Equal(39, (int)ServerMessageId.Warp);
    }
}
