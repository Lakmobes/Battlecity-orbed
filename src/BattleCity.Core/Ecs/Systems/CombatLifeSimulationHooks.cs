namespace BattleCity.Core.Ecs.Systems;

public readonly struct CombatLifeSimulationHooks
{
    /// <summary>Online local player waits for authoritative <c>smWarp</c>.</summary>
    public bool SuppressLocalPlayerRespawn { get; init; }

    /// <summary>Server defers network-player respawn to <c>ProcessNetworkPlayerRespawns</c>.</summary>
    public bool DeferNetworkPlayerRespawn { get; init; }
}
