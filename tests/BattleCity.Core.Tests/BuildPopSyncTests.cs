using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Core.Tests;

public class BuildPopSyncTests
{
    [Fact]
    public void ServerCanBuildPacket_RoundTripsMenuIndex()
    {
        var packet = ServerCanBuildPacket.FromMenuIndex(4, canBuildState: 2);

        Span<byte> buffer = stackalloc byte[ServerCanBuildPacket.Size];
        packet.Write(buffer);
        var read = ServerCanBuildPacket.Read(buffer);

        Assert.Equal(5, read.BuildSlot);
        Assert.Equal((byte)0, read.LegacyCanBuild);
        Assert.Equal((byte)2, read.CanBuildState);
    }

    [Fact]
    public void ApplyNetworkCanBuild_UpdatesCityBuildState()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(LevelLoader.LoadLegacyCity("Buenos Aires", "demo"));
        Assert.True(simulation.TryGetCityBuild(0, out var build));

        build.CanBuild[2] = 0;
        simulation.ApplyNetworkCanBuild(ServerCanBuildPacket.FromMenuIndex(2, canBuildState: 1));

        Assert.Equal(1, build.CanBuild[2]);
    }

    [Fact]
    public void ApplyNetworkUpdatePop_UpdatesBuildingPopulation()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(LevelLoader.LoadLegacyCity("Buenos Aires", "demo"));

        var buildingId = simulation.CollectBuildingPopulations().First().BuildingId;
        simulation.ApplyNetworkUpdatePop(new ServerUpdatePopPacket(buildingId, 120));

        var updated = simulation.CollectBuildingPopulations().First(entry => entry.BuildingId == buildingId);
        Assert.Equal(120, updated.Population);
    }
}
