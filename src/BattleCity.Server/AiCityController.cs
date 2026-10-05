using System.Numerics;

using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Network.Packets;

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
        string? message,
        IReadOnlyList<ServerBuildingPacket>? buildings = null,
        IReadOnlyList<ServerAddItemPacket>? walls = null)
    {
        Spawned = spawned;
        Removed = removed;
        Message = message;
        Buildings = buildings ?? [];
        Walls = walls ?? [];
    }

    public IReadOnlyList<AiBotPlayer> Spawned { get; }

    public IReadOnlyList<AiBotPlayer> Removed { get; }

    public string? Message { get; }

    public IReadOnlyList<ServerBuildingPacket> Buildings { get; }

    public IReadOnlyList<ServerAddItemPacket> Walls { get; }

    public static AiCityPresenceChange None { get; } = new([], [], null);
}

/// <summary>
/// Scripted enemy cities. The host arms them; tanks only exist while at least one human is in a city.
/// </summary>
public sealed class AiCityController
{
    public const int DefaultSoldierCount = 3;
    public const int MaxCities = 4;

    /// <summary>Wait this long after an orb before the city is built again.</summary>
    public const float RedeployDelaySeconds = 30f;

    /// <summary>If a player is standing on the pad, try again after this long.</summary>
    public const float RedeployRetrySeconds = 5f;

    /// <summary>Chebyshev tiles around the command-center anchor that count as "in the way".</summary>
    public const int RedeployClearanceTiles = 6;

    private readonly List<AiCitySlot> _cities = [];
    private readonly List<PendingRedeploy> _redeploys = [];
    private int _appliedCityCount = -1;
    private AiCityStance _appliedStance;
    private bool _suppressRefill;

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
        _suppressRefill = false;
        _redeploys.Clear();
    }

    public void Disarm()
    {
        IsArmed = false;
        _redeploys.Clear();
    }

    public static bool TankBlocksRedeploy(int anchorX, int anchorY, int tankGridX, int tankGridY)
    {
        var dx = Math.Abs(tankGridX - anchorX);
        var dy = Math.Abs(tankGridY - anchorY);
        return Math.Max(dx, dy) <= RedeployClearanceTiles;
    }

    public void AdvanceRedeploy(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        for (var i = 0; i < _redeploys.Count; i++)
        {
            _redeploys[i].Seconds -= deltaSeconds;
        }
    }

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
            _redeploys.Clear();
            var why = !IsArmed
                ? "AI cities disarmed."
                : "AI cities standing down (no players in a city).";
            return new AiCityPresenceChange([], removed, why);
        }

        if (_suppressRefill && _cities.Count == 0)
        {
            return AiCityPresenceChange.None;
        }

        if (_cities.Count > 0
            && _appliedCityCount == RequestedCityCount
            && _appliedStance == Stance)
        {
            return AiCityPresenceChange.None;
        }

        var cleared = TakeAllBots();
        Despawn(simulation, mayors, releasePlayerId, cleared);
        _redeploys.Clear();

        var spawned = new List<AiBotPlayer>();
        var buildings = new List<ServerBuildingPacket>();
        var walls = new List<ServerAddItemPacket>();
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
                    buildings,
                    walls,
                    preferredCityId: null,
                    isBlocked: null,
                    out var slot,
                    out _,
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
        return new AiCityPresenceChange(spawned, cleared, message, buildings, walls);
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
        List<ServerBuildingPacket> buildings,
        List<ServerAddItemPacket> walls,
        byte? preferredCityId,
        Func<byte, bool>? isBlocked,
        out AiCitySlot slot,
        out string message,
        out bool waitingOnPlayer)
    {
        slot = null!;
        waitingOnPlayer = false;
        byte cityId;
        if (preferredCityId is byte preferredCity
            && !reserved.Contains(preferredCity)
            && !mayors.HasMayor(preferredCity)
            && !humans.Any(human => human.InGame && human.CityId == preferredCity))
        {
            if (isBlocked?.Invoke(preferredCity) == true)
            {
                waitingOnPlayer = true;
                message = "A player is in the way.";
                return false;
            }

            cityId = preferredCity;
        }
        else if (!TryPickCity(cities, mayors, humans, reserved, isBlocked, out cityId))
        {
            waitingOnPlayer = isBlocked is not null
                && TryPickCity(cities, mayors, humans, reserved, isBlocked: null, out _);
            message = waitingOnPlayer
                ? "A player is in the way."
                : "No free city available for AI.";
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
        var placed = simulation.PlaceAiCityBase(cityId);
        buildings.AddRange(placed.Buildings);
        walls.AddRange(placed.Walls);
        spawn = simulation.FindOpenTankSpawnNear(spawn);

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
                simulation.FindOpenTankSpawnNear(spawn + offset),
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

    /// <summary>
    /// An orb retired this city. Tanks leave now; the city is rebuilt after <see cref="RedeployDelaySeconds"/>
    /// if a player is not standing on the command center.
    /// </summary>
    public IReadOnlyList<AiBotPlayer> RetireCity(
        byte cityId,
        GameSimulation simulation,
        CityMayorRegistry mayors,
        Action<byte> releasePlayerId)
    {
        var index = _cities.FindIndex(city => city.CityId == cityId);
        if (index < 0)
        {
            return [];
        }

        var bots = _cities[index].Bots.ToList();
        _cities.RemoveAt(index);
        Despawn(simulation, mayors, releasePlayerId, bots);
        _redeploys.Add(new PendingRedeploy(cityId, RedeployDelaySeconds));
        if (_cities.Count == 0)
        {
            _suppressRefill = true;
        }

        return bots;
    }

    /// <summary>
    /// Spawns one replacement city when a redeploy timer has elapsed and some command center is clear.
    /// </summary>
    public AiCityPresenceChange TryRedeployDue(
        GameSimulation simulation,
        CityRegistry cities,
        CityMayorRegistry mayors,
        Func<byte> allocatePlayerId,
        Action<byte> releasePlayerId,
        IEnumerable<(byte PlayerId, byte CityId, bool InGame)> humanPlayers,
        Func<byte, bool> cityIsBlocked)
    {
        var due = _redeploys.FindIndex(pending => pending.Seconds <= 0f);
        if (due < 0 || !IsArmed)
        {
            return AiCityPresenceChange.None;
        }

        var humans = humanPlayers as IReadOnlyList<(byte PlayerId, byte CityId, bool InGame)>
            ?? humanPlayers.ToList();
        if (!humans.Any(human => human.InGame) || _cities.Count >= RequestedCityCount)
        {
            _redeploys.RemoveAt(due);
            return AiCityPresenceChange.None;
        }

        var buildings = new List<ServerBuildingPacket>();
        var walls = new List<ServerAddItemPacket>();
        var reserved = new HashSet<byte>(_cities.Select(city => city.CityId));
        if (!TrySpawnCity(
                simulation,
                cities,
                mayors,
                allocatePlayerId,
                humans,
                reserved,
                buildings,
                walls,
                _redeploys[due].CityId,
                cityIsBlocked,
                out var slot,
                out _,
                out var waitingOnPlayer))
        {
            _redeploys[due].Seconds = RedeployRetrySeconds;
            return waitingOnPlayer
                ? new AiCityPresenceChange([], [], "AI city redeploy waiting — a player is in the way.")
                : AiCityPresenceChange.None;
        }

        _redeploys.RemoveAt(due);
        _cities.Add(slot);
        if (_cities.Count > 0)
        {
            _suppressRefill = false;
        }

        RefreshAttackGoals(simulation, humans);
        var name = CityCatalog.GetName(slot.CityId);
        return new AiCityPresenceChange(
            slot.Bots,
            [],
            $"AI city redeployed: {name} ({slot.CityId}).",
            buildings,
            walls);
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
        Func<byte, bool>? isBlocked,
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
            if (mayors.HasMayor(candidate) || occupied.Contains(candidate) || isBlocked?.Invoke(candidate) == true)
            {
                continue;
            }

            cityId = candidate;
            return true;
        }

        foreach (var candidate in CityRegistry.EnumerateSpiralCityIds(cities.StartingCityId, citiesWanted: 32))
        {
            if (mayors.HasMayor(candidate) || occupied.Contains(candidate) || isBlocked?.Invoke(candidate) == true)
            {
                continue;
            }

            cityId = candidate;
            return true;
        }

        cityId = 0;
        return false;
    }

    private sealed class PendingRedeploy(byte cityId, float seconds)
    {
        public byte CityId { get; } = cityId;

        public float Seconds { get; set; } = seconds;
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
