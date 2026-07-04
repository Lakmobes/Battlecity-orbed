using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Shared.Tests;

public class ChatPacketTests
{
    [Fact]
    public void ServerChatMessagePacket_RoundTripsSenderAndMessage()
    {
        Span<byte> buffer = stackalloc byte[32];
        var written = ServerChatMessagePacket.Write(buffer, 5, "hello team");

        var packet = ServerChatMessagePacket.Read(buffer[..written]);
        Assert.Equal((byte)5, packet.SenderId);
        Assert.Equal("hello team", packet.Message);
    }
}
