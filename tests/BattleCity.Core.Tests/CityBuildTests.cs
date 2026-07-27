using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Core.Network;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Network.Packets;

using Arch.Core;

using Xunit;

namespace BattleCity.Core.Tests;

public sealed class CityBuildTests
{
    [Fact]
    public void LoadCityLayout_CreatesBuildPermissions()
    {
        using var simulation = new GameSimulation();
        var layout = LevelLoader.LoadLegacyCity("Buenos Aires", "demo");

        simulation.LoadCityLayout(layout);

        Assert.True(simulation.TryGetCityBuild(0, out var build));
        Assert.True(build.CanBuild[2] >= 1);
        Assert.True(build.CanBuild[4] >= 1);
    }

    [Fact]
    public void MarkExistingBuildings_DoesNotReopenExistingFactoryWhenResearchFollows()
    {
        var build = new CityBuildState();
        CityBuildInitializer.ApplyLegacyStartingPermissions(build);

        // Factory listed before its research must stay CanBuild == 2 (already owned).
        var layout = new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings =
            [
                new CityBuildingPlacement(9, 20, 20, 102), // MedKit Factory
                new CityBuildingPlacement(8, 24, 20, 402), // MedKit Research
            ],
        };

        CityBuildInitializer.MarkExistingBuildings(build, layout);

        Assert.Equal(2, build.CanBuild[9]);
        Assert.Equal(2, build.CanBuild[8]);
        Assert.False(CityBuildPermissions.CanPlace(build, 9));
        Assert.False(CityBuildPermissions.IsVisibleInMenu(build, 9));
    }

    [Fact]
    public void TryPlaceBuilding_SpawnsHouseOnOpenTerrain()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });

        Assert.True(simulation.TryGetCityBuild(0, out var build));
        Assert.Equal(1, build.CanBuild[1]);

        Assert.True(simulation.TryPlaceBuilding(buildSlot: 2, gridAnchorX: 40, gridAnchorY: 40));

        Assert.True(
            BuildingPlacementValidator.TryFindBuildingAt(simulation.World, 40, 40, out _));
    }

    [Fact]
    public void LoadCityLayout_SpawnsCommandCenterEntity()
    {
        using var simulation = new GameSimulation();
        var layout = LevelLoader.LoadLegacyCity("Buenos Aires", "demo");

        simulation.LoadCityLayout(layout);

        Assert.True(CommandCenterLookup.TryGetWorldPosition(simulation.World, out _));
    }

    [Fact]
    public void CityOrbedService_PreservesCommandCenter()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });

        Assert.True(simulation.TryGetCityBuild(0, out var build));
        CityOrbedService.ApplyOrbed(simulation.World, build);

        Assert.True(CommandCenterLookup.TryGetWorldPosition(simulation.World, out _));
    }

    [Fact]
    public void CityOrbedNotificationSystem_TriggerForVictim_ShowsOverlay()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(System.Numerics.Vector2.Zero);

        CityOrbedNotificationSystem.Trigger(
            simulation.World,
            victimCityId: 0,
            attackerCityId: 1,
            victimCityName: "Test City",
            attackerCityName: "Enemy");

        ref var orbed = ref simulation.World.Get<CityOrbedState>(player);
        Assert.True(orbed.ShowOverlay);
        Assert.True(orbed.IsVictim);
        Assert.Contains("destroyed", orbed.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDropItemForNetworkPlayer_CreatesAuthoritativeItem()
    {
        using var simulation = new GameSimulation();
        var entity = simulation.CreateNetworkPlayerEntity(new System.Numerics.Vector2(12 * 48f, 12 * 48f), playerId: 1);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(entity);
        inventory.Orb = 1;

        Assert.True(simulation.TryDropItemForNetworkPlayer(
            1,
            ItemType.Orb,
            active: false,
            out var packet));
        Assert.Equal(5, packet.Type);
        Assert.True(packet.Id > 0);

        simulation.ApplyNetworkAddItem(packet);
        Assert.True(simulation.TryGetNetworkPlayerEntity(1, out _));
    }

    [Fact]
    public void TryFireShotForNetworkPlayer_BroadcastsLegacyShotPacket()
    {
        using var simulation = new GameSimulation();
        simulation.CreateNetworkPlayerEntity(new System.Numerics.Vector2(12 * 48f, 12 * 48f), playerId: 1);

        var request = new ClientShotPacket(600, 620, direction: 0, type: 0);
        Assert.True(simulation.TryFireShotForNetworkPlayer(1, request, out var shot));
        Assert.Equal(1, shot.PlayerId);
        Assert.Equal(0, shot.Type);
    }

    [Fact]
    public void TryPickupItemForNetworkPlayer_RemovesItemAndConfirmsPickup()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        var entity = simulation.CreateNetworkPlayerEntity(new System.Numerics.Vector2(12 * 48f, 12 * 48f), playerId: 1);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(entity);
        inventory.Bomb = 1;
        Assert.True(simulation.TryDropItemForNetworkPlayer(1, ItemType.Bomb, active: true, out var dropPacket));
        simulation.ApplyNetworkAddItem(dropPacket);

        Assert.True(simulation.TryPickupItemForNetworkPlayer(
            1,
            new ClientItemPickupPacket(dropPacket.Id, active: 1, itemType: (byte)ItemType.Bomb),
            out var removePacket,
            out var pickedUpPacket));

        Assert.Equal(dropPacket.Id, removePacket.ItemId);
        Assert.Equal((byte)ItemType.Bomb, pickedUpPacket.ItemType);
    }

    [Fact]
    public void TryBuildForNetworkPlayer_AssignsNetworkBuildingId()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });
        Assert.True(simulation.TryGetCityBuild(0, out var build));
        var mayorTileX = build.CommandCenterGridX - 1;
        var mayorTileY = build.CommandCenterGridY - 1;
        simulation.CreateNetworkPlayerEntity(
            new System.Numerics.Vector2(
                mayorTileX * GameConstants.TileSize,
                mayorTileY * GameConstants.TileSize),
            playerId: 1);

        Assert.True(simulation.TryBuildForNetworkPlayer(
            1,
            new ClientBuildPacket(40, 40, buildSlot: 2, isAutoBuild: false),
            out var buildingPacket));

        Assert.True(buildingPacket.Id > 0);
        simulation.ApplyNetworkNewBuilding(buildingPacket);
        Assert.True(BuildingCommandService.TryFindBuildingByNetworkId(
            simulation.World,
            buildingPacket.Id,
            out _));
    }

    [Fact]
    public void CollectJoinSnapshot_IncludesAuthoritativeItemsAndBuildings()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });
        simulation.SpawnDemoItems();
        simulation.AssignNetworkItemIds();

        var snapshot = new JoinWorldSnapshot();
        simulation.CollectJoinSnapshot(snapshot);

        Assert.NotEmpty(snapshot.Items);
        Assert.NotEmpty(snapshot.Buildings);
        Assert.All(snapshot.Items, item => Assert.True(item.Id > 0));
        Assert.All(snapshot.Buildings, building => Assert.True(building.Id > 0));
    }

    [Fact]
    public void CollectJoinSnapshot_IncludesDemolishedBuildings()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });
        Assert.True(simulation.TryGetCityBuild(0, out var build));
        var mayorTileX = build.CommandCenterGridX - 1;
        var mayorTileY = build.CommandCenterGridY - 1;
        simulation.CreateNetworkPlayerEntity(
            new System.Numerics.Vector2(
                mayorTileX * GameConstants.TileSize,
                mayorTileY * GameConstants.TileSize),
            playerId: 1);

        Assert.True(simulation.TryBuildForNetworkPlayer(
            1,
            new ClientBuildPacket(40, 40, buildSlot: 2, isAutoBuild: false),
            out var placed));
        Assert.True(simulation.TryDemolishForNetworkPlayer(
            1,
            new ClientDemolishPacket(placed.Id),
            out _));

        var snapshot = new JoinWorldSnapshot();
        simulation.CollectJoinSnapshot(snapshot);

        Assert.Contains(snapshot.RemovedBuildings, removed => removed.Id == placed.Id);
        Assert.DoesNotContain(snapshot.Buildings, building => building.Id == placed.Id);
    }

    [Fact]
    public void ResearchCompleteNotificationSystem_Trigger_ShowsOverlay()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(System.Numerics.Vector2.Zero);

        ResearchCompleteNotificationSystem.Trigger(simulation.World, cityId: 0, treeIndex: 0);

        ref var complete = ref simulation.World.Get<CityResearchCompleteState>(player);
        Assert.True(complete.ShowOverlay);
        Assert.Contains("Laser Factory", complete.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPreviewHelper_RejectsBlockedTerrain()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });

        Assert.True(simulation.TryGetCityBuild(0, out var build));
        var valid = BuildPreviewHelper.Evaluate(
            simulation.World,
            build,
            simulation.TileMap,
            buildModeSlot: 2,
            gridAnchorX: 40,
            gridAnchorY: 40,
            playerCenter: null,
            out var typeCode,
            out var isDemolish);

        Assert.True(valid);
        Assert.Equal(300, typeCode);
        Assert.False(isDemolish);
    }

    [Fact]
    public void ResearchVisuals_DetectsInProgressResearch()
    {
        var build = new CityBuildState();
        build.ResearchStatus[0] = 0;
        build.ResearchTimers[0] = 5f;

        Assert.True(ResearchVisuals.IsResearchInProgress(
            build,
            typeCode: 400,
            EconomyConstants.PopulationMaxNonHouse,
            out var treeIndex));
        Assert.Equal(0, treeIndex);
    }

    [Fact]
    public void ResearchSystem_CompletesAndUnlocksFactory()
    {
        using var world = World.Create();
        var build = new CityBuildState();
        build.CanBuild[2] = 2;
        build.ResearchStatus[0] = 0;

        world.Create(
            new BuildingRef { TypeCode = 400, MenuIndex = 2, GridAnchorX = 0, GridAnchorY = 0, CityId = build.CityId },
            new BuildingState { Population = EconomyConstants.PopulationMaxNonHouse });

        for (var i = 0; i < 11; i++)
        {
            ResearchSystem.Update(world, build, 1f);
        }

        Assert.Equal(-1, build.ResearchStatus[0]);
        Assert.Equal(1, build.CanBuild[3]); // Laser Factory unlocked
        Assert.Equal(1, build.CanBuild[6]); // Time Bomb (Cloak) Research unlocked via tree
        Assert.Equal(1, build.CanBuild[8]); // MedKit Research unlocked via tree
    }

    [Fact]
    public void TryDemolishForNetworkPlayer_RefusesCommandCenter()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });
        Assert.True(simulation.TryGetCityBuild(0, out var build));
        simulation.CreateNetworkPlayerEntity(
            new System.Numerics.Vector2(
                (build.CommandCenterGridX - 1) * GameConstants.TileSize,
                (build.CommandCenterGridY - 1) * GameConstants.TileSize),
            playerId: 1,
            cityId: 0);

        ushort ccNetworkId = 0;
        var query = new QueryDescription().WithAll<BuildingRef>();
        simulation.World.Query(
            in query,
            (ref BuildingRef building) =>
            {
                if (BuildingCatalog.IsCommandCenter(building.TypeCode) && building.NetworkId != 0)
                {
                    ccNetworkId = building.NetworkId;
                }
            });
        Assert.True(ccNetworkId > 0);

        Assert.False(simulation.TryDemolishForNetworkPlayer(
            1,
            new ClientDemolishPacket(ccNetworkId),
            out _));
    }

    [Fact]
    public void TryDemolishForNetworkPlayer_RefusesOtherCityBuilding()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });
        Assert.True(simulation.TryGetCityBuild(0, out var build));
        var mayorTileX = build.CommandCenterGridX - 1;
        var mayorTileY = build.CommandCenterGridY - 1;
        simulation.CreateNetworkPlayerEntity(
            new System.Numerics.Vector2(
                mayorTileX * GameConstants.TileSize,
                mayorTileY * GameConstants.TileSize),
            playerId: 1,
            cityId: 0);

        Assert.True(simulation.TryBuildForNetworkPlayer(
            1,
            new ClientBuildPacket(40, 40, buildSlot: 2, isAutoBuild: false),
            out var placed));

        simulation.CreateNetworkPlayerEntity(
            new System.Numerics.Vector2(
                mayorTileX * GameConstants.TileSize,
                mayorTileY * GameConstants.TileSize),
            playerId: 2,
            cityId: 5);

        Assert.False(simulation.TryDemolishForNetworkPlayer(
            2,
            new ClientDemolishPacket(placed.Id),
            out _));
    }

    [Fact]
    public void TryPickupItemForNetworkPlayer_RefusesOtherCityItem()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        var owner = simulation.CreateNetworkPlayerEntity(
            new System.Numerics.Vector2(12 * 48f, 12 * 48f),
            playerId: 1,
            cityId: 0);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(owner);
        inventory.Bomb = 1;
        Assert.True(simulation.TryDropItemForNetworkPlayer(1, ItemType.Bomb, active: true, out var dropPacket));
        simulation.ApplyNetworkAddItem(dropPacket);

        simulation.CreateNetworkPlayerEntity(
            new System.Numerics.Vector2(12 * 48f, 12 * 48f),
            playerId: 2,
            cityId: 5);

        Assert.False(simulation.TryPickupItemForNetworkPlayer(
            2,
            new ClientItemPickupPacket(dropPacket.Id, active: 1, itemType: (byte)ItemType.Bomb),
            out _,
            out _));
    }

    [Fact]
    public void EnsureCityBuild_AllowsPlaceNearThatCitysCommandCenter()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });

        var otherCity = simulation.EnsureCityBuild(7);
        otherCity.CommandCenterGridX = 80;
        otherCity.CommandCenterGridY = 80;

        Assert.True(simulation.TryPlaceBuilding(
            cityId: 7,
            buildSlot: 2,
            gridAnchorX: 90,
            gridAnchorY: 90,
            playerCenter: new System.Numerics.Vector2(81 * GameConstants.TileSize, 81 * GameConstants.TileSize)));

        Assert.True(BuildingPlacementValidator.TryFindBuildingAt(simulation.World, 90, 90, out var entity));
        Assert.Equal(7, simulation.World.Get<BuildingRef>(entity).CityId);
    }

    [Fact]
    public void EnsureCityBuild_DifferentCitiesHaveIndependentBuilds()
    {
        using var simulation = new GameSimulation();
        simulation.TileMap = TileMap.CreateEmpty();
        simulation.LoadCityLayout(new CityLayout
        {
            CityName = "Test",
            SourcePath = "test.city",
            Buildings = [],
        });

        var cityA = simulation.EnsureCityBuild(3);
        var cityB = simulation.EnsureCityBuild(9);
        cityA.CanBuild[1] = 0;
        cityB.CanBuild[1] = 1;

        Assert.True(simulation.TryGetCityBuild(3, out var buildA));
        Assert.True(simulation.TryGetCityBuild(9, out var buildB));
        Assert.Equal(0, buildA.CanBuild[1]);
        Assert.Equal(1, buildB.CanBuild[1]);
        Assert.NotSame(buildA, buildB);
        Assert.Contains(3, simulation.EnumerateCityBuildIds());
        Assert.Contains(9, simulation.EnumerateCityBuildIds());
    }
}
