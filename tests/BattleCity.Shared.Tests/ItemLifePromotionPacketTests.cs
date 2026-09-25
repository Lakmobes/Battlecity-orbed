using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Shared.Tests;

public class ItemLifePromotionPacketTests
{
    [Fact]
    public void ServerItemLifePacket_RoundTrips()
    {
        var original = new ServerItemLifePacket(itemId: 77, life: 1);
        Span<byte> buffer = stackalloc byte[ServerItemLifePacket.Size];
        original.Write(buffer);

        var parsed = ServerItemLifePacket.Read(buffer);

        Assert.Equal(original.ItemId, parsed.ItemId);
        Assert.Equal(original.Life, parsed.Life);
    }

    [Fact]
    public void ServerPromotionPacket_RoundTrips()
    {
        var original = new ServerPromotionPacket(playerId: 3, rank: "Captain");
        Span<byte> buffer = stackalloc byte[original.GetWriteLength()];
        original.Write(buffer);

        var parsed = ServerPromotionPacket.Read(buffer);

        Assert.Equal(original.PlayerId, parsed.PlayerId);
        Assert.Equal(original.Rank, parsed.Rank);
    }

    [Fact]
    public void LegacyMessageIds_MatchNetMessages()
    {
        Assert.Equal(41, (int)ServerMessageId.ItemLife);
        Assert.Equal(43, (int)ServerMessageId.Promotion);
    }
}
