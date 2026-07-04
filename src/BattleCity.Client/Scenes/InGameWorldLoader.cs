using BattleCity.Core.Maps;

namespace BattleCity.Client.Scenes;

internal static class InGameWorldLoader
{
    public static TileMap LoadTileMap()
    {
        var terrainPath = Path.Combine(AppContext.BaseDirectory, "Content", "Data", "terrain.json");
        var autotilePath = Path.Combine(AppContext.BaseDirectory, "Content", "Data", "autotile-index.json");

        if (File.Exists(terrainPath))
        {
            return TileMap.LoadFromJson(terrainPath, autotilePath);
        }

        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "legacy", "data", "map.dat");
            if (File.Exists(candidate))
            {
                return TileMap.LoadFromLegacyMapDat(candidate);
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new FileNotFoundException(
            "Terrain data not found. Run tools/ContentBuild.ps1 to generate Content/Data JSON.");
    }
}
