using BattleCity.Shared.Constants;

namespace BattleCity.Shared.Gameplay;

/// <summary>Result of a mayor <c>/load</c> (<c>smAutoBuild</c>).</summary>
public enum AutoBuildOutcome : byte
{
    Denied = 0,
    Loaded = 1,
    MissingFile = 2,
}

/// <summary>
/// City-design name from <c>/load</c>. The file is <c>cities/{city}/{name}.city</c> on the server.
/// </summary>
public static class AutoBuildDesign
{
    public const int NameCapacity = 64;

    public static bool TryNormalize(string? raw, out string name)
    {
        name = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var token = raw.Trim();
        var space = token.IndexOf(' ');
        if (space >= 0)
        {
            token = token[..space];
        }

        if (token.EndsWith(GameConstants.CityFileExtension, StringComparison.OrdinalIgnoreCase))
        {
            token = token[..^GameConstants.CityFileExtension.Length];
        }

        if (token.Length == 0 || token.Length >= NameCapacity)
        {
            return false;
        }

        if (token is "." or ".." || token.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        if (token.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0)
        {
            return false;
        }

        name = token;
        return true;
    }

    public static string Describe(AutoBuildOutcome outcome, string designName, int placedCount, string cityName)
    {
        switch (outcome)
        {
            case AutoBuildOutcome.MissingFile:
                return $"Unable to open the file \"{cityName}/{designName}{GameConstants.CityFileExtension}\"!";
            case AutoBuildOutcome.Loaded when placedCount <= 0:
                return $"Loaded \"{designName}\" (no new buildings).";
            case AutoBuildOutcome.Loaded:
                return $"Loaded \"{designName}\" ({placedCount} buildings).";
            default:
                return "You cannot load a city template now!";
        }
    }
}
