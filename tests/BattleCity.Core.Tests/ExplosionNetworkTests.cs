using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Core.Tests;

public class ExplosionNetworkTests
{
    [Fact]
    public void ReportBombEventsToNetwork_QueuesExplosionAndItemRemoval()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.ReportBombEventsToNetwork = true;

        const int gridX = 10;
        const int gridY = 12;
        const ushort networkItemId = 42;
        GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Bomb,
            gridX,
            gridY,
            active: true,
            networkItemId: networkItemId);

        simulation.Tick(EconomyConstants.TimerBomb / 1000f + 0.1f);

        Assert.True(simulation.TryConsumeNetworkExplosionEvent(out var explosionEvent));
        Assert.Equal((ushort)(gridX + 1), explosionEvent.Explosion.GridX);
        Assert.Equal((ushort)(gridY + 1), explosionEvent.Explosion.GridY);
        Assert.Equal(networkItemId, explosionEvent.RemovedItemId);
        Assert.False(simulation.TryConsumeNetworkExplosionEvent(out _));
    }

    [Fact]
    public void SuppressLocalBombDetonation_SkipsLocalDetonation()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.SuppressLocalBombDetonation = true;

        GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Bomb,
            5,
            5,
            active: true);

        simulation.Tick(EconomyConstants.TimerBomb / 1000f + 0.1f);

        var bombQuery = new QueryDescription().WithAll<PlacedItemRef>();
        var bombCount = 0;
        simulation.World.Query(in bombQuery, (ref PlacedItemRef item) =>
        {
            if (item.Type == ItemType.Bomb)
            {
                bombCount++;
            }
        });

        Assert.Equal(1, bombCount);
    }

    [Fact]
    public void ApplyNetworkExplosion_CreatesLargeExplosionEntity()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();

        simulation.ApplyNetworkExplosion(new ServerExplosionPacket(cityId: 0, gridX: 11, gridY: 13));

        var explosionQuery = new QueryDescription().WithAll<ExplosionRef>();
        var found = false;
        simulation.World.Query(
            in explosionQuery,
            (ref ExplosionRef explosion) =>
            {
                found = true;
                Assert.Equal(ExplosionKind.Large, explosion.Kind);
            });

        Assert.True(found);
    }
}
