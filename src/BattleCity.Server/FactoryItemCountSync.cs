using BattleCity.Core.Ecs;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Server;

/// <summary>Diffs factory bay stock (<c>BuildingState.ItemsLeft</c>) for network sync.</summary>
public sealed class FactoryItemCountSync
{
    private readonly Dictionary<ushort, byte> _itemCounts = new();

    public void Reset(GameSimulation simulation)
    {
        _itemCounts.Clear();
        foreach (var (buildingId, count) in simulation.CollectFactoryItemCounts())
        {
            _itemCounts[buildingId] = count;
        }
    }

    public IEnumerable<ServerItemCountPacket> CollectItemCountChanges(GameSimulation simulation)
    {
        var seen = new HashSet<ushort>();

        foreach (var (buildingId, count) in simulation.CollectFactoryItemCounts())
        {
            seen.Add(buildingId);
            if (_itemCounts.TryGetValue(buildingId, out var previous) && previous == count)
            {
                continue;
            }

            _itemCounts[buildingId] = count;
            yield return new ServerItemCountPacket(buildingId, count);
        }

        foreach (var removedId in _itemCounts.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _itemCounts.Remove(removedId);
        }
    }

    public IEnumerable<ServerItemCountPacket> CreateItemCountSnapshot(GameSimulation simulation)
    {
        foreach (var (buildingId, count) in simulation.CollectFactoryItemCounts())
        {
            yield return new ServerItemCountPacket(buildingId, count);
        }
    }
}
