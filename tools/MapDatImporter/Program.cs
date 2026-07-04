using BattleCity.Core.Maps;
using BattleCity.Shared.Data;

namespace BattleCity.Tools.MapDatImporter;

internal static class Program
{
    public static int Main(string[] args)
    {
        var mapDatPath = FindPath(args, "--input");
        var outputDirectory = FindPath(args, "--output");

        if (mapDatPath is null || outputDirectory is null)
        {
            PrintUsage();
            return 1;
        }

        if (!File.Exists(mapDatPath))
        {
            Console.Error.WriteLine($"map.dat not found: {mapDatPath}");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);

        var tileMap = TileMap.LoadFromLegacyMapDat(mapDatPath);
        var terrainPath = Path.Combine(outputDirectory, "terrain.json");
        var autotilePath = Path.Combine(outputDirectory, "autotile-index.json");
        tileMap.WriteJson(terrainPath, autotilePath);

        var lavaCount = CountTerrain(tileMap, TerrainTileType.Lava);
        var rockCount = CountTerrain(tileMap, TerrainTileType.Rock);
        var cityCount = CountTerrain(tileMap, TerrainTileType.CityCenter);

        Console.WriteLine($"Wrote {terrainPath}");
        Console.WriteLine($"Wrote {autotilePath}");
        Console.WriteLine($"Lava={lavaCount}, Rock={rockCount}, CityCenters={cityCount}");

        return 0;
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

    private static string? FindPath(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: MapDatImporter --input <map.dat> --output <json-dir>");
    }
}
