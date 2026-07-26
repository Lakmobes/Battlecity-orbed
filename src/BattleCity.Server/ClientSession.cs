using System.Net;
using System.Net.Sockets;

using BattleCity.Shared.Network;

namespace BattleCity.Server;

public sealed class ClientSession : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly PacketReceiveBuffer _receiveBuffer = new();

    public ClientSession(byte playerId, TcpClient client)
    {
        PlayerId = playerId;
        _client = client;
        _stream = client.GetStream();
    }

    public byte PlayerId { get; }

    public string DisplayName { get; set; } = "Guest";

    public string Town { get; set; } = string.Empty;

    public PlayerSessionState State { get; set; } = PlayerSessionState.Connected;

    public byte CityId { get; set; }

    public bool HasCityAssignment { get; set; }

    public bool IsGuest { get; set; } = true;

    public bool IsAdmin { get; set; }

    public bool IsMayor { get; set; }

    public string? RegisteredUsername { get; set; }

    public int Points { get; set; }

    public int Deaths { get; set; }

    public bool IsInGame => State == PlayerSessionState.InGame;

    public PacketReceiveBuffer ReceiveBuffer => _receiveBuffer;

    public void Send(byte messageId, ReadOnlySpan<byte> payload)
    {
        if (!_client.Connected)
        {
            return;
        }

        try
        {
            var packet = LegacyPacketCodec.Encode(messageId, payload);
            _stream.Write(packet);
        }
        catch (IOException)
        {
            // Peer disconnected mid-write; removal happens on the next Update.
        }
        catch (ObjectDisposedException)
        {
            // Stream already closed.
        }
    }

    public void SendServer(ServerMessageId messageId, ReadOnlySpan<byte> payload) =>
        Send((byte)messageId, payload);

    public int ReadAvailable()
    {
        if (!_stream.DataAvailable)
        {
            return 0;
        }

        var scratch = new byte[512];
        var read = _stream.Read(scratch, 0, scratch.Length);
        if (read > 0)
        {
            _receiveBuffer.Append(scratch.AsSpan(0, read));
        }

        return read;
    }

    public bool IsConnected => _client.Connected;

    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}

public enum PlayerSessionState
{
    Connected,
    Verified,
    LoggedIn,
    Meeting,
    Interview,
    InGame,
}
