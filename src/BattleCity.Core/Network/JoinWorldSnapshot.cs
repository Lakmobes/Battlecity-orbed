using BattleCity.Shared.Network.Packets;

namespace BattleCity.Core.Network;

/// <summary>Authoritative world state sent to a player on join (legacy join burst).</summary>
public sealed class JoinWorldSnapshot
{
    public List<ServerAddItemPacket> Items { get; } = [];

    public List<ServerBuildingPacket> Buildings { get; } = [];

    public List<ServerBuildingPacket> RemovedBuildings { get; } = [];
}
