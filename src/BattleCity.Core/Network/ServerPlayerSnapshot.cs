using BattleCity.Shared.Network.Packets;

namespace BattleCity.Core.Network;

public readonly struct ServerPlayerSnapshot
{
    public ServerPlayerSnapshot(byte playerId, ushort x, ushort y, byte turn, byte move, byte direction)
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

    public ServerUpdatePacket ToPacket() =>
        new(PlayerId, X, Y, Turn, Move, Direction);
}
