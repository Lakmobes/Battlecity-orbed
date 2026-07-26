using BattleCity.Core.Maps;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Core.Tests;

public class TileMapTests
{
    [Fact]
    public void LoadFromLegacyMapDat_HasExpectedSize()
    {
        var mapDatPath = FindLegacyMapDat();
        Assert.NotNull(mapDatPath);

        var map = TileMap.LoadFromLegacyMapDat(mapDatPath);

        Assert.Equal(TileMap.Size, map.Terrain.GetLength(0));
        Assert.Equal(TileMap.Size, map.Terrain.GetLength(1));
    }

    [Fact]
    public void AutotileCalculatorMatchesLoadedMapRecalculation()
    {
        var mapDatPath = FindLegacyMapDat();
        Assert.NotNull(mapDatPath);

        var map = TileMap.LoadFromLegacyMapDat(mapDatPath);
        var recalculated = AutotileCalculator.Calculate(map.Terrain);

        for (var x = 0; x < TileMap.Size; x++)
        {
            for (var y = 0; y < TileMap.Size; y++)
            {
                Assert.Equal(recalculated[x, y], map.AutotileIndices[x, y]);
            }
        }
    }

    [Fact]
    public void JsonRoundTripPreservesTerrainAndAutotiles()
    {
        var mapDatPath = FindLegacyMapDat();
        Assert.NotNull(mapDatPath);

        var original = TileMap.LoadFromLegacyMapDat(mapDatPath);
        var tempDirectory = Path.Combine(Path.GetTempPath(), "battlecity-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var terrainPath = Path.Combine(tempDirectory, "terrain.json");
            var autotilePath = Path.Combine(tempDirectory, "autotile-index.json");
            original.WriteJson(terrainPath, autotilePath);

            var loaded = TileMap.LoadFromJson(terrainPath, autotilePath);

            Assert.Equal(
                CountTerrain(original, TerrainTileType.Lava),
                CountTerrain(loaded, TerrainTileType.Lava));

            Assert.Equal(
                CountTerrain(original, TerrainTileType.Rock),
                CountTerrain(loaded, TerrainTileType.Rock));

            for (var x = 0; x < TileMap.Size; x++)
            {
                for (var y = 0; y < TileMap.Size; y++)
                {
                    Assert.Equal(original.AutotileIndices[x, y], loaded.AutotileIndices[x, y]);
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void IsolatedRockTileUsesFullAutotileSheetOffset()
    {
        var terrain = new TerrainTileType[TileMap.Size, TileMap.Size];
        terrain[10, 10] = TerrainTileType.Rock;

        var autotiles = AutotileCalculator.Calculate(terrain);

        Assert.Equal(15 * 48, autotiles[10, 10]);
    }

    [Theory]
    [InlineData(0, -1, 8)]  // open north → up bit → col 8
    [InlineData(0, 1, 4)]   // open south → down bit → col 4
    [InlineData(-1, 0, 1)]  // open west → left bit → col 1
    [InlineData(1, 0, 2)]   // open east → right bit → col 2
    public void AutotileColumnMatchesSheetEdgeOrder(int openDx, int openDy, int expectedColumn)
    {
        var terrain = new TerrainTileType[TileMap.Size, TileMap.Size];
        // 3×3 lava block; clear one neighbor of the center so only that edge is open.
        for (var y = 9; y <= 11; y++)
        {
            for (var x = 9; x <= 11; x++)
            {
                terrain[x, y] = TerrainTileType.Lava;
            }
        }

        terrain[10 + openDx, 10 + openDy] = TerrainTileType.Open;

        var autotiles = AutotileCalculator.Calculate(terrain);
        Assert.Equal(expectedColumn * 48, autotiles[10, 10]);
    }

    private static string? FindLegacyMapDat()
    {
        var directory = AppContext.BaseDirectory;

        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "legacy", "data", "map.dat");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    private static int CountTerrain(TileMap map, TerrainTileType tileType)
    {
        var count = 0;

        for (var x = 0; x < TileMap.Size; x++)
        {
            for (var y = 0; y < TileMap.Size; y++)
            {
                if (map.Terrain[x, y] == tileType)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
