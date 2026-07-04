using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Shared.Tests;

public class PointsUpdatePacketTests
{
    [Fact]
    public void ServerPointsUpdatePacket_RoundTrips()
    {
        var original = new ServerPointsUpdatePacket(3, 250, 12, 1, 2, 99);
        Span<byte> buffer = stackalloc byte[ServerPointsUpdatePacket.Size];
        original.Write(buffer);

        var parsed = ServerPointsUpdatePacket.Read(buffer);

        Assert.Equal(original.PlayerId, parsed.PlayerId);
        Assert.Equal(original.Points, parsed.Points);
        Assert.Equal(original.Deaths, parsed.Deaths);
        Assert.Equal(original.Orbs, parsed.Orbs);
        Assert.Equal(original.Assists, parsed.Assists);
        Assert.Equal(original.MonthlyPoints, parsed.MonthlyPoints);
    }
}
