namespace BattleCity.Shared.Network;

public readonly struct LegacyPacket
{
    public LegacyPacket(byte messageId, ReadOnlyMemory<byte> payload)
    {
        MessageId = messageId;
        Payload = payload;
    }

    public byte MessageId { get; }

    public ReadOnlyMemory<byte> Payload { get; }
}
