using Arch.Core;

using BattleCity.Core.Collision;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Levels;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.Levels;

public static class LevelLoader
{
    public static CityLayout LoadLegacyCity(string cityName, string layoutName = "demo")
    {
        var path = CityLayoutPaths.FindLegacyCityLayout(cityName, layoutName)
            ?? throw new FileNotFoundException(
                $"Legacy city layout '{layoutName}' for '{cityName}' was not found under legacy/data/cities/.");

        return CityLayoutParser.ParseFile(path, cityName);
    }

    public static void SpawnBuildings(World world, CityLayout layout)
    {
        foreach (var building in layout.Buildings)
        {
            SpawnBuilding(world, building);
        }
    }

    public static Entity SpawnCommandCenter(World world, int gridAnchorX, int gridAnchorY) =>
        SpawnBuilding(
            world,
            new CityBuildingPlacement(-1, gridAnchorX, gridAnchorY, BuildingCatalog.CommandCenterTypeCode));

    public static Entity SpawnBuilding(World world, CityBuildingPlacement building)
    {
        var position = BuildingPlacement.GridAnchorToWorldPosition(building.GridX, building.GridY);
        var (sourceX, sourceY) = BuildingSprites.GetSourceOrigin(building.TypeCode);

        var (offsetX, offsetY, width, height) = BuildingCollision.GetPlayerColliderShape(building.TypeCode);

        return world.Create(
            new Transform2D { Position = position, PreviousPosition = position },
            new BuildingRef
            {
                MenuIndex = building.MenuIndex,
                TypeCode = building.TypeCode,
                GridAnchorX = building.GridX,
                GridAnchorY = building.GridY,
            },
            new BuildingState
            {
                Population = GetInitialPopulation(building.TypeCode),
                ItemsLeft = 0,
            },
            new SpriteRef
            {
                TextureKey = BuildingSprites.TextureKey,
                SourceX = sourceX,
                SourceY = sourceY,
                Width = BuildingSprites.SpriteSize,
                Height = BuildingSprites.SpriteSize,
            },
            new Collider
            {
                OffsetX = offsetX,
                OffsetY = offsetY,
                Width = width,
                Height = height,
                Layer = CollisionLayer.Building,
            });
    }

    private static int GetInitialPopulation(int typeCode)
    {
        if (BuildingCatalog.IsResearch(typeCode))
        {
            return 0;
        }

        if (BuildingCatalog.IsHouse(typeCode))
        {
            return EconomyConstants.PopulationMaxHouse / 2;
        }

        return EconomyConstants.PopulationMaxNonHouse / 2;
    }
}
