using Arch.Core;

using BattleCity.Shared.Network.Packets;

namespace BattleCity.Core.Ecs.Systems;

public readonly struct BombSimulationHooks
{
    public bool SuppressDetonation { get; init; }

    public Action<ServerExplosionPacket>? ReportExplosion { get; init; }

    public Action<ushort>? ReportItemRemoved { get; init; }

    public Action<Entity, int, int>? ReportHpChanged { get; init; }

    public Action<Entity, byte>? ReportNetworkPlayerKilled { get; init; }
}
