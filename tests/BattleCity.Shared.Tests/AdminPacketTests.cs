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
}
