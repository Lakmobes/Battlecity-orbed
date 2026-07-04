using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Shared.Tests;

public class ExplosionPacketTests
{
    [Fact]
    public void ServerExplosionPacket_RoundTrips()
    {
        var original = new ServerExplosionPacket(cityId: 2, gridX: 15, gridY: 20);
        Span<byte> buffer = stackalloc byte[ServerExplosionPacket.Size];
        original.Write(buffer);

        var parsed = ServerExplosionPacket.Read(buffer);

        Assert.Equal(original.CityId, parsed.CityId);
        Assert.Equal(original.GridX, parsed.GridX);
        Assert.Equal(original.GridY, parsed.GridY);
    }

    [Fact]
    public void ServerExplosion_UsesLegacyMessageId()
    {
        Assert.Equal(38, (int)ServerMessageId.Explosion);
    }
}
