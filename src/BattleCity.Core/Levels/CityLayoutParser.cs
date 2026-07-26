using BattleCity.Shared.Catalogs;

namespace BattleCity.Core.Levels;

public static class CityLayoutParser
{
    public static CityLayout ParseFile(string path, string? cityName = null)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"City layout not found: {path}", path);
        }

        var buildings = new List<CityBuildingPlacement>();

        foreach (var line in File.ReadLines(path))
        {
            if (!TryParseLine(line, out var placement))
            {
                continue;
            }

            buildings.Add(placement);
        }

        return new CityLayout
        {
            CityName = cityName ?? InferCityName(path),
            SourcePath = path,
            Buildings = buildings,
        };
    }

    public static bool TryParseLine(string line, out CityBuildingPlacement placement)
    {
        placement = default;
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var parts = trimmed.Split((char[]?)[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var menuIndex)
            || !int.TryParse(parts[1], out var gridX)
            || !int.TryParse(parts[2], out var gridY))
        {
            return false;
        }

        if (menuIndex <= 0 || gridX <= 0 || gridY <= 0)
        {
            return false;
        }

        if (menuIndex > BuildingCatalog.MenuTypeCodes.Count)
        {
            return false;
        }

        var typeCode = BuildingCatalog.MenuTypeCodes[menuIndex - 1];
        // City files use 1-based menu slots; CanBuild / BuildingRef use 0-based indexes.
        placement = new CityBuildingPlacement(menuIndex - 1, gridX, gridY, typeCode);
        return true;
    }

    private static string InferCityName(string path)
    {
        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(directory)
            ? "Unknown"
            : Path.GetFileName(directory);
    }
}
