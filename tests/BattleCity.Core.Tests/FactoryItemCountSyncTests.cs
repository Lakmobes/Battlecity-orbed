using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Server;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;

using Arch.Core;

using Xunit;

namespace BattleCity.Core.Tests;

public class FactoryItemCountSyncTests
{
    [Fact]
    public void CollectItemCountChanges_EmitsWhenFactoryStockChanges()
    {
        using var simulation = CreateSimulationWithFactory();
        var sync = new FactoryItemCountSync();
        sync.Reset(simulation);

        Assert.Empty(sync.CollectItemCountChanges(simulation).ToList());

        ref var state = ref GetFactoryState(simulation);
        state.ItemsLeft = 4;

        var changes = sync.CollectItemCountChanges(simulation).ToList();
        Assert.Single(changes);
        Assert.Equal(4, changes[0].ItemCount);
    }

    [Fact]
    public void ApplyNetworkItemCount_UpdatesFactoryOverlayStock()
    {
        using var simulation = CreateSimulationWithFactory();
        var buildingId = simulation.CollectFactoryItemCounts().Single().BuildingId;

        simulation.ApplyNetworkItemCount(new Shared.Network.Packets.ServerItemCountPacket(buildingId, 3));

        ref var state = ref GetFactoryState(simulation);
        Assert.Equal(3, state.ItemsLeft);
    }

    private static GameSimulation CreateSimulationWithFactory()
    {
        var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });

        var factoryType = BuildingCatalog.MenuTypeCodes.First(BuildingCatalog.IsFactory);
        var entity = simulation.World.Create(
            new BuildingRef
            {
                TypeCode = factoryType,
                GridAnchorX = 10,
                GridAnchorY = 10,
                NetworkId = 42,
            },
            new BuildingState
            {
                Population = EconomyConstants.PopulationMaxNonHouse,
                ItemsLeft = 5,
            },
            new Transform2D { Position = new System.Numerics.Vector2(10 * 48f, 10 * 48f) },
            new SpriteRef { TextureKey = "Sprites/Buildings" },
            new Collider { Layer = CollisionLayer.Building });

        _ = entity;
        return simulation;
    }

    private static ref BuildingState GetFactoryState(GameSimulation simulation)
    {
        Entity factory = default;
        simulation.World.Query(
            new QueryDescription().WithAll<BuildingRef, BuildingState>(),
            (Entity entity, ref BuildingRef building) =>
            {
                if (BuildingCatalog.IsFactory(building.TypeCode))
                {
                    factory = entity;
                }
            });

        return ref simulation.World.Get<BuildingState>(factory);
    }
}
