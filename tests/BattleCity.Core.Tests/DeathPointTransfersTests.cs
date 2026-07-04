using System.Net;
using System.Net.Sockets;

using BattleCity.Server;

using Xunit;

namespace BattleCity.Core.Tests;

public class DeathPointTransfersTests
{
    [Fact]
    public void Apply_TransfersPointsToKillerCityAllies()
    {
        using var victim = CreateSession(playerId: 1, cityId: 0, points: 150);
        using var ally = CreateSession(playerId: 2, cityId: 1, points: 50);
        using var outsider = CreateSession(playerId: 3, cityId: 2, points: 50);
        var updates = new List<(byte PlayerId, int Delta)>();

        DeathPointTransfers.Apply(
            victim.Session,
            killerCityId: 1,
            [victim.Session, ally.Session, outsider.Session],
            (session, delta) => updates.Add((session.PlayerId, delta)));

        Assert.Equal(148, victim.Session.Points);
        Assert.Equal(52, ally.Session.Points);
        Assert.Equal(50, outsider.Session.Points);
        Assert.Equal(3, updates.Count);
        Assert.Equal(2, updates.Count(entry => entry.PlayerId == 1));
        Assert.Contains(updates, entry => entry is { PlayerId: 1, Delta: -2 });
        Assert.Contains(updates, entry => entry is { PlayerId: 2, Delta: 2 });
    }

    [Fact]
    public void Apply_SkipsTransferWhenVictimBelowThreshold()
    {
        using var victim = CreateSession(playerId: 1, cityId: 0, points: 100);
        using var ally = CreateSession(playerId: 2, cityId: 1, points: 50);
        var updates = new List<(byte PlayerId, int Delta)>();

        DeathPointTransfers.Apply(victim.Session, killerCityId: 1, [victim.Session, ally.Session], (session, delta) =>
            updates.Add((session.PlayerId, delta)));

        Assert.Equal(100, victim.Session.Points);
        Assert.Equal(50, ally.Session.Points);
        Assert.Single(updates);
        Assert.Equal((1, 0), updates[0]);
    }

    [Fact]
    public void Apply_SkipsTransferForFriendlyFire()
    {
        using var victim = CreateSession(playerId: 1, cityId: 1, points: 200);
        var updates = new List<(byte PlayerId, int Delta)>();

        DeathPointTransfers.Apply(victim.Session, killerCityId: 1, [victim.Session], (session, delta) =>
            updates.Add((session.PlayerId, delta)));

        Assert.Equal(200, victim.Session.Points);
        Assert.Single(updates);
        Assert.Equal((1, 0), updates[0]);
    }

    private static SessionFixture CreateSession(byte playerId, byte cityId, int points)
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
                Points = points,
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
