using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Server;

public sealed class CityRegistry
{
    /// <summary>Always offer at least this many joinable meeting-room entries.</summary>
    public const int MinimumJoinableCities = 3;

    /// <summary>
    /// Preferred empty-city order (legacy starting-CC neighborhood around Buenos Aires).
    /// </summary>
    private static readonly byte[] PreferredEmptyCities =
    [
        27, 26, 28, 19, 20, 18, 34, 35, 36, 0, 1, 2, 3, 4, 5,
    ];

    private readonly Dictionary<byte, CitySlot> _slots = new();

    public CitySlot GetOrCreate(byte cityId) =>
        _slots.TryGetValue(cityId, out var slot) ? slot : _slots[cityId] = new CitySlot(cityId);

    public void ClearHiring(byte cityId)
    {
        if (_slots.TryGetValue(cityId, out var slot))
        {
            slot.HiringApplicantId = null;
        }
    }

    public bool TryFindSlotByApplicant(byte applicantId, out CitySlot slot)
    {
        foreach (var candidate in _slots.Values)
        {
            if (candidate.HiringApplicantId == applicantId)
            {
                slot = candidate;
                return true;
            }
        }

        slot = null!;
        return false;
    }

    public IEnumerable<ServerAddRemCityPacket> BuildCityList(
        CityMayorRegistry mayors,
        IEnumerable<ClientSession> sessions,
        byte defaultCityId)
    {
        var inGameCounts = CountInGamePlayersByCity(sessions);
        var results = new List<ServerAddRemCityPacket>();
        var sent = new HashSet<byte>();

        // (a) Every city that already has a mayor and can still take commandos.
        foreach (var cityId in mayors.GetMayoredCityIds())
        {
            inGameCounts.TryGetValue(cityId, out var count);
            if (count >= GameConstants.MaxPlayersPerCity)
            {
                continue;
            }

            if (GetOrCreate(cityId).DenyApplicants)
            {
                continue;
            }

            if (!mayors.TryGetMayorPlayerId(cityId, out var mayorId))
            {
                continue;
            }

            if (!sent.Add(cityId))
            {
                continue;
            }

            results.Add(new ServerAddRemCityPacket(cityId, mayorId, (byte)count));
        }

        // (b) Enough empty "Mayor required" slots so the list always has ≥ 3 joinable cities.
        foreach (var cityId in EnumerateEmptyCityCandidates(defaultCityId))
        {
            if (results.Count >= MinimumJoinableCities)
            {
                break;
            }

            if (mayors.HasMayor(cityId) || !CityCatalog.IsValidCityId(cityId) || !sent.Add(cityId))
            {
                continue;
            }

            results.Add(new ServerAddRemCityPacket(cityId, ServerAddRemCityPacket.NoMayor, 0));
        }

        return results;
    }

    private static IEnumerable<byte> EnumerateEmptyCityCandidates(byte defaultCityId)
    {
        yield return defaultCityId;

        foreach (var cityId in PreferredEmptyCities)
        {
            if (cityId != defaultCityId)
            {
                yield return cityId;
            }
        }

        for (var id = 0; id < CityCatalog.Names.Count; id++)
        {
            yield return (byte)id;
        }
    }

    private static Dictionary<byte, int> CountInGamePlayersByCity(IEnumerable<ClientSession> sessions)
    {
        var counts = new Dictionary<byte, int>();
        foreach (var session in sessions)
        {
            if (session.State != PlayerSessionState.InGame)
            {
                continue;
            }

            counts.TryGetValue(session.CityId, out var count);
            counts[session.CityId] = count + 1;
        }

        return counts;
    }
}

public sealed class CitySlot
{
    public CitySlot(byte cityId) => CityId = cityId;

    public byte CityId { get; }

    public byte? HiringApplicantId { get; set; }

    /// <summary>When true, the mayor auto-declines new applicants (legacy notHiring).</summary>
    public bool DenyApplicants { get; set; }
}
