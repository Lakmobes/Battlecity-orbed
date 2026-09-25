using System.Net;
using System.Net.Sockets;

using BattleCity.Server;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Core.Tests;

public class CityRegistryTests
{
    [Fact]
    public void ComputeCitiesWanted_MatchesLegacyFormula()
    {
        Assert.Equal(6, CityRegistry.ComputeCitiesWanted(0));
        Assert.Equal(6, CityRegistry.ComputeCitiesWanted(4));
        Assert.Equal(7, CityRegistry.ComputeCitiesWanted(5));
        Assert.Equal(8, CityRegistry.ComputeCitiesWanted(10));
    }

    [Fact]
    public void EnumerateSpiralCityIds_FromBuenosAires_VisitsExpectedNeighbors()
    {
        // City 27 = row 3, col 3. First legs: right 28, down 36, left 35/34, up 26/18.
        var visited = CityRegistry.EnumerateSpiralCityIds(startingCityId: 27, citiesWanted: 6).ToList();

        Assert.Equal(6, visited.Count);
        Assert.Equal((byte)27, visited[0]);
        Assert.Equal((byte)28, visited[1]);
        Assert.Equal((byte)36, visited[2]);
        Assert.Equal((byte)35, visited[3]);
        Assert.Equal((byte)34, visited[4]);
        Assert.Equal((byte)26, visited[5]);
    }

    [Fact]
    public void BuildCityList_WithNoPlayers_OffersSixEmptyCitiesFromStartingCity()
    {
        var registry = new CityRegistry();
        registry.SetStartingCityForTests(27);
        var mayors = new CityMayorRegistry();

        var entries = registry.BuildCityList(mayors, []).ToList();

        Assert.Equal(6, entries.Count);
        Assert.All(entries, entry => Assert.True(entry.NeedsMayor));
        Assert.Equal((byte)27, entries[0].CityId);
        Assert.Contains(entries, entry => entry.CityId == 28);
        Assert.DoesNotContain(entries, entry => entry.CityId == 0);
    }

    [Fact]
    public void BuildCityList_IncludesHiringMayoredCityWithPlayers()
    {
        var registry = new CityRegistry();
        registry.SetStartingCityForTests(27);
        var mayors = new CityMayorRegistry();
        mayors.Assign(27, 2);

        using var inGameMayor = CreateSession(2, cityId: 27, PlayerSessionState.InGame);
        var entries = registry.BuildCityList(mayors, [inGameMayor.Session]).ToList();

        var mayored = Assert.Single(entries, entry => entry.CityId == 27 && !entry.NeedsMayor);
        Assert.Equal((byte)2, mayored.MayorPlayerId);
        Assert.Equal((byte)1, mayored.PlayerCount);

        // Spiral still fills empty slots; 27 is mayored so spiral budget still visits it but does not re-send.
        Assert.DoesNotContain(entries, entry => entry.CityId == 27 && entry.NeedsMayor);
        Assert.Contains(entries, entry => entry.NeedsMayor && entry.CityId == 28);
    }

    [Fact]
    public void BuildCityList_SkipsMayoredCityWhenDenyApplicants()
    {
        var registry = new CityRegistry();
        registry.SetStartingCityForTests(18);
        var mayors = new CityMayorRegistry();
        mayors.Assign(18, 2);
        registry.GetOrCreate(18).DenyApplicants = true;

        using var inGameMayor = CreateSession(2, cityId: 18, PlayerSessionState.InGame);
        var entries = registry.BuildCityList(mayors, [inGameMayor.Session]).ToList();

        Assert.DoesNotContain(entries, entry => entry.CityId == 18 && !entry.NeedsMayor);
        Assert.All(entries, entry => Assert.True(entry.NeedsMayor));
    }

    [Fact]
    public void BuildCityList_SkipsMayoredCityWithZeroInGamePlayers()
    {
        // Legacy SendCommandos requires playerCount > 0.
        var registry = new CityRegistry();
        registry.SetStartingCityForTests(27);
        var mayors = new CityMayorRegistry();
        mayors.Assign(27, 2);

        using var meetingOnly = CreateSession(2, cityId: 27, PlayerSessionState.Meeting);
        var entries = registry.BuildCityList(mayors, [meetingOnly.Session]).ToList();

        Assert.DoesNotContain(entries, entry => entry.CityId == 27 && !entry.NeedsMayor);
    }

    [Fact]
    public void BuildCityList_SpiralSkipsTakenCitiesButStillCountsBudget()
    {
        var registry = new CityRegistry();
        registry.SetStartingCityForTests(27);
        var mayors = new CityMayorRegistry();
        // Take the first spiral neighbors so they consume budget without being listed as empty.
        mayors.Assign(28, 3);
        mayors.Assign(36, 4);

        var entries = registry.BuildCityList(mayors, []).ToList();

        // citiesWanted=6 visits: 27,28,36,35,34,26 — but 28 and 36 have mayors → 4 empty packets.
        Assert.Equal(4, entries.Count);
        Assert.All(entries, entry => Assert.True(entry.NeedsMayor));
        Assert.Equal(new byte[] { 27, 35, 34, 26 }, entries.Select(e => e.CityId).ToArray());
    }

    [Fact]
    public void ResetStartingCity_PicksFromBuenosAiresNeighborhood()
    {
        var registry = new CityRegistry();
        var seen = new HashSet<byte>();
        for (var i = 0; i < 40; i++)
        {
            registry.ResetStartingCity(new Random(i));
            seen.Add(registry.StartingCityId);
            Assert.Contains(registry.StartingCityId, CityRegistry.StartingCityOptions);
        }

        Assert.True(seen.Count >= 3);
    }

    [Fact]
    public void StartingCityOptions_MatchLegacyArray()
    {
        Assert.Equal(
            new byte[] { 18, 19, 20, 26, 27, 28, 34, 35, 36 },
            CityRegistry.StartingCityOptions);
        Assert.Equal(GameConstants.MaxPlayersPerCity, 4);
    }

    private static SessionFixture CreateSession(byte playerId, byte cityId, PlayerSessionState state)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = new TcpClient();
        client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        var serverSocket = listener.AcceptTcpClient();
        listener.Stop();

        return new SessionFixture(
            new ClientSession(playerId, serverSocket)
            {
                CityId = cityId,
                State = state,
            },
            client,
            serverSocket);
    }

    private sealed class SessionFixture : IDisposable
    {
        public SessionFixture(ClientSession session, TcpClient client, TcpClient serverSocket)
        {
            Session = session;
            _client = client;
            _serverSocket = serverSocket;
        }

        public ClientSession Session { get; }

        private readonly TcpClient _client;
        private readonly TcpClient _serverSocket;

        public void Dispose()
        {
            Session.Dispose();
            _client.Dispose();
            _serverSocket.Dispose();
        }
    }
}
