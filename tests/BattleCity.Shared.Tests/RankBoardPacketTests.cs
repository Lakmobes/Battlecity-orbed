using BattleCity.Shared.Network;
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
            startIndex: 0,
            seasonName: "Spring Cup",
            rows:
            [
                ("Ace", 1800),
                ("Bee", 40),
            ]);

        Assert.True(RankBoardPacket.TryRead(payload, out var board, out var start, out var season, out var rows));
        Assert.Equal(2, board);
        Assert.Equal(0, start);
        Assert.Equal("Spring Cup", season);
        Assert.Equal(2, rows.Length);
        Assert.Equal("Ace", rows[0].Name);
        Assert.Equal(1800, rows[0].Points);
        Assert.Equal("Bee", rows[1].Name);
        Assert.Equal(40, rows[1].Points);
    }

    [Fact]
    public void RankBoardPacket_SplitsTwentyRowsIntoTwoFrames()
    {
        var rows = new (string Name, int Points)[20];
        for (var i = 0; i < rows.Length; i++)
        {
            rows[i] = ($"Player{i + 1}", 1000 - i);
        }

        var chunks = RankBoardPacket.CreateChunks(0, "Season", rows);
        Assert.Equal(2, chunks.Count);

        var combined = new List<(string Name, int Points)>();
        byte? previousStart = null;
        foreach (var chunk in chunks)
        {
            var framed = LegacyPacketCodec.EncodeServer(ServerMessageId.RankBoard, chunk);
            Assert.True(LegacyPacketCodec.TryReadPacket(framed, out var packet, out _));
            Assert.True(RankBoardPacket.TryRead(
                packet.Payload.Span,
                out var board,
                out var start,
                out var season,
                out var piece));
            Assert.Equal(0, board);
            Assert.Equal("Season", season);
            Assert.Equal(10, piece.Length);
            if (previousStart is null)
            {
                Assert.Equal(0, start);
            }
            else
            {
                Assert.Equal(10, start);
            }

            previousStart = start;
            combined.AddRange(piece);
        }

        Assert.Equal(20, combined.Count);
        Assert.Equal("Player1", combined[0].Name);
        Assert.Equal("Player20", combined[19].Name);
        Assert.Equal(981, combined[19].Points);
    }

    [Fact]
    public void RankBoardPacket_RejectsShortPayload()
    {
        Assert.False(RankBoardPacket.TryRead([1, 1], out _, out _, out _, out var rows));
        Assert.Empty(rows);
    }
}
