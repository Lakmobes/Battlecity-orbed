using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Shared.Tests;

public class RankBoardPacketTests
{
    [Fact]
    public void RankBoardPacket_RoundTripsTopRowsAndSeason()
    {
        var payload = RankBoardPacket.Create(
            board: 2,
            seasonName: "Spring Cup",
            rows:
            [
                ("Ace", 1800),
                ("Bee", 40),
            ]);

        Assert.True(RankBoardPacket.TryRead(payload, out var board, out var season, out var rows));
        Assert.Equal(2, board);
        Assert.Equal("Spring Cup", season);
        Assert.Equal(2, rows.Length);
        Assert.Equal("Ace", rows[0].Name);
        Assert.Equal(1800, rows[0].Points);
        Assert.Equal("Bee", rows[1].Name);
        Assert.Equal(40, rows[1].Points);
    }

    [Fact]
    public void RankBoardPacket_RejectsShortPayload()
    {
        Assert.False(RankBoardPacket.TryRead([1, 1], out _, out _, out var rows));
        Assert.Empty(rows);
    }
}
