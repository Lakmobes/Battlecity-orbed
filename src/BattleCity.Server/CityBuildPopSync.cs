using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Server;

/// <summary>Diffs authoritative city build tree and building population for network sync.</summary>
public sealed class CityBuildPopSync
{
    private readonly Dictionary<int, int[]> _canBuildByCity = new();
    private readonly Dictionary<ushort, byte> _populations = new();

    public void Reset(GameSimulation simulation) => Reset(simulation, cityId: 0);

    public void Reset(GameSimulation simulation, int cityId)
    {
        var snapshot = new int[CityBuildState.MenuSlotCount];
        if (simulation.TryGetCityBuild(cityId, out var build))
        {
            build.CanBuild.CopyTo(snapshot, 0);
        }

        _canBuildByCity[cityId] = snapshot;

        _populations.Clear();
        foreach (var (buildingId, population) in simulation.CollectBuildingPopulations())
        {
            _populations[buildingId] = population;
        }
    }

    public IEnumerable<ServerCanBuildPacket> CollectCanBuildChanges(GameSimulation simulation) =>
        CollectCanBuildChanges(simulation, cityId: 0);

    public IEnumerable<ServerCanBuildPacket> CollectCanBuildChanges(GameSimulation simulation, int cityId)
    {
        if (!simulation.TryGetCityBuild(cityId, out var build))
        {
            yield break;
        }

        if (!_canBuildByCity.TryGetValue(cityId, out var previous))
        {
            previous = new int[CityBuildState.MenuSlotCount];
            _canBuildByCity[cityId] = previous;
        }

        for (var menuIndex = 0; menuIndex < build.CanBuild.Length; menuIndex++)
        {
            var value = build.CanBuild[menuIndex];
            if (previous[menuIndex] == value)
            {
                continue;
            }

            previous[menuIndex] = value;
            yield return ServerCanBuildPacket.FromMenuIndex(menuIndex, value);
        }
    }

    public IEnumerable<ServerUpdatePopPacket> CollectPopulationChanges(GameSimulation simulation)
    {
        var seen = new HashSet<ushort>();

        foreach (var (buildingId, population) in simulation.CollectBuildingPopulations())
        {
            seen.Add(buildingId);
            if (_populations.TryGetValue(buildingId, out var previous) && previous == population)
            {
                continue;
            }

            _populations[buildingId] = population;
            yield return new ServerUpdatePopPacket(buildingId, population);
        }

        foreach (var removedId in _populations.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _populations.Remove(removedId);
        }
    }

    public IEnumerable<ServerCanBuildPacket> CreateCanBuildSnapshot(GameSimulation simulation) =>
        CreateCanBuildSnapshot(simulation, cityId: 0);

    public IEnumerable<ServerCanBuildPacket> CreateCanBuildSnapshot(GameSimulation simulation, int cityId)
    {
        if (!simulation.TryGetCityBuild(cityId, out var build))
        {
            yield break;
        }

        for (var menuIndex = 0; menuIndex < build.CanBuild.Length; menuIndex++)
        {
            yield return ServerCanBuildPacket.FromMenuIndex(menuIndex, build.CanBuild[menuIndex]);
        }
    }
}
