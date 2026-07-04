namespace BattleCity.Shared.Network.Packets;

public readonly struct ServerAddRemCityPacket
{
    public const byte NoMayor = byte.MaxValue;

    public ServerAddRemCityPacket(byte cityId, byte mayorPlayerId, byte playerCount)
    {
        CityId = cityId;
        MayorPlayerId = mayorPlayerId;
        PlayerCount = playerCount;
    }

    public byte CityId { get; }

    /// <summary>255 when the city needs a mayor.</summary>
    public byte MayorPlayerId { get; }

    public byte PlayerCount { get; }

    public bool NeedsMayor => MayorPlayerId == NoMayor;

    public static ServerAddRemCityPacket Read(ReadOnlySpan<byte> buffer) =>
        new(buffer[0], buffer[1], buffer.Length >= 3 ? buffer[2] : (byte)0);

    public void Write(Span<byte> buffer)
    {
        buffer[0] = CityId;
        buffer[1] = MayorPlayerId;
        if (buffer.Length >= 3)
        {
            buffer[2] = PlayerCount;
        }
    }
}

public readonly struct ServerMayorHirePacket
{
    public const int Size = 1;

    public ServerMayorHirePacket(byte applicantPlayerId) => ApplicantPlayerId = applicantPlayerId;

    public byte ApplicantPlayerId { get; }

    public static ServerMayorHirePacket Read(ReadOnlySpan<byte> buffer) => new(buffer[0]);

    public void Write(Span<byte> buffer) => buffer[0] = ApplicantPlayerId;
}
