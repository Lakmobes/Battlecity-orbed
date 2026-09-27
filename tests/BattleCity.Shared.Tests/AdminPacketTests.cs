using System.Numerics;

using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Shared.Tests;

public class AdminPacketTests
{
    [Fact]
    public void ClientAdminPacket_RoundTrips()
    {
        Span<byte> buffer = stackalloc byte[ClientAdminPacket.Size];
        new ClientAdminPacket(12, AdminCommands.Ban).Write(buffer);
        var read = ClientAdminPacket.Read(buffer);
        Assert.Equal((ushort)12, read.TargetId);
        Assert.Equal(AdminCommands.Ban, read.Command);
    }

    [Fact]
    public void ServerAdminPacket_RoundTrips()
    {
        Span<byte> buffer = stackalloc byte[ServerAdminPacket.Size];
        new ServerAdminPacket(1, 7, AdminCommands.Kick).Write(buffer);
        var read = ServerAdminPacket.Read(buffer);
        Assert.Equal((ushort)1, read.AdminPlayerId);
        Assert.Equal((ushort)7, read.TargetPlayerId);
        Assert.Equal(AdminCommands.Kick, read.Command);
    }

    [Fact]
    public void ServerBanPacket_RoundTrips()
    {
        Span<byte> buffer = stackalloc byte[ServerBanPacket.Size];
        new ServerBanPacket("Alice", "HostAdmin", "griefing").Write(buffer);
        var read = ServerBanPacket.Read(buffer);
        Assert.Equal("Alice", read.Account);
        Assert.Equal("HostAdmin", read.IpAddress);
        Assert.Equal("griefing", read.Reason);
    }

    [Fact]
    public void StartingCityPacket_RoundTrips()
    {
        Span<byte> buffer = stackalloc byte[StartingCityPacket.Size];
        new StartingCityPacket(27).Write(buffer);
        Assert.Equal(27, StartingCityPacket.Read(buffer).CityId);
    }

    [Fact]
    public void AdminEditPacket_RoundTripsCoreFields()
    {
        Span<byte> buffer = stackalloc byte[AdminEditPacket.Size];
        var original = new AdminEditPacket(
            "Alice",
            "secret",
            "a@b.c",
            "Alice Full",
            "Berlin",
            "DE",
            points: 42,
            monthlyPoints: 7,
            deaths: 3,
            orbs: 1,
            assists: 2,
            playerType: 1);
        original.Write(buffer);
        var read = AdminEditPacket.Read(buffer);
        Assert.Equal("Alice", read.Username);
        Assert.Equal("secret", read.Password);
        Assert.Equal("a@b.c", read.Email);
        Assert.Equal("Alice Full", read.FullName);
        Assert.Equal("Berlin", read.Town);
        Assert.Equal("DE", read.State);
        Assert.Equal(42, read.Points);
        Assert.Equal(7, read.MonthlyPoints);
        Assert.Equal(3, read.Deaths);
        Assert.Equal(1, read.Orbs);
        Assert.Equal(2, read.Assists);
        Assert.Equal(1, read.PlayerType);
        Assert.Equal(AdminEditPacket.Size, buffer.Length);
    }
}
