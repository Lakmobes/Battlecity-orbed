using System.Buffers.Binary;

namespace BattleCity.Shared.Network.Packets;

public readonly struct ServerClickPlayerPacket
{
    public const int Size = 15;

    public ServerClickPlayerPacket(
        byte playerId,
        int points,
        int monthlyPoints,
        int orbs,
        int assists,
        int deaths)
    {
        PlayerId = playerId;
        Points = points;
        MonthlyPoints = monthlyPoints;
        Orbs = (ushort)Math.Clamp(orbs, 0, ushort.MaxValue);
        Assists = (ushort)Math.Clamp(assists, 0, ushort.MaxValue);
        Deaths = (ushort)Math.Clamp(deaths, 0, ushort.MaxValue);
    }

    public byte PlayerId { get; }

    public int Points { get; }

    public int MonthlyPoints { get; }

    public ushort Orbs { get; }

    public ushort Assists { get; }

    public ushort Deaths { get; }

    public static ServerClickPlayerPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            buffer[0],
            BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(1, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(5, 4)),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(9, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(11, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(13, 2)));

    public void Write(Span<byte> buffer)
    {
        buffer[0] = PlayerId;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(1, 4), Points);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(5, 4), MonthlyPoints);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(9, 2), Orbs);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(11, 2), Assists);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(13, 2), Deaths);
    }
}

public readonly struct ServerRightClickCityPacket
{
    public const int Size = 10;

    public ServerRightClickCityPacket(
        byte cityId,
        int buildingCount,
        bool isOrbable,
        int orbs,
        int orbPoints,
        int uptimeMinutes)
    {
        CityId = cityId;
        BuildingCount = (ushort)Math.Clamp(buildingCount, 0, ushort.MaxValue);
        IsOrbable = isOrbable;
        Orbs = (ushort)Math.Clamp(orbs, 0, ushort.MaxValue);
        OrbPoints = (ushort)Math.Clamp(orbPoints, 0, ushort.MaxValue);
        UptimeMinutes = (ushort)Math.Clamp(uptimeMinutes, 0, ushort.MaxValue);
    }

    public byte CityId { get; }

    public ushort BuildingCount { get; }

    public bool IsOrbable { get; }

    public ushort Orbs { get; }

    public ushort OrbPoints { get; }

    public ushort UptimeMinutes { get; }

    public static ServerRightClickCityPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            buffer[0],
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(1, 2)),
            buffer[3] != 0,
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(4, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(6, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(8, 2)));

    public void Write(Span<byte> buffer)
    {
        buffer[0] = CityId;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1, 2), BuildingCount);
        buffer[3] = (byte)(IsOrbable ? 1 : 0);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(4, 2), Orbs);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(6, 2), OrbPoints);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(8, 2), UptimeMinutes);
    }
}
