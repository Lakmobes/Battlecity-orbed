using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Shared.Tests;

public class ItemCountPacketTests
{
    [Fact]
    public void ServerItemCountPacket_RoundTrips()
    {
        var original = new ServerItemCountPacket(1234, 7);
        Span<byte> buffer = stackalloc byte[ServerItemCountPacket.Size];
        original.Write(buffer);

        var parsed = ServerItemCountPacket.Read(buffer);

        Assert.Equal(original.BuildingId, parsed.BuildingId);
        Assert.Equal(original.ItemCount, parsed.ItemCount);
    }

    [Fact]
    public void ServerItemCount_UsesLegacyMessageId()
    {
        Assert.Equal(22, (int)ServerMessageId.ItemCount);
    }
}
