using System.Numerics;

using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Core.Tests;

public class MedKitNetworkTests
{
    [Fact]
    public void TryUseMedKitForNetworkPlayer_HealsAndConsumesInventory()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        var entity = simulation.CreateNetworkPlayerEntity(new Vector2(12 * 48f, 12 * 48f), playerId: 1);
        ref var health = ref simulation.World.Get<Health>(entity);
        health.Current = 10;
        ref var inventory = ref simulation.World.Get<PlayerInventory>(entity);
        inventory.MedKit = 2;

        Assert.True(simulation.TryUseMedKitForNetworkPlayer(1, out var hpPacket));

        Assert.Equal(GameConstants.MaxHealth, health.Current);
        Assert.Equal(1, inventory.MedKit);
        Assert.Equal(1, hpPacket.PlayerId);
        Assert.Equal(GameConstants.MaxHealth, hpPacket.Health);
    }

    [Fact]
    public void TryUseMedKitForNetworkPlayer_RejectsWhenInventoryEmpty()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        var entity = simulation.CreateNetworkPlayerEntity(new Vector2(12 * 48f, 12 * 48f), playerId: 1);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(entity);
        inventory.MedKit = 0;

        Assert.False(simulation.TryUseMedKitForNetworkPlayer(1, out _));
    }

    [Fact]
    public void ApplyNetworkHp_HealsLocalInputControlledPlayer()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        var entity = simulation.CreatePlayerEntity(new Vector2(12 * 48f, 12 * 48f));
        ref var health = ref simulation.World.Get<Health>(entity);
        health.Current = 10;

        simulation.ApplyNetworkHp(new Shared.Network.Packets.ServerHpPacket(playerId: 3, health: 40), localPlayerId: 3);

        Assert.Equal(GameConstants.MaxHealth, simulation.World.Get<Health>(entity).Current);
    }
}
