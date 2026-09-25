using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Server;

/// <summary>
/// Meeting-room city list — mirrors legacy <c>CSend::SendCityList</c>
/// (<c>SendCommandos</c> + spiral <c>SendTheCities</c>).
/// </summary>
public sealed class CityRegistry
{
    /// <summary>
    /// Legacy <c>startingCityOptions</c> — Buenos Aires neighborhood on the 8×8 city grid.
    /// </summary>
    public static readonly byte[] StartingCityOptions =
    [
        18, 19, 20, 26, 27, 28, 34, 35, 36,
    ];

    private readonly Dictionary<byte, CitySlot> _slots = new();

    public byte StartingCityId { get; private set; } = 27;

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

    /// <summary>Legacy <c>CSend::ResetStartingCC</c> — pick a BA-neighborhood seed city.</summary>
    public void ResetStartingCity(Random? random = null)
    {
        random ??= Random.Shared;
        StartingCityId = StartingCityOptions[random.Next(StartingCityOptions.Length)];
    }

    public void SetStartingCityForTests(byte cityId)
    {
        if (!CityCatalog.IsValidCityId(cityId))
        {
            throw new ArgumentOutOfRangeException(nameof(cityId));
        }

        StartingCityId = cityId;
    }

    /// <summary>
    /// Legacy <c>citiesWanted = ⌊players/5⌋ + 6</c> where players are meeting/interview/logged-in/in-game.
    /// </summary>
    public static int ComputeCitiesWanted(int lobbyPlayerCount) =>
        (Math.Max(0, lobbyPlayerCount) / 5) + 6;

    public IEnumerable<ServerAddRemCityPacket> BuildCityList(
        CityMayorRegistry mayors,
        IEnumerable<ClientSession> sessions)
    {
        var sessionList = sessions as IList<ClientSession> ?? sessions.ToList();
        var inGameCounts = CountInGamePlayersByCity(sessionList);
        var results = new List<ServerAddRemCityPacket>();
        var sent = new HashSet<byte>();

        // (a) SendCommandos — mayor'd cities that are hiring and not empty/full.
        foreach (var cityId in mayors.GetMayoredCityIds().OrderBy(id => id))
        {
            inGameCounts.TryGetValue(cityId, out var count);
            if (count <= 0 || count >= GameConstants.MaxPlayersPerCity)
            {
                continue;
            }

            if (GetOrCreate(cityId).DenyApplicants)
            {
                continue;
            }

            if (!mayors.TryGetMayorPlayerId(cityId, out var mayorId) || !sent.Add(cityId))
            {
                continue;
            }

            results.Add(new ServerAddRemCityPacket(cityId, mayorId, (byte)count));
        }

        // (b) SendTheCities — spiral empty (needs-mayor) cities from startingCity.
        var citiesWanted = ComputeCitiesWanted(CountLobbyPlayers(sessionList));
        foreach (var cityId in EnumerateSpiralCityIds(StartingCityId, citiesWanted))
        {
            if (mayors.HasMayor(cityId) || !sent.Add(cityId))
            {
                continue;
            }

            results.Add(new ServerAddRemCityPacket(cityId, ServerAddRemCityPacket.NoMayor, 0));
        }

        return results;
    }

    /// <summary>
    /// Legacy spiral from <c>CSend::SendTheCities</c>. Yields up to <paramref name="citiesWanted"/>
    /// visited indices (including ones that already have mayors — those consume budget but are not sent).
    /// Callers skip mayored cities when building packets.
    /// </summary>
    public static IEnumerable<byte> EnumerateSpiralCityIds(byte startingCityId, int citiesWanted)
    {
        if (citiesWanted <= 0 || !CityCatalog.IsValidCityId(startingCityId))
        {
            yield break;
        }

        var citiesFound = 0;
        var counter = 0;
        var targetCity = (int)startingCityId;
        var isNeighbor = true;

        // Starting city always consumes one budget slot (sent only if empty).
        yield return startingCityId;
        citiesFound++;

        while (citiesFound < citiesWanted)
        {
            var steps = (counter / 2) + 1;
            for (var i = 0; i < steps; i++)
            {
                var lastCity = targetCity;
                targetCity += SpiralStep(counter);

                // Legacy edge-wrap toggle.
                if ((targetCity % 8) != ((lastCity % 8) + 1 - (counter % 4))
                    && (targetCity / 8) != ((lastCity / 8) + 2 - (counter % 4)))
                {
                    isNeighbor = !isNeighbor;
                }

                if (CityCatalog.IsValidCityId(targetCity) && isNeighbor)
                {
                    yield return (byte)targetCity;
                    citiesFound++;
                    if (citiesFound >= citiesWanted)
                    {
                        yield break;
                    }
                }
            }

            counter++;

            // Safety: avoid infinite loop if spiral math fails to find enough valid neighbors.
            if (counter > 256)
            {
                yield break;
            }
        }
    }

    /// <summary>Right, down, left, up deltas matching legacy counter formula.</summary>
    private static int SpiralStep(int counter) =>
        1
        + (7 * (counter % 2))
        - (2 * ((counter / 2) % 2))
        - (14 * ((counter / 2) % 2) * (counter % 2));

    private static int CountLobbyPlayers(IEnumerable<ClientSession> sessions)
    {
        var count = 0;
        foreach (var session in sessions)
        {
            if (session.State is PlayerSessionState.Meeting
                or PlayerSessionState.Interview
                or PlayerSessionState.LoggedIn
                or PlayerSessionState.InGame)
            {
                count++;
            }
        }

        return count;
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
