using BattleCity.Shared.Gameplay;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Shared.Tests;

public class InspectPanelTests
{
    [Fact]
    public void FormatPlayer_UsesOneDeathWhenThePlayerHasNone()
    {
        var lines = InspectPanelText.FormatPlayer("Ada", 40, 10, 2, 3, 0);
        Assert.Equal("Ada", lines[0]);
        Assert.Equal("Points:  40", lines[1]);
        Assert.Equal("Orbs:    2", lines[2]);
        Assert.Equal("Assists: 3", lines[3]);
        Assert.Equal("Pts/Death: 40", lines[6]);
    }

    [Fact]
    public void FormatCity_HidesBountyUntilTheCityIsOrbable()
    {
        var hidden = InspectPanelText.FormatCity("Balkh", null, 1, 4, false, 0, 0, 0);
        Assert.Equal(4, hidden.Length);
        Assert.Equal("Mayor:   (none)", hidden[1]);

        var shown = InspectPanelText.FormatCity("Balkh", "Ada", 2, 21, true, 1, 30, 90);
        Assert.Contains("Bounty:  30 points", shown);
        Assert.Contains("Uptime:  1h 30m", shown);
    }

    [Fact]
    public void InspectPackets_RoundTrip()
    {
        Span<byte> playerBuffer = stackalloc byte[ServerClickPlayerPacket.Size];
        new ServerClickPlayerPacket(4, 80, 15, 2, 1, 3).Write(playerBuffer);
        var player = ServerClickPlayerPacket.Read(playerBuffer);
        Assert.Equal(4, player.PlayerId);
        Assert.Equal(80, player.Points);
        Assert.Equal(15, player.MonthlyPoints);
        Assert.Equal(2, player.Orbs);
        Assert.Equal(1, player.Assists);
        Assert.Equal(3, player.Deaths);

        Span<byte> cityBuffer = stackalloc byte[ServerRightClickCityPacket.Size];
        new ServerRightClickCityPacket(7, 21, true, 4, 50, 125).Write(cityBuffer);
        var city = ServerRightClickCityPacket.Read(cityBuffer);
        Assert.Equal(7, city.CityId);
        Assert.Equal(21, city.BuildingCount);
        Assert.True(city.IsOrbable);
        Assert.Equal(4, city.Orbs);
        Assert.Equal(50, city.OrbPoints);
        Assert.Equal(125, city.UptimeMinutes);
    }
}
