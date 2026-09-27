using System.Numerics;

using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Server;

/// <summary>Server-side scripted enemy city (mayor + soldiers) for solo / light MP.</summary>
public sealed class AiCityController
{
    public const int DefaultSoldierCount = 3;

    private readonly List<AiBotPlayer> _bots = [];

    public bool IsEnabled => _bots.Count > 0;

    public byte CityId { get; private set; }

    public IReadOnlyList<AiBotPlayer> Bots => _bots;

    public void Clear()
    {
        _bots.Clear();
        CityId = 0;
    }

    public bool TryEnable(
        GameSimulation simulation,
        CityRegistry cities,
        CityMayorRegistry mayors,
        Func<byte> allocatePlayerId,
        Action<byte> releasePlayerId,
        IEnumerable<(byte PlayerId, byte CityId, bool InGame)> humanPlayers,
        out string message)
    {
        if (IsEnabled)
        {
            message = "AI City already enabled.";
            return false;
        }

        if (!TryPickCity(cities, mayors, humanPlayers, out var cityId))
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

        var mayorId = allocatePlayerId();
        if (mayorId == 0)
        {
            message = "No free player slots for AI mayor.";
            return false;
        }

        simulation.CreateNetworkBotPlayer(
            spawn,
            mayorId,
            cityId,
            isMayor: true,
            BotRoles.Defend);
        mayors.Assign(cityId, mayorId);
        _bots.Add(new AiBotPlayer(mayorId, $"AI-Mayor-{cityId}", cityId, IsMayor: true, BotRoles.Defend));

        for (var i = 0; i < DefaultSoldierCount; i++)
        {
            var soldierId = allocatePlayerId();
            if (soldierId == 0)
            {
                break;
            }

            var offset = new Vector2((i + 1) * 48f, (i % 2) * 48f);
            var role = i == 0 ? BotRoles.Defend : BotRoles.Attack;
            simulation.CreateNetworkBotPlayer(
                spawn + offset,
                soldierId,
                cityId,
                isMayor: false,
                role);
            _bots.Add(new AiBotPlayer(soldierId, $"AI-Sold-{cityId}-{i + 1}", cityId, IsMayor: false, role));
        }

        CityId = cityId;
        message = $"AI City enabled at {CityCatalog.GetName(cityId)} ({cityId}) with {_bots.Count} bots.";
        return true;
    }

    public void Disable(
        GameSimulation simulation,
        CityMayorRegistry mayors,
        Action<byte> releasePlayerId)
    {
        if (!IsEnabled)
        {
            return;
        }

        var mayor = _bots.FirstOrDefault(bot => bot.IsMayor);
        if (mayor is not null)
        {
            mayors.Remove(CityId, mayor.PlayerId);
        }

        foreach (var bot in _bots)
        {
            simulation.TryRemoveNetworkPlayer(bot.PlayerId);
            releasePlayerId(bot.PlayerId);
        }

        Clear();
    }

    public void RefreshAttackGoals(
        GameSimulation simulation,
        IEnumerable<(byte PlayerId, byte CityId, bool InGame)> humanPlayers)
    {
        if (!IsEnabled)
        {
            return;
        }

        Vector2? raidTarget = null;
        foreach (var human in humanPlayers)
        {
            if (!human.InGame || human.CityId == CityId)
            {
                continue;
            }

            if (simulation.TryGetCityRespawnPosition(human.CityId, out var pos, out _))
            {
                raidTarget = pos;
                break;
            }

            if (simulation.TryGetNetworkPlayerPosition(human.PlayerId, out var tankPos))
            {
                raidTarget = tankPos;
                break;
            }
        }

        if (raidTarget is not { } goal)
        {
            return;
        }

        foreach (var bot in _bots)
        {
            if (bot.Role == BotRoles.Attack)
            {
                simulation.SetNetworkBotGoal(bot.PlayerId, goal);
            }
        }
    }

    private static bool TryPickCity(
        CityRegistry cities,
        CityMayorRegistry mayors,
        IEnumerable<(byte PlayerId, byte CityId, bool InGame)> humans,
        out byte cityId)
    {
        var occupied = new HashSet<byte>();
        foreach (var human in humans)
        {
            if (human.InGame)
            {
                occupied.Add(human.CityId);
            }
        }

        // Prefer BA-neighborhood options that are empty and not mayored.
        foreach (var candidate in CityRegistry.StartingCityOptions)
        {
            if (mayors.HasMayor(candidate) || occupied.Contains(candidate))
            {
                continue;
            }

            cityId = candidate;
            return true;
        }

        // Fall back to spiral from current starting city.
        foreach (var candidate in CityRegistry.EnumerateSpiralCityIds(cities.StartingCityId, citiesWanted: 24))
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
}

public sealed record AiBotPlayer(
    byte PlayerId,
    string DisplayName,
    byte CityId,
    bool IsMayor,
    byte Role);
