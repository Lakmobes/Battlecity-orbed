using System.Net;
using System.Net.Sockets;

using BattleCity.Server;

using Xunit;

namespace BattleCity.Core.Tests;

public class CityRegistryTests
{
    [Fact]
    public void BuildCityList_IncludesDefaultCityWhenMayorMissing()
    {
        var registry = new CityRegistry();
        var mayors = new CityMayorRegistry();

        var entries = registry.BuildCityList(mayors, [], defaultCityId: 0).ToList();

        Assert.Single(entries);
        Assert.Equal((byte)0, entries[0].CityId);
        Assert.True(entries[0].NeedsMayor);
    }

    [Fact]
    public void BuildCityList_IncludesCommandoSlotForCityWithMayor()
    {
        var registry = new CityRegistry();
        var mayors = new CityMayorRegistry();
        mayors.Assign(0, 2);

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

        Assert.Single(entries);
        Assert.Equal((byte)2, entries[0].MayorPlayerId);
        Assert.Equal((byte)1, entries[0].PlayerCount);
    }
}
