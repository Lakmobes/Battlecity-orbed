using System.Numerics;

using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Maps;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Core.Tests;

public class CloakNetworkTests
{
    [Fact]
    public void TryUseCloakForNetworkPlayer_ActivatesCloakAndConsumesInventory()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        var entity = simulation.CreateNetworkPlayerEntity(new Vector2(12 * 48f, 12 * 48f), playerId: 1);

        Assert.True(simulation.TryUseCloakForNetworkPlayer(1));

        ref var inventory = ref simulation.World.Get<PlayerInventory>(entity);
        ref var status = ref simulation.World.Get<TankStatus>(entity);
        Assert.Equal(0, inventory.Cloak);
        Assert.True(status.IsCloaked);
        Assert.True(status.CloakRemainingSeconds > 0f);
    }

    [Fact]
    public void ApplyNetworkCloak_ActivatesLocalPlayer()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.CreatePlayerEntity(Vector2.Zero);

        Assert.True(simulation.ApplyNetworkCloak(playerId: 1, localPlayerId: 1));

        var query = new Arch.Core.QueryDescription().WithAll<InputControlled, TankStatus>();
        simulation.World.Query(
            in query,
            (ref TankStatus status) => Assert.True(status.IsCloaked));
    }
}
