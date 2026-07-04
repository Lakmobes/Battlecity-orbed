using BattleCity.Shared.Constants;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Server;

public sealed class CityRegistry
{
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
        var sent = new HashSet<byte>();

        if (!mayors.HasMayor(defaultCityId))
        {
            sent.Add(defaultCityId);
            yield return new ServerAddRemCityPacket(defaultCityId, ServerAddRemCityPacket.NoMayor, 0);
        }

        foreach (var (cityId, count) in inGameCounts)
        {
            if (!mayors.HasMayor(cityId) || count >= GameConstants.MaxPlayersPerCity)
            {
                continue;
            }

            if (!sent.Add(cityId))
            {
                continue;
            }

            mayors.TryGetMayorPlayerId(cityId, out var mayorId);
            yield return new ServerAddRemCityPacket(cityId, mayorId, (byte)count);
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
