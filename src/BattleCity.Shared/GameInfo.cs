namespace BattleCity.Shared;

public static class GameInfo
{
    public const string Title = "Battle City";

    /// <summary>MonoGame rewrite version (distinct from legacy 3.5.7).</summary>
    public const string Version = "4.0.0-dev";

    /// <summary>Original C++ release this rewrite targets for gameplay parity.</summary>
    public const string LegacyVersion = Constants.NetworkConstants.LegacyVersion;
}
