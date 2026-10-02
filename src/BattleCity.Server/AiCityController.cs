using System.Numerics;

using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;

namespace BattleCity.Server;

public enum AiCityStance
{
    /// <summary>Mayor and soldiers hold the city.</summary>
    LeanDefense = 0,

    /// <summary>Mayor and one soldier defend; the rest raid.</summary>
    Balanced = 1,

    /// <summary>Mayor holds home; every soldier raids. Longer aggro.</summary>
    LeanOffense = 2,
}

/// <summary>Result of aligning armed AI cities with whether humans are in a city.</summary>
public readonly struct AiCityPresenceChange
{
    public AiCityPresenceChange(
        IReadOnlyList<AiBotPlayer> spawned,
        IReadOnlyList<AiBotPlayer> removed,
        string? message)
    {
        Spawned = spawned;
        Removed = removed;
        Message = message;
    }

    public IReadOnlyList<AiBotPlayer> Spawned { get; }

    public IReadOnlyList<AiBotPlayer> Removed { get; }

    public string? Message { get; }

    public static AiCityPresenceChange None { get; } = new([], [], null);
}

/// <summary>
/// Scripted enemy cities. The host arms them; tanks only exist while at least one human is in a city.
/// </summary>
public sealed class AiCityController
{
    public const int DefaultSoldierCount = 3;
    public const int MaxCities = 4;

    private readonly List<AiCitySlot> _cities = [];
    private int _appliedCityCount = -1;
    private AiCityStance _appliedStance;

    public bool IsArmed { get; private set; }

    public bool IsActive => _cities.Count > 0;

    public int RequestedCityCount { get; private set; } = 1;

    public AiCityStance Stance { get; private set; } = AiCityStance.Balanced;

    public IReadOnlyList<byte> CityIds => _cities.Select(city => city.CityId).ToList();

    public IReadOnlyList<AiBotPlayer> Bots => _cities.SelectMany(city => city.Bots).ToList();

    public void Arm(int cityCount, AiCityStance stance)
    {
        RequestedCityCount = Math.Clamp(cityCount, 1, MaxCities);
        Stance = stance;
        IsArmed = true;
    }

    public void Disarm() => IsArmed = false;

    public AiCityPresenceChange Sync(
        GameSimulation simulation,
        CityRegistry cities,
        CityMayorRegistry mayors,
        Func<byte> allocatePlayerId,
        Action<byte> releasePlayerId,
        IEnumerable<(byte PlayerId, byte CityId, bool InGame)> humanPlayers)
    {
        var humans = humanPlayers as IReadOnlyList<(byte PlayerId, byte CityId, bool InGame)>
            ?? humanPlayers.ToList();
        var humansInGame = humans.Any(human => human.InGame);

        if (!IsArmed || !humansInGame)
        {
            if (_cities.Count == 0)
            {
                return AiCityPresenceChange.None;
            }

            var removed = TakeAllBots();
            Despawn(simulation, mayors, releasePlayerId, removed);
            _appliedCityCount = -1;
            var why = !IsArmed
                ? "AI cities disarmed."
                : "AI cities standing down (no players in a city).";
            return new AiCityPresenceChange([], removed, why);
        }

        if (_cities.Count > 0
            && _appliedCityCount == RequestedCityCount
            && _appliedStance == Stance)
        {
            return AiCityPresenceChange.None;
        }

        var cleared = TakeAllBots();
        Despawn(simulation, mayors, releasePlayerId, cleared);

        var spawned = new List<AiBotPlayer>();
        var reserved = new HashSet<byte>();
        for (var i = 0; i < RequestedCityCount; i++)
        {
            if (!TrySpawnCity(
                    simulation,
                    cities,
                    mayors,
                    allocatePlayerId,
                    humans,
                    reserved,
                    out var slot,
                    out _))
            {
                break;
            }

            _cities.Add(slot);
            reserved.Add(slot.CityId);
            spawned.AddRange(slot.Bots);
        }

        RefreshAttackGoals(simulation, humans);
        _appliedCityCount = RequestedCityCount;
        _appliedStance = Stance;

        if (spawned.Count == 0)
        {
            return new AiCityPresenceChange([], cleared, "AI cities armed, but no free city was available.");
        }

        var names = string.Join(", ", _cities.Select(city => $"{CityCatalog.GetName(city.CityId)} ({city.CityId})"));
        var message =
            $"AI cities active ({StanceLabel(Stance)}): {names} — {spawned.Count} bots.";
        return new AiCityPresenceChange(spawned, cleared, message);
    }

    public void RefreshAttackGoals(
        GameSimulation simulation,
        IEnumerable<(byte PlayerId, byte CityId, bool InGame)> humanPlayers)
    {
        if (!IsActive)
        {
            return;
        }

        var goals = new List<Vector2>();
        foreach (var human in humanPlayers)
        {
            if (!human.InGame)
            {
                continue;
            }

            if (simulation.TryGetCityRespawnPosition(human.CityId, out var pos, out _))
            {
                goals.Add(pos);
                continue;
            }

            if (simulation.TryGetNetworkPlayerPosition(human.PlayerId, out var tankPos))
            {
                goals.Add(tankPos);
            }
        }

        if (goals.Count == 0)
        {
            return;
        }

        var goalIndex = 0;
        foreach (var city in _cities)
        {
            var goal = goals[goalIndex % goals.Count];
            goalIndex++;
            foreach (var bot in city.Bots)
            {
                if (bot.Role == BotRoles.Attack)
                {
                    simulation.SetNetworkBotGoal(bot.PlayerId, goal);
                }
            }
        }
    }

    private bool TrySpawnCity(
        GameSimulation simulation,
        CityRegistry cities,
        CityMayorRegistry mayors,
        Func<byte> allocatePlayerId,
        IReadOnlyList<(byte PlayerId, byte CityId, bool InGame)> humans,
        HashSet<byte> reserved,
        out AiCitySlot slot,
        out string message)
    {
        slot = null!;
        if (!TryPickCity(cities, mayors, humans, reserved, out var cityId))
        {
            message = "No free city available for AI.";
            return false;
        }

        if (!simulation.TryGetCityRespawnPosition(cityId, out var spawn, out _))
        {
            if (!CityBuildInitializer.TryGetCommandCenterGridForCity(
                    cityId,
                    simulation.TileMap,
                    out var gridX,
                    out var gridY))
            {
                message = "AI City spawn failed (no CC).";
                return false;
            }

            spawn = simulation.FindOpenTankSpawnNear(
                CommandCenterLookup.GetRespawnPositionFromGridAnchor(gridX, gridY));
        }

        simulation.EnsureCityBuild(cityId);
        var aggro = AggroFor(Stance);
        var bots = new List<AiBotPlayer>();

        var mayorId = allocatePlayerId();
        if (mayorId == 0)
        {
            message = "No free player slots for AI mayor.";
            return false;
        }

        simulation.CreateNetworkBotPlayer(spawn, mayorId, cityId, isMayor: true, BotRoles.Defend, aggro);
        mayors.Assign(cityId, mayorId);
        bots.Add(new AiBotPlayer(mayorId, $"AI-Mayor-{cityId}", cityId, IsMayor: true, BotRoles.Defend));

        for (var i = 0; i < DefaultSoldierCount; i++)
        {
            var soldierId = allocatePlayerId();
            if (soldierId == 0)
            {
                break;
            }

            var role = RoleForSoldier(Stance, i);
            var offset = new Vector2((i + 1) * 48f, (i % 2) * 48f);
            simulation.CreateNetworkBotPlayer(
                spawn + offset,
                soldierId,
                cityId,
                isMayor: false,
                role,
                aggro);
            bots.Add(new AiBotPlayer(soldierId, $"AI-Sold-{cityId}-{i + 1}", cityId, IsMayor: false, role));
        }

        slot = new AiCitySlot(cityId, Stance, bots);
        message = $"AI city {CityCatalog.GetName(cityId)}";
        return true;
    }

    private List<AiBotPlayer> TakeAllBots()
    {
        var bots = Bots.ToList();
        _cities.Clear();
        return bots;
    }

    private static void Despawn(
        GameSimulation simulation,
        CityMayorRegistry mayors,
        Action<byte> releasePlayerId,
        IReadOnlyList<AiBotPlayer> bots)
    {
        foreach (var mayor in bots.Where(bot => bot.IsMayor))
        {
            mayors.Remove(mayor.CityId, mayor.PlayerId);
        }

        foreach (var bot in bots)
        {
            simulation.TryRemoveNetworkPlayer(bot.PlayerId);
            releasePlayerId(bot.PlayerId);
        }
    }

    private static byte RoleForSoldier(AiCityStance stance, int soldierIndex) =>
        stance switch
        {
            AiCityStance.LeanDefense => BotRoles.Defend,
            AiCityStance.LeanOffense => BotRoles.Attack,
            _ => soldierIndex == 0 ? BotRoles.Defend : BotRoles.Attack,
        };

    private static float AggroFor(AiCityStance stance) =>
        stance switch
        {
            AiCityStance.LeanDefense => 1600f,
            AiCityStance.LeanOffense => 3600f,
            _ => 2400f,
        };

    public static string StanceLabel(AiCityStance stance) =>
        stance switch
        {
            AiCityStance.LeanDefense => "Lean defense",
            AiCityStance.LeanOffense => "Lean offense",
            _ => "Balanced",
        };

    private static bool TryPickCity(
        CityRegistry cities,
        CityMayorRegistry mayors,
        IEnumerable<(byte PlayerId, byte CityId, bool InGame)> humans,
        HashSet<byte> reserved,
        out byte cityId)
    {
        var occupied = new HashSet<byte>(reserved);
        foreach (var human in humans)
        {
            if (human.InGame)
            {
                occupied.Add(human.CityId);
            }
        }

        foreach (var candidate in CityRegistry.StartingCityOptions)
        {
            if (mayors.HasMayor(candidate) || occupied.Contains(candidate))
            {
                continue;
            }

            cityId = candidate;
            return true;
        }

        foreach (var candidate in CityRegistry.EnumerateSpiralCityIds(cities.StartingCityId, citiesWanted: 32))
        {
            if (mayors.HasMayor(candidate) || occupied.Contains(candidate))
            {
                continue;
            }

            cityId = candidate;
            return true;
        }

        cityId = 0;
        return false;
    }

    private sealed class AiCitySlot(byte cityId, AiCityStance stance, List<AiBotPlayer> bots)
    {
        public byte CityId { get; } = cityId;

        public AiCityStance Stance { get; } = stance;

        public List<AiBotPlayer> Bots { get; } = bots;
    }
}

public sealed record AiBotPlayer(
    byte PlayerId,
    string DisplayName,
    byte CityId,
    bool IsMayor,
    byte Role);
