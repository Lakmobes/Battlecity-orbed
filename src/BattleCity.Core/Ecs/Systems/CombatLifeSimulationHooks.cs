namespace BattleCity.Core.Ecs.Systems;

public readonly struct CombatLifeSimulationHooks
{
    /// <summary>
    /// When true, online local player still respawns locally when the timer hits 0
    /// (optimistic); <c>smWarp</c> reconciles position. Kept for call-site clarity.
    /// </summary>
    public bool SuppressLocalPlayerRespawn { get; init; }

    /// <summary>Server defers network-player respawn to ProcessNetworkPlayerRespawns.</summary>
    public bool DeferNetworkPlayerRespawn { get; init; }

    /// <summary>Invoked once when a living tank transitions to dead.</summary>
    public Action<Arch.Core.Entity>? OnTankDied { get; init; }

    /// <summary>Optional open spawn near the city CC (preferred over stale SpawnPosition).</summary>
    public Func<Arch.Core.Entity, System.Numerics.Vector2?>? ResolveRespawnPosition { get; init; }
}
