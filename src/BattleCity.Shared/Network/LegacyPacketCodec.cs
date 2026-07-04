namespace BattleCity.Shared.Network;

/// <summary>Legacy TCP framing from legacy/client/CWinsock.cpp and legacy/server/CSocket.cpp.</summary>
public static class LegacyPacketCodec
{
    public const int ChecksumSeed = 3412;
    public const int ChecksumMod = 71;
    public const int MaxPacketLength = 255;

    public static byte[] Encode(byte messageId, ReadOnlySpan<byte> payload)
    {
        var bodyLength = payload.Length + 2;
        var packet = new byte[bodyLength + 1];
        packet[0] = (byte)bodyLength;
        packet[2] = messageId;
        payload.CopyTo(packet.AsSpan(3));
        packet[1] = ComputePayloadChecksum(payload);
        return packet;
    }

    public static byte[] EncodeClient(ClientMessageId messageId, ReadOnlySpan<byte> payload) =>
        Encode((byte)messageId, payload);

    public static byte[] EncodeServer(ServerMessageId messageId, ReadOnlySpan<byte> payload) =>
        Encode((byte)messageId, payload);

    public static bool TryReadPacket(ReadOnlySpan<byte> buffer, out LegacyPacket packet, out int bytesConsumed)
    {
        packet = default;
        bytesConsumed = 0;

        if (buffer.Length < 3)
        {
            return false;
        }

        var bodyLength = buffer[0];
        if (bodyLength < 2 || bodyLength > MaxPacketLength)
        {
            bytesConsumed = 1;
            return false;
        }

        var totalLength = bodyLength + 1;
        if (buffer.Length < totalLength)
        {
            return false;
        }

        var payloadLength = bodyLength - 2;
        var expectedChecksum = buffer[1];
        var actualChecksum = ComputePayloadChecksum(buffer.Slice(3, payloadLength));
        if (expectedChecksum != actualChecksum)
        {
            bytesConsumed = totalLength;
            return false;
        }

        var messageId = buffer[2];
        packet = new LegacyPacket(messageId, buffer.Slice(3, payloadLength).ToArray());
        bytesConsumed = totalLength;
        return true;
    }

    public static byte ComputePayloadChecksum(ReadOnlySpan<byte> payload)
    {
        var checksum = ChecksumSeed;
        foreach (var value in payload)
        {
            checksum += value;
        }

        return (byte)(checksum % ChecksumMod);
    }
}
