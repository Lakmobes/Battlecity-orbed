using System.Numerics;

using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Maps;
using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Core.Tests;

public sealed class CityAlertTests
{
    [Fact]
    public void CityAlertSystem_TriggerForCity_SetsTimer()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(Vector2.Zero);
        ref var alert = ref simulation.World.Get<CityAlertState>(player);

        CityAlertSystem.TriggerForCity(simulation.World, 0);

        Assert.True(alert.IsUnderAttack);
        Assert.InRange(alert.UnderAttackRemainingSeconds, 2.9f, 3f);
    }

    [Fact]
    public void ApplyNetworkUnderAttack_TriggersHomeCityAlert()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(CityLayoutTestHelper.CreateMinimalLayout());
        var player = simulation.CreatePlayerEntity(Vector2.Zero);
        ref var alert = ref simulation.World.Get<CityAlertState>(player);

        simulation.ApplyNetworkUnderAttack();

        Assert.True(alert.IsUnderAttack);
        Assert.InRange(alert.UnderAttackRemainingSeconds, 2.9f, 3f);
    }

    [Fact]
    public void ApplyNetworkRemoveBuilding_OwnCity_TriggersUnderAttack()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(CityLayoutTestHelper.CreateMinimalLayout());
        var player = simulation.CreatePlayerEntity(Vector2.Zero);
        ref var alert = ref simulation.World.Get<CityAlertState>(player);

        simulation.ApplyNetworkRemoveBuilding(new ServerBuildingPacket(
            city: 0,
            x: 0,
            y: 0,
            buildSlot: 0,
            count: 0,
            id: 0,
            population: 0));

        Assert.True(alert.IsUnderAttack);
    }

    [Fact]
    public void ApplyNetworkRemoveBuilding_OtherCity_DoesNotTriggerUnderAttack()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(CityLayoutTestHelper.CreateMinimalLayout());
        var player = simulation.CreatePlayerEntity(Vector2.Zero);
        ref var alert = ref simulation.World.Get<CityAlertState>(player);

        simulation.ApplyNetworkRemoveBuilding(new ServerBuildingPacket(
            city: 7,
            x: 0,
            y: 0,
            buildSlot: 0,
            count: 0,
            id: 0,
            population: 0));

        Assert.False(alert.IsUnderAttack);
    }

    [Fact]
    public void UnderAttackMessageId_MatchesLegacyEnum()
    {
        Assert.Equal(58, (int)ServerMessageId.UnderAttack);
    }
}
