using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

using Xunit;

namespace BattleCity.Shared.Tests;

public class LegacyPacketCodecTests
{
    [Fact]
    public void EncodeDecodeRoundTripPreservesPayload()
    {
        Span<byte> payload = stackalloc byte[ClientUpdatePacket.Size];
        new ClientUpdatePacket(1200, 2400, turn: 1, move: -1, direction: 8).Write(payload);

        var encoded = LegacyPacketCodec.EncodeClient(ClientMessageId.Update, payload);

        Assert.True(LegacyPacketCodec.TryReadPacket(encoded, out var packet, out var consumed));
        Assert.Equal((byte)ClientMessageId.Update, packet.MessageId);
        Assert.Equal(encoded.Length, consumed);
        Assert.Equal(payload.ToArray(), packet.Payload.ToArray());
    }

    [Fact]
    public void ClientAndServerMessageIdsMatchLegacyJoinFlow()
    {
        Assert.Equal(1, (int)ClientMessageId.Version);
        Assert.Equal(2, (int)ClientMessageId.Login);
        Assert.Equal(21, (int)ClientMessageId.NextStep);
        Assert.Equal(28, (int)ClientMessageId.Update);
        Assert.Equal(8, (int)ServerMessageId.StateGame);
        Assert.Equal(18, (int)ServerMessageId.JoinData);
        Assert.Equal(46, (int)ServerMessageId.Update);
    }

    [Fact]
    public void ServerAddItemPacket_RoundTripsLegacyLayout()
    {
        Span<byte> buffer = stackalloc byte[ServerAddItemPacket.Size];
        new ServerAddItemPacket(12, 34, city: 0, type: 5, active: 0, id: 99).Write(buffer);

        var packet = ServerAddItemPacket.Read(buffer);
        Assert.Equal(12, packet.X);
        Assert.Equal(34, packet.Y);
        Assert.Equal(5, packet.Type);
        Assert.Equal((ushort)99, packet.Id);
        Assert.Equal(23, (int)ServerMessageId.AddItem);
    }

    [Fact]
    public void ClientItemDropPacket_RoundTrips()
    {
        Span<byte> buffer = stackalloc byte[ClientItemDropPacket.Size];
        new ClientItemDropPacket(itemType: 5, active: 0).Write(buffer);
        var packet = ClientItemDropPacket.Read(buffer);
        Assert.Equal(5, packet.ItemType);
        Assert.Equal(0, packet.Active);
        Assert.Equal(14, (int)ClientMessageId.ItemDrop);
    }

    [Fact]
    public void ServerOrbedCityPacket_RoundTripsLegacyLayout()
    {
        Span<byte> buffer = stackalloc byte[ServerOrbedCityPacket.Size];
        new ServerOrbedCityPacket(victimCity: 0, orberCity: 1, points: 12000, orberCityPoints: 5000).Write(buffer);

        var packet = ServerOrbedCityPacket.Read(buffer);
        Assert.Equal(0, packet.VictimCity);
        Assert.Equal(1, packet.OrberCity);
        Assert.Equal(12000u, packet.Points);
        Assert.Equal(5000u, packet.OrberCityPoints);
        Assert.Equal(27, (int)ServerMessageId.Orbed);
    }

    [Fact]
    public void ClientShotPacket_RoundTripsLegacyLayout()
    {
        Span<byte> buffer = stackalloc byte[ClientShotPacket.Size];
        new ClientShotPacket(100, 200, direction: 8, type: 0).Write(buffer);

        var packet = ClientShotPacket.Read(buffer);
        Assert.Equal(100, packet.X);
        Assert.Equal(200, packet.Y);
        Assert.Equal(8, packet.Direction);
        Assert.Equal(0, packet.Type);
        Assert.Equal(29, (int)ClientMessageId.Shoot);
    }

    [Fact]
    public void ServerShotPacket_RoundTripsLegacyLayout()
    {
        Span<byte> buffer = stackalloc byte[ServerShotPacket.Size];
        new ServerShotPacket(playerId: 2, x: 300, y: 400, direction: 16, type: 1).Write(buffer);

        var packet = ServerShotPacket.Read(buffer);
        Assert.Equal(2, packet.PlayerId);
        Assert.Equal(300, packet.X);
        Assert.Equal(47, (int)ServerMessageId.Shoot);
    }

    [Fact]
    public void ServerBuildingPacket_RoundTripsLegacyLayout()
    {
        Span<byte> buffer = stackalloc byte[ServerBuildingPacket.Size];
        new ServerBuildingPacket(city: 0, x: 40, y: 41, buildSlot: 2, count: 0, id: 55, population: 10).Write(buffer);

        var packet = ServerBuildingPacket.Read(buffer);
        Assert.Equal(40, packet.X);
        Assert.Equal(55, packet.Id);
        Assert.Equal(15, (int)ServerMessageId.NewBuilding);
    }

    [Fact]
    public void ClientBuildAndDemolishPackets_RoundTrip()
    {
        Span<byte> buildBuffer = stackalloc byte[ClientBuildPacket.Size];
        new ClientBuildPacket(10, 11, buildSlot: 3, isAutoBuild: false).Write(buildBuffer);
        var build = ClientBuildPacket.Read(buildBuffer);
        Assert.Equal(3, build.BuildSlot);
        Assert.Equal(10, (int)ClientMessageId.Build);

        Span<byte> demolishBuffer = stackalloc byte[ClientDemolishPacket.Size];
        new ClientDemolishPacket(99).Write(demolishBuffer);
        Assert.Equal(99, ClientDemolishPacket.Read(demolishBuffer).BuildingId);
        Assert.Equal(16, (int)ClientMessageId.Demolish);
        Assert.Equal(46, (int)ClientMessageId.Cloak);
        Assert.Equal(90, (int)ServerMessageId.Cloak);
    }

    [Fact]
    public void UpdatePacketEncodesLegacyAxisValues()
    {
        var packet = new ClientUpdatePacket(100, 200, turn: -1, move: 1, direction: 4);
        Assert.Equal(0, packet.Turn);
        Assert.Equal(2, packet.Move);
        Assert.Equal(-1, packet.TurnInput);
        Assert.Equal(1, packet.MoveInput);
    }
}
