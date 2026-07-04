using BattleCity.Shared.Constants;

namespace BattleCity.Core.Levels;

public static class CityLayoutPaths
{
    public static string GetLegacyLayoutPath(string repoRoot, string cityName, string layoutName = "demo") =>
        Path.Combine(
            repoRoot,
            "legacy",
            "data",
            GameConstants.CitiesFolder,
            cityName,
            layoutName + GameConstants.CityFileExtension);

    public static string? FindLegacyCityLayout(string cityName, string layoutName = "demo")
    {
        var directory = AppContext.BaseDirectory;

        while (directory is not null)
        {
            var candidate = GetLegacyLayoutPath(directory, cityName, layoutName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }
}
