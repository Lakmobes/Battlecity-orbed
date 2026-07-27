using System.Net;
using System.Net.Sockets;

using BattleCity.Server;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Core.Tests;

public class CityRegistryTests
{
    [Fact]
    public void BuildCityList_AlwaysOffersAtLeastThreeEmptyCitiesWhenNoMayors()
    {
        var registry = new CityRegistry();
        var mayors = new CityMayorRegistry();

        var entries = registry.BuildCityList(mayors, [], defaultCityId: 0).ToList();

        Assert.True(entries.Count >= CityRegistry.MinimumJoinableCities);
        Assert.All(entries, entry => Assert.True(entry.NeedsMayor));
        Assert.Contains(entries, entry => entry.CityId == 0);
    }

    [Fact]
    public void BuildCityList_IncludesMayoredCityAndPadsEmptySlotsToThree()
    {
        var registry = new CityRegistry();
        var mayors = new CityMayorRegistry();
        mayors.Assign(27, 2);

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var serverSocket = listener.AcceptTcpClient();
        using var inGameMayor = new ClientSession(2, serverSocket)
        {
            State = PlayerSessionState.InGame,
            CityId = 27,
        };

        var entries = registry.BuildCityList(mayors, [inGameMayor], defaultCityId: 0).ToList();

        Assert.True(entries.Count >= CityRegistry.MinimumJoinableCities);
        var mayored = Assert.Single(entries, entry => entry.CityId == 27);
        Assert.Equal((byte)2, mayored.MayorPlayerId);
        Assert.Equal((byte)1, mayored.PlayerCount);
        Assert.Equal(2, entries.Count(entry => entry.NeedsMayor));
    }

    [Fact]
    public void BuildCityList_SkipsMayoredCityWhenDenyApplicants()
    {
        var registry = new CityRegistry();
        var mayors = new CityMayorRegistry();
        mayors.Assign(0, 2);
        registry.GetOrCreate(0).DenyApplicants = true;

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var serverSocket = listener.AcceptTcpClient();
        using var inGameMayor = new ClientSession(2, serverSocket)
        {
            State = PlayerSessionState.InGame,
            CityId = 0,
        };

        var entries = registry.BuildCityList(mayors, [inGameMayor], defaultCityId: 0).ToList();

        Assert.DoesNotContain(entries, entry => entry.CityId == 0 && !entry.NeedsMayor);
        Assert.True(entries.Count >= CityRegistry.MinimumJoinableCities);
        Assert.All(entries, entry => Assert.True(entry.NeedsMayor));
    }
}
