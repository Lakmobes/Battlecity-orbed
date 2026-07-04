using System.Buffers.Binary;
using System.Text;

using BattleCity.Shared.Constants;

namespace BattleCity.Shared.Network.Packets;

public readonly struct ClientVersionPacket
{
    public const int Size = 60;

    public ClientVersionPacket(string uniqueId, string version)
    {
        UniqueId = uniqueId;
        Version = version;
    }

    public string UniqueId { get; }

    public string Version { get; }

    public static ClientVersionPacket CreateDefault(string uniqueId) =>
        new(uniqueId, NetworkConstants.LegacyVersion);

    public void Write(Span<byte> buffer)
    {
        WriteFixedAscii(buffer.Slice(0, 50), UniqueId);
        WriteFixedAscii(buffer.Slice(50, 10), Version);
    }

    private static void WriteFixedAscii(Span<byte> destination, string value)
    {
        destination.Clear();
        var bytes = Encoding.ASCII.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }
}

public readonly struct ClientNewAccountPacket
{
    public const int Size = 130;

    public ClientNewAccountPacket(
        string username,
        string password,
        string email,
        string fullName,
        string town,
        string state)
    {
        Username = username;
        Password = password;
        Email = email;
        FullName = fullName;
        Town = town;
        State = state;
    }

    public string Username { get; }

    public string Password { get; }

    public string Email { get; }

    public string FullName { get; }

    public string Town { get; }

    public string State { get; }

    public static ClientNewAccountPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            ReadFixedAscii(buffer.Slice(0, 15)),
            ReadFixedAscii(buffer.Slice(15, 15)),
            ReadFixedAscii(buffer.Slice(30, 50)),
            ReadFixedAscii(buffer.Slice(80, 20)),
            ReadFixedAscii(buffer.Slice(100, 15)),
            ReadFixedAscii(buffer.Slice(115, 15)));

    public void Write(Span<byte> buffer)
    {
        WriteFixedAscii(buffer.Slice(0, 15), Username);
        WriteFixedAscii(buffer.Slice(15, 15), Password);
        WriteFixedAscii(buffer.Slice(30, 50), Email);
        WriteFixedAscii(buffer.Slice(80, 20), FullName);
        WriteFixedAscii(buffer.Slice(100, 15), Town);
        WriteFixedAscii(buffer.Slice(115, 15), State);
    }

    private static void WriteFixedAscii(Span<byte> destination, string value)
    {
        destination.Clear();
        var bytes = Encoding.ASCII.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }

    private static string ReadFixedAscii(ReadOnlySpan<byte> buffer)
    {
        var length = buffer.IndexOf((byte)0);
        if (length < 0)
        {
            length = buffer.Length;
        }

        return Encoding.ASCII.GetString(buffer.Slice(0, length));
    }
}

public readonly struct ClientDeathPacket
{
    public const int Size = 1;

    public ClientDeathPacket(byte killerCity)
    {
        KillerCity = killerCity;
    }

    public byte KillerCity { get; }

    public static ClientDeathPacket Read(ReadOnlySpan<byte> buffer) => new(buffer[0]);

    public void Write(Span<byte> buffer) => buffer[0] = KillerCity;
}

public readonly struct ServerDeathPacket
{
    public const int Size = 3;

    public ServerDeathPacket(byte playerId, byte deathType, byte killerCity)
    {
        PlayerId = playerId;
        DeathType = deathType;
        KillerCity = killerCity;
    }

    public byte PlayerId { get; }

    public byte DeathType { get; }

    public byte KillerCity { get; }

    public static ServerDeathPacket Read(ReadOnlySpan<byte> buffer) =>
        new(buffer[0], buffer[1], buffer[2]);

    public void Write(Span<byte> buffer)
    {
        buffer[0] = PlayerId;
        buffer[1] = DeathType;
        buffer[2] = KillerCity;
    }
}

public static class ChatPacketLimits
{
    public const int MaxMessageLength = 100;
}

public readonly struct ServerChatMessagePacket
{
    public ServerChatMessagePacket(byte senderId, string message)
    {
        SenderId = senderId;
        Message = message;
    }

    public byte SenderId { get; }

    public string Message { get; }

    public static ServerChatMessagePacket Read(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return new ServerChatMessagePacket(0, string.Empty);
        }

        var senderId = buffer[0];
        var message = buffer.Length > 1
            ? ReadAsciiMessage(buffer.Slice(1))
            : string.Empty;
        return new ServerChatMessagePacket(senderId, message);
    }

    public static int Write(Span<byte> buffer, byte senderId, string message)
    {
        buffer[0] = senderId;
        var messageBytes = Encoding.ASCII.GetBytes(message);
        var copyLength = Math.Min(messageBytes.Length, buffer.Length - 1);
        messageBytes.AsSpan(0, copyLength).CopyTo(buffer.Slice(1));
        return 1 + copyLength;
    }

    private static string ReadAsciiMessage(ReadOnlySpan<byte> buffer)
    {
        var length = buffer.IndexOf((byte)0);
        if (length < 0)
        {
            length = buffer.Length;
        }

        return Encoding.ASCII.GetString(buffer.Slice(0, length));
    }
}

public readonly struct ClientWhisperPacket
{
    public const int MessageOffset = 2;
    public const int MaxMessageLength = 128;
    public const int Size = MessageOffset + MaxMessageLength;

    public ClientWhisperPacket(byte recipientId, string message)
    {
        RecipientId = recipientId;
        Message = message;
    }

    public byte RecipientId { get; }

    public string Message { get; }

    public static ClientWhisperPacket Read(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 2)
        {
            return new ClientWhisperPacket(0, string.Empty);
        }

        var message = buffer.Length > MessageOffset
            ? ReadAsciiMessage(buffer.Slice(MessageOffset))
            : string.Empty;
        return new ClientWhisperPacket(buffer[1], message);
    }

    public void Write(Span<byte> buffer)
    {
        buffer.Clear();
        buffer[1] = RecipientId;
        var messageBytes = Encoding.ASCII.GetBytes(Message);
        var copyLength = Math.Min(messageBytes.Length, MaxMessageLength - 1);
        messageBytes.AsSpan(0, copyLength).CopyTo(buffer.Slice(MessageOffset));
    }

    private static string ReadAsciiMessage(ReadOnlySpan<byte> buffer)
    {
        var length = buffer.IndexOf((byte)0);
        if (length < 0)
        {
            length = Math.Min(buffer.Length, MaxMessageLength - 1);
        }

        length = Math.Min(length, MaxMessageLength - 1);
        return Encoding.ASCII.GetString(buffer.Slice(0, length));
    }
}

public readonly struct ServerHpPacket
{
    public const int Size = 2;

    public ServerHpPacket(byte playerId, byte health)
    {
        PlayerId = playerId;
        Health = health;
    }

    public byte PlayerId { get; }

    public byte Health { get; }

    public static ServerHpPacket Read(ReadOnlySpan<byte> buffer) =>
        new(buffer[0], buffer[1]);

    public void Write(Span<byte> buffer)
    {
        buffer[0] = PlayerId;
        buffer[1] = Health;
    }
}

public readonly struct ServerCloakPacket
{
    public const int Size = 1;

    public ServerCloakPacket(byte playerId) => PlayerId = playerId;

    public byte PlayerId { get; }

    public static ServerCloakPacket Read(ReadOnlySpan<byte> buffer) => new(buffer[0]);

    public void Write(Span<byte> buffer) => buffer[0] = PlayerId;
}

public readonly struct ServerPointsUpdatePacket
{
    public const int Size = 21;

    public ServerPointsUpdatePacket(
        byte playerId,
        uint points,
        uint deaths,
        uint orbs = 0,
        uint assists = 0,
        uint monthlyPoints = 0)
    {
        PlayerId = playerId;
        Points = points;
        Deaths = deaths;
        Orbs = orbs;
        Assists = assists;
        MonthlyPoints = monthlyPoints;
    }

    public byte PlayerId { get; }

    public uint Points { get; }

    public uint Deaths { get; }

    public uint Orbs { get; }

    public uint Assists { get; }

    public uint MonthlyPoints { get; }

    public static ServerPointsUpdatePacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            buffer[0],
            ReadUInt32(buffer, 1),
            ReadUInt32(buffer, 5),
            ReadUInt32(buffer, 9),
            ReadUInt32(buffer, 13),
            ReadUInt32(buffer, 17));

    public void Write(Span<byte> buffer)
    {
        buffer[0] = PlayerId;
        WriteUInt32(buffer, 1, Points);
        WriteUInt32(buffer, 5, Deaths);
        WriteUInt32(buffer, 9, Orbs);
        WriteUInt32(buffer, 13, Assists);
        WriteUInt32(buffer, 17, MonthlyPoints);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> buffer, int offset) =>
        (uint)(buffer[offset]
            | (buffer[offset + 1] << 8)
            | (buffer[offset + 2] << 16)
            | (buffer[offset + 3] << 24));

    private static void WriteUInt32(Span<byte> buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }
}

public readonly struct ClientLoginPacket
{
    public const int Size = 30;

    public ClientLoginPacket(string username, string password)
    {
        Username = username;
        Password = password;
    }

    public string Username { get; }

    public string Password { get; }

    public void Write(Span<byte> buffer)
    {
        WriteFixedAscii(buffer.Slice(0, 15), Username);
        WriteFixedAscii(buffer.Slice(15, 15), Password);
    }

    public static ClientLoginPacket Read(ReadOnlySpan<byte> buffer) =>
        new(ReadFixedAscii(buffer.Slice(0, 15)), ReadFixedAscii(buffer.Slice(15, 15)));

    private static string ReadFixedAscii(ReadOnlySpan<byte> buffer)
    {
        var length = buffer.IndexOf((byte)0);
        if (length < 0)
        {
            length = buffer.Length;
        }

        return Encoding.ASCII.GetString(buffer.Slice(0, length));
    }

    private static void WriteFixedAscii(Span<byte> destination, string value)
    {
        destination.Clear();
        var bytes = Encoding.ASCII.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }
}

public readonly struct ClientUpdatePacket
{
    public const int Size = 7;

    public ClientUpdatePacket(ushort x, ushort y, int turn, int move, byte direction)
    {
        X = x;
        Y = y;
        Turn = EncodeAxisInput(turn);
        Move = EncodeAxisInput(move);
        Direction = direction;
    }

    public ushort X { get; }

    public ushort Y { get; }

    public byte Turn { get; }

    public byte Move { get; }

    public byte Direction { get; }

    public int TurnInput => DecodeAxisInput(Turn);

    public int MoveInput => DecodeAxisInput(Move);

    public static ClientUpdatePacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(2)),
            buffer[4],
            buffer[5],
            buffer[6]);

    public void Write(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, X);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2), Y);
        buffer[4] = Turn;
        buffer[5] = Move;
        buffer[6] = Direction;
    }

    public static byte EncodeAxisInput(int value) => (byte)(value + 1);

    public static int DecodeAxisInput(byte encoded) => encoded - 1;
}

public readonly struct ServerUpdatePacket
{
    public const int Size = 8;

    public ServerUpdatePacket(byte playerId, ushort x, ushort y, byte turn, byte move, byte direction)
    {
        PlayerId = playerId;
        X = x;
        Y = y;
        Turn = turn;
        Move = move;
        Direction = direction;
    }

    public byte PlayerId { get; }

    public ushort X { get; }

    public ushort Y { get; }

    public byte Turn { get; }

    public byte Move { get; }

    public byte Direction { get; }

    public int TurnInput => ClientUpdatePacket.DecodeAxisInput(Turn);

    public int MoveInput => ClientUpdatePacket.DecodeAxisInput(Move);

    public static ServerUpdatePacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            buffer[0],
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(1)),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(3)),
            buffer[5],
            buffer[6],
            buffer[7]);

    public void Write(Span<byte> buffer)
    {
        buffer[0] = PlayerId;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1), X);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(3), Y);
        buffer[5] = Turn;
        buffer[6] = Move;
        buffer[7] = Direction;
    }
}

public readonly struct ServerJoinDataPacket
{
    public const int Size = 3;

    public ServerJoinDataPacket(byte playerId, byte mayor, byte city)
    {
        PlayerId = playerId;
        Mayor = mayor;
        City = city;
    }

    public byte PlayerId { get; }

    public byte Mayor { get; }

    public byte City { get; }

    public static ServerJoinDataPacket Read(ReadOnlySpan<byte> buffer) =>
        new(buffer[0], buffer[1], buffer[2]);

    public void Write(Span<byte> buffer)
    {
        buffer[0] = PlayerId;
        buffer[1] = Mayor;
        buffer[2] = City;
    }
}

public readonly struct ServerMayorUpdatePacket
{
    public const int Size = 2;

    public ServerMayorUpdatePacket(byte playerId, bool isMayor)
    {
        PlayerId = playerId;
        IsMayor = isMayor;
    }

    public byte PlayerId { get; }

    public bool IsMayor { get; }

    public static ServerMayorUpdatePacket Read(ReadOnlySpan<byte> buffer) =>
        new(buffer[0], buffer[1] != 0);

    public void Write(Span<byte> buffer)
    {
        buffer[0] = PlayerId;
        buffer[1] = (byte)(IsMayor ? 1 : 0);
    }
}

public readonly struct ServerStateGamePacket
{
    public const int Size = 5;

    public ServerStateGamePacket(ushort x, ushort y, byte city)
    {
        X = x;
        Y = y;
        City = city;
    }

    public ushort X { get; }

    public ushort Y { get; }

    public byte City { get; }

    public static ServerStateGamePacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(2)),
            buffer[4]);

    public void Write(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, X);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2), Y);
        buffer[4] = City;
    }
}

public readonly struct ClientItemDropPacket
{
    public const int Size = 2;

    public ClientItemDropPacket(byte itemType, byte active)
    {
        ItemType = itemType;
        Active = active;
    }

    public byte ItemType { get; }

    public byte Active { get; }

    public static ClientItemDropPacket Read(ReadOnlySpan<byte> buffer) =>
        new(buffer[0], buffer[1]);

    public void Write(Span<byte> buffer)
    {
        buffer[0] = ItemType;
        buffer[1] = Active;
    }
}

public readonly struct ServerAddItemPacket
{
    public const int Size = 9;

    public ServerAddItemPacket(ushort x, ushort y, byte city, byte type, byte active, ushort id)
    {
        X = x;
        Y = y;
        City = city;
        Type = type;
        Active = active;
        Id = id;
    }

    public ushort X { get; }

    public ushort Y { get; }

    public byte City { get; }

    public byte Type { get; }

    public byte Active { get; }

    public ushort Id { get; }

    public static ServerAddItemPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(2)),
            buffer[4],
            buffer[5],
            buffer[6],
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(7)));

    public void Write(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, X);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2), Y);
        buffer[4] = City;
        buffer[5] = Type;
        buffer[6] = Active;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(7), Id);
    }
}

public readonly struct ClientShotPacket
{
    public const int Size = 6;

    public ClientShotPacket(ushort x, ushort y, byte direction, byte type)
    {
        X = x;
        Y = y;
        Direction = direction;
        Type = type;
    }

    public ushort X { get; }

    public ushort Y { get; }

    public byte Direction { get; }

    public byte Type { get; }

    public static ClientShotPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(2)),
            buffer[4],
            buffer[5]);

    public void Write(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, X);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2), Y);
        buffer[4] = Direction;
        buffer[5] = Type;
    }
}

public readonly struct ServerShotPacket
{
    public const int Size = 8;

    public ServerShotPacket(ushort playerId, ushort x, ushort y, byte direction, byte type)
    {
        PlayerId = playerId;
        X = x;
        Y = y;
        Direction = direction;
        Type = type;
    }

    public ushort PlayerId { get; }

    public ushort X { get; }

    public ushort Y { get; }

    public byte Direction { get; }

    public byte Type { get; }

    public static ServerShotPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(2)),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(4)),
            buffer[6],
            buffer[7]);

    public void Write(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, PlayerId);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2), X);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(4), Y);
        buffer[6] = Direction;
        buffer[7] = Type;
    }
}

public readonly struct ClientItemPickupPacket
{
    public const int Size = 4;

    public ClientItemPickupPacket(ushort itemId, byte active, byte itemType)
    {
        ItemId = itemId;
        Active = active;
        ItemType = itemType;
    }

    public ushort ItemId { get; }

    public byte Active { get; }

    public byte ItemType { get; }

    public static ClientItemPickupPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            buffer[2],
            buffer[3]);

    public void Write(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, ItemId);
        buffer[2] = Active;
        buffer[3] = ItemType;
    }
}

public readonly struct ServerRemoveItemPacket
{
    public const int Size = 2;

    public ServerRemoveItemPacket(ushort itemId)
    {
        ItemId = itemId;
    }

    public ushort ItemId { get; }

    public static ServerRemoveItemPacket Read(ReadOnlySpan<byte> buffer) =>
        new(BinaryPrimitives.ReadUInt16LittleEndian(buffer));

    public void Write(Span<byte> buffer) =>
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, ItemId);
}

public readonly struct ServerPickedUpPacket
{
    public const int Size = 4;

    public ServerPickedUpPacket(ushort itemId, byte active, byte itemType)
    {
        ItemId = itemId;
        Active = active;
        ItemType = itemType;
    }

    public ushort ItemId { get; }

    public byte Active { get; }

    public byte ItemType { get; }

    public static ServerPickedUpPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            buffer[2],
            buffer[3]);

    public void Write(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, ItemId);
        buffer[2] = Active;
        buffer[3] = ItemType;
    }
}

public readonly struct ClientBuildPacket
{
    public const int Size = 6;

    public ClientBuildPacket(ushort x, ushort y, byte buildSlot, bool isAutoBuild)
    {
        X = x;
        Y = y;
        BuildSlot = buildSlot;
        IsAutoBuild = isAutoBuild;
    }

    public ushort X { get; }

    public ushort Y { get; }

    public byte BuildSlot { get; }

    public bool IsAutoBuild { get; }

    public static ClientBuildPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(2)),
            buffer[4],
            buffer[5] != 0);

    public void Write(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, X);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2), Y);
        buffer[4] = BuildSlot;
        buffer[5] = (byte)(IsAutoBuild ? 1 : 0);
    }
}

public readonly struct ClientDemolishPacket
{
    public const int Size = 2;

    public ClientDemolishPacket(ushort buildingId)
    {
        BuildingId = buildingId;
    }

    public ushort BuildingId { get; }

    public static ClientDemolishPacket Read(ReadOnlySpan<byte> buffer) =>
        new(BinaryPrimitives.ReadUInt16LittleEndian(buffer));

    public void Write(Span<byte> buffer) =>
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, BuildingId);
}

public readonly struct ServerBuildingPacket
{
    public const int Size = 10;

    public ServerBuildingPacket(
        byte city,
        ushort x,
        ushort y,
        byte buildSlot,
        byte count,
        ushort id,
        byte population)
    {
        City = city;
        X = x;
        Y = y;
        BuildSlot = buildSlot;
        Count = count;
        Id = id;
        Population = population;
    }

    public byte City { get; }

    public ushort X { get; }

    public ushort Y { get; }

    public byte BuildSlot { get; }

    public byte Count { get; }

    public ushort Id { get; }

    public byte Population { get; }

    public static ServerBuildingPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            buffer[0],
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(1)),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(3)),
            buffer[5],
            buffer[6],
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(7)),
            buffer[9]);

    public void Write(Span<byte> buffer)
    {
        buffer[0] = City;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1), X);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(3), Y);
        buffer[5] = BuildSlot;
        buffer[6] = Count;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(7), Id);
        buffer[9] = Population;
    }
}

public readonly struct ServerOrbedCityPacket
{
    public const int Size = 10;

    public ServerOrbedCityPacket(byte victimCity, byte orberCity, uint points, uint orberCityPoints)
    {
        VictimCity = victimCity;
        OrberCity = orberCity;
        Points = points;
        OrberCityPoints = orberCityPoints;
    }

    public byte VictimCity { get; }

    public byte OrberCity { get; }

    public uint Points { get; }

    public uint OrberCityPoints { get; }

    public static ServerOrbedCityPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            buffer[0],
            buffer[1],
            BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(2)),
            BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(6)));

    public void Write(Span<byte> buffer)
    {
        buffer[0] = VictimCity;
        buffer[1] = OrberCity;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(2), Points);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(6), OrberCityPoints);
    }
}

public readonly struct ServerPlayerDataPacket
{
    public const int Size = 40;

    public ServerPlayerDataPacket(
        byte index,
        string name,
        string town,
        byte playerType,
        byte red,
        byte green,
        byte blue,
        byte member,
        byte tank)
    {
        Index = index;
        Name = name;
        Town = town;
        PlayerType = playerType;
        Red = red;
        Green = green;
        Blue = blue;
        Member = member;
        Tank = tank;
    }

    public byte Index { get; }

    public string Name { get; }

    public string Town { get; }

    public byte PlayerType { get; }

    public byte Red { get; }

    public byte Green { get; }

    public byte Blue { get; }

    public byte Member { get; }

    public byte Tank { get; }

    public static ServerPlayerDataPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            (byte)buffer[0],
            ReadFixedAscii(buffer.Slice(1, 16)),
            ReadFixedAscii(buffer.Slice(17, 16)),
            buffer[33],
            buffer[34],
            buffer[35],
            buffer[36],
            buffer[37],
            buffer[38]);

    private static string ReadFixedAscii(ReadOnlySpan<byte> buffer)
    {
        var length = buffer.IndexOf((byte)0);
        if (length < 0)
        {
            length = buffer.Length;
        }

        return Encoding.ASCII.GetString(buffer.Slice(0, length));
    }
}

/// <summary>Legacy smCanBuild — menu slot unlock/buildable state for the mayor.</summary>
public readonly struct ServerCanBuildPacket
{
    public const int Size = 3;

    public ServerCanBuildPacket(byte buildSlot, byte legacyCanBuild, byte canBuildState)
    {
        BuildSlot = buildSlot;
        LegacyCanBuild = legacyCanBuild;
        CanBuildState = canBuildState;
    }

    /// <summary>1-based build menu slot (legacy packet[0]).</summary>
    public byte BuildSlot { get; }

    /// <summary>Legacy iCan flag: 1 = buildable, 0 = hidden.</summary>
    public byte LegacyCanBuild { get; }

    /// <summary>Full canBuild value (0 locked, 1 buildable, 2 already placed).</summary>
    public byte CanBuildState { get; }

    public static ServerCanBuildPacket FromMenuIndex(int menuIndex, int canBuildState) =>
        new(
            (byte)(menuIndex + 1),
            (byte)(canBuildState == 1 ? 1 : 0),
            (byte)Math.Clamp(canBuildState, 0, 2));

    public static ServerCanBuildPacket Read(ReadOnlySpan<byte> buffer) =>
        new(buffer[0], buffer[1], buffer.Length >= 3 ? buffer[2] : buffer[1]);

    public void Write(Span<byte> buffer)
    {
        buffer[0] = BuildSlot;
        buffer[1] = LegacyCanBuild;
        buffer[2] = CanBuildState;
    }
}

/// <summary>Legacy smUpdatePop / sSMPop — building population sync.</summary>
public readonly struct ServerUpdatePopPacket
{
    public const int Size = 3;

    public ServerUpdatePopPacket(ushort buildingId, byte population)
    {
        BuildingId = buildingId;
        Population = population;
    }

    public ushort BuildingId { get; }

    public byte Population { get; }

    public static ServerUpdatePopPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            buffer[2]);

    public void Write(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, BuildingId);
        buffer[2] = Population;
    }
}

/// <summary>Legacy smRespawn — player id only (1 byte payload).</summary>
public readonly struct ServerRespawnPacket
{
    public const int Size = 1;

    public ServerRespawnPacket(byte playerId) => PlayerId = playerId;

    public byte PlayerId { get; }

    public static ServerRespawnPacket Read(ReadOnlySpan<byte> buffer) => new(buffer[0]);

    public void Write(Span<byte> buffer) => buffer[0] = PlayerId;
}

/// <summary>Legacy smExplosion / sSMExplode — large bomb blast at grid anchor (grid + 1).</summary>
public readonly struct ServerExplosionPacket
{
    public const int Size = 5;

    public ServerExplosionPacket(byte cityId, ushort gridX, ushort gridY)
    {
        CityId = cityId;
        GridX = gridX;
        GridY = gridY;
    }

    public byte CityId { get; }

    /// <summary>Legacy blast anchor X (placed grid X + 1).</summary>
    public ushort GridX { get; }

    /// <summary>Legacy blast anchor Y (placed grid Y + 1).</summary>
    public ushort GridY { get; }

    public static ServerExplosionPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            buffer[0],
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(1)),
            BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(3)));

    public void Write(Span<byte> buffer)
    {
        buffer[0] = CityId;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1), GridX);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(3), GridY);
    }
}

/// <summary>Legacy smItemCount / sSMItemCount — factory items remaining in current batch.</summary>
public readonly struct ServerItemCountPacket
{
    public const int Size = 3;

    public ServerItemCountPacket(ushort buildingId, byte itemCount)
    {
        BuildingId = buildingId;
        ItemCount = itemCount;
    }

    public ushort BuildingId { get; }

    public byte ItemCount { get; }

    public static ServerItemCountPacket Read(ReadOnlySpan<byte> buffer) =>
        new(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            buffer[2]);

    public void Write(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, BuildingId);
        buffer[2] = ItemCount;
    }
}
