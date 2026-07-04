namespace BattleCity.Core;

/// <summary>
/// Placeholder type retained so BattleCity.Core builds; ECS lives under BattleCity.Core.Ecs.
/// </summary>
public static class AssemblyMarker
{
    public static string Version => Shared.GameInfo.Version;

    public static float FixedTimestep => Ecs.GameSimulation.FixedDeltaSeconds;
}
