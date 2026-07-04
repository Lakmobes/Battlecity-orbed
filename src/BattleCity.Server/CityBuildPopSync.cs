using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Server;

/// <summary>Diffs authoritative city build tree and building population for network sync.</summary>
public sealed class CityBuildPopSync
{
    private readonly int[] _canBuild = new int[CityBuildState.MenuSlotCount];
    private readonly Dictionary<ushort, byte> _populations = new();

    public void Reset(GameSimulation simulation)
    {
        Array.Clear(_canBuild, 0, _canBuild.Length);
        _populations.Clear();

        if (simulation.TryGetCityBuild(0, out var build))
        {
            build.CanBuild.CopyTo(_canBuild, 0);
        }

        foreach (var (buildingId, population) in simulation.CollectBuildingPopulations())
        {
            _populations[buildingId] = population;
        }
    }

    public IEnumerable<ServerCanBuildPacket> CollectCanBuildChanges(GameSimulation simulation)
    {
        if (!simulation.TryGetCityBuild(0, out var build))
        {
            yield break;
        }

        for (var menuIndex = 0; menuIndex < build.CanBuild.Length; menuIndex++)
        {
            var value = build.CanBuild[menuIndex];
            if (_canBuild[menuIndex] == value)
            {
                continue;
            }

            _canBuild[menuIndex] = value;
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

    public IEnumerable<ServerCanBuildPacket> CreateCanBuildSnapshot(GameSimulation simulation)
    {
        if (!simulation.TryGetCityBuild(0, out var build))
        {
            yield break;
        }

        for (var menuIndex = 0; menuIndex < build.CanBuild.Length; menuIndex++)
        {
            yield return ServerCanBuildPacket.FromMenuIndex(menuIndex, build.CanBuild[menuIndex]);
        }
    }
}
