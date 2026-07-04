using System.Net.Sockets;

using BattleCity.Server;

using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Core.Tests;

public class GameServerNetworkTests : IDisposable
{
    private readonly GameServer _server = new();

    public GameServerNetworkTests()
    {
        _server.Start("127.0.0.1", 0);
    }

    public void Dispose() => _server.Dispose();

    [Fact]
    public void GuestClientCanCompleteLegacyLoginHandshake()
    {
        using var tcpClient = new TcpClient();
        tcpClient.Connect("127.0.0.1", _server.Port);
        using var stream = tcpClient.GetStream();
        var receiveBuffer = new PacketReceiveBuffer();

        Span<byte> versionPayload = stackalloc byte[ClientVersionPacket.Size];
        ClientVersionPacket.CreateDefault("test-client").Write(versionPayload);
        stream.Write(LegacyPacketCodec.EncodeClient(ClientMessageId.Version, versionPayload));

        Span<byte> loginPayload = stackalloc byte[ClientLoginPacket.Size];
        new ClientLoginPacket("Tester", "guest").Write(loginPayload);
        stream.Write(LegacyPacketCodec.EncodeClient(ClientMessageId.Login, loginPayload));

        PumpServer();
        var loginCorrect = WaitForServerPacket(
            receiveBuffer,
            stream,
            packet => packet.MessageId == (byte)ServerMessageId.LoginCorrect);
        Assert.Equal((byte)ServerMessageId.LoginCorrect, loginCorrect.MessageId);

        stream.Write(LegacyPacketCodec.EncodeClient(ClientMessageId.NextStep, "A"u8));
        PumpServer();

        var stateGame = WaitForServerPacket(
            receiveBuffer,
            stream,
            packet => packet.MessageId == (byte)ServerMessageId.StateGame);
        Assert.Equal((byte)ServerMessageId.StateGame, stateGame.MessageId);
        Assert.True(stateGame.Payload.Length >= ServerStateGamePacket.Size);
    }

    private void PumpServer()
    {
        for (var i = 0; i < 5; i++)
        {
            _server.Update(1f / 60f);
            Thread.Sleep(10);
        }
    }

    private static LegacyPacket WaitForServerPacket(
        PacketReceiveBuffer receiveBuffer,
        NetworkStream stream,
        Func<LegacyPacket, bool> predicate)
    {
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            if (stream.DataAvailable)
            {
                var scratch = new byte[256];
                var read = stream.Read(scratch, 0, scratch.Length);
                receiveBuffer.Append(scratch.AsSpan(0, read));
            }

            while (receiveBuffer.TryRead(out var packet))
            {
                if (predicate(packet))
                {
                    return packet;
                }
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("Timed out waiting for server packet.");
    }
}
