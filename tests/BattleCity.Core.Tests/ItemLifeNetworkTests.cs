using System.Numerics;

using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Maps;
using BattleCity.Shared.Data;
using BattleCity.Shared.Gameplay;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Core.Tests;

public class ItemLifeNetworkTests
{
    [Fact]
    public void ReportItemLifeToNetwork_QueuesBurnSyncWhenWallDamaged()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.ReportItemLifeToNetwork = true;

        const ushort networkItemId = 12;
        var player = simulation.CreatePlayerEntity(new Vector2(10 * 48f, 10 * 48f));
        var wall = GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Wall,
            12,
            10,
            cityId: 0,
            networkItemId: networkItemId);
        ref var wallHealth = ref simulation.World.Get<Health>(wall);
        wallHealth.Current = 25;

        var bullet = GameplayEntityFactory.CreateBullet(
            simulation.World,
            BulletKind.Laser,
            new Vector2(12 * 48f + 20f, 10 * 48f + 20f),
            direction: 8,
            player);
        ServerItemLifePacket? queued = null;
        BulletCollisionSystem.Resolve(
            simulation.World,
            simulation.TileMap,
            onPlacedItemDamaged: (_, _, healthAfterDamage) =>
            {
                if (ItemDamageSync.ShouldBroadcastItemLife(ItemType.Wall, healthAfterDamage))
                {
                    queued = new ServerItemLifePacket(networkItemId, ItemDamageSync.LegacyBurnLifeValue);
                }
            });

        Assert.NotNull(queued);
        Assert.Equal(networkItemId, queued.Value.ItemId);
        Assert.Equal(ItemDamageSync.LegacyBurnLifeValue, queued.Value.Life);
    }

    [Fact]
    public void ApplyNetworkItemLife_UpdatesRemoteItemHealth()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();

        const ushort networkItemId = 9;
        var wall = GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Wall,
            5,
            5,
            networkItemId: networkItemId);
        ref var health = ref simulation.World.Get<Health>(wall);
        health.Current = 30;

        simulation.ApplyNetworkItemLife(new ServerItemLifePacket(networkItemId, ItemDamageSync.LegacyBurnLifeValue));

        Assert.Equal(ItemDamageSync.LegacyBurnLifeValue, health.Current);
    }
}
