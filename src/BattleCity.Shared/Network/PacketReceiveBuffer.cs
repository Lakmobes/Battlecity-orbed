using BattleCity.Shared.Network;

namespace BattleCity.Shared.Network;

public sealed class PacketReceiveBuffer
{
    private readonly byte[] _buffer = new byte[4096];
    private int _length;

    public ReadOnlySpan<byte> Unprocessed => _buffer.AsSpan(0, _length);

    public void Append(ReadOnlySpan<byte> data)
    {
        if (_length + data.Length > _buffer.Length)
        {
            throw new InvalidOperationException("Network receive buffer overflow.");
        }

        data.CopyTo(_buffer.AsSpan(_length));
        _length += data.Length;
    }

    public bool TryRead(out LegacyPacket packet)
    {
        packet = default;
        if (!LegacyPacketCodec.TryReadPacket(Unprocessed, out packet, out var consumed))
        {
            return false;
        }

        if (consumed <= 0 || consumed > _length)
        {
            return false;
        }

        _length -= consumed;
        if (_length > 0)
        {
            _buffer.AsSpan(consumed, _length).CopyTo(_buffer);
        }

        return true;
    }

    public void Clear() => _length = 0;
}
