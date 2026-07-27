using System.Net.Sockets;

using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Client.Network;

public enum GameClientEventKind
{
    LoginCorrect,
    StateGame,
    JoinData,
    PlayerUpdate,
    PlayerData,
    AddItem,
    Shoot,
    RemoveItem,
    PickedUp,
    NewBuilding,
    RemoveBuilding,
    Death,
    Hp,
    ChatMessage,
    GlobalChat,
    WhisperChat,
    MayorUpdate,
    AddRemCity,
    Interview,
    MayorInInterview,
    MayorHire,
    MayorDeclined,
    InterviewCancel,
    Comms,
    Fired,
    CanBuild,
    UpdatePop,
    ItemCount,
    PointsUpdate,
    MedKit,
    Cloak,
    Explosion,
    Warp,
    Respawn,
    Orbed,
    UnderAttack,
    ChatCommand,
    ClearPlayer,
    CityListClear,
    Error,
    Disconnected,
}

public readonly struct GameClientEvent
{
    public GameClientEvent(GameClientEventKind kind)
    {
        Kind = kind;
    }

    public GameClientEventKind Kind { get; }

    public byte PlayerId { get; init; }

    /// <summary>Legacy chat-command opcode (e.g. 69 = left battlefield).</summary>
    public byte ChatCommandCode { get; init; }

    public ServerStateGamePacket StateGame { get; init; }

    public ServerJoinDataPacket JoinData { get; init; }

    public ServerUpdatePacket Update { get; init; }

    public ServerPlayerDataPacket PlayerData { get; init; }

    public ServerOrbedCityPacket Orbed { get; init; }

    public ServerAddItemPacket AddItem { get; init; }

    public ServerShotPacket Shot { get; init; }

    public ServerRemoveItemPacket RemoveItem { get; init; }

    public ServerPickedUpPacket PickedUp { get; init; }

    public ServerBuildingPacket Building { get; init; }

    public ServerDeathPacket Death { get; init; }

    public ServerHpPacket Hp { get; init; }

    public ServerChatMessagePacket ChatMessage { get; init; }

    public ServerMayorUpdatePacket MayorUpdate { get; init; }

    public ServerAddRemCityPacket AddRemCity { get; init; }

    public ServerMayorHirePacket MayorHire { get; init; }

    public ServerCanBuildPacket CanBuild { get; init; }

    public ServerUpdatePopPacket UpdatePop { get; init; }

    public ServerItemCountPacket ItemCount { get; init; }

    public ServerPointsUpdatePacket PointsUpdate { get; init; }

    public ServerCloakPacket Cloak { get; init; }

    public ServerExplosionPacket Explosion { get; init; }

    public ServerStateGamePacket Warp { get; init; }

    public ServerRespawnPacket Respawn { get; init; }

    public char ErrorCode { get; init; }
}

public sealed class GameClient : IDisposable
{
    private readonly PacketReceiveBuffer _receiveBuffer = new();
    private readonly Queue<GameClientEvent> _events = new();
    private readonly string _uniqueId = Guid.NewGuid().ToString("N");

    private TcpClient? _client;
    private NetworkStream? _stream;

    public byte PlayerId { get; private set; }

    public bool IsConnected => _client?.Connected == true;

    public bool IsInGame { get; private set; }

    public string? LastError { get; private set; }

    public ServerStateGamePacket? SpawnState { get; private set; }

    public bool IsMayor { get; private set; }

    public bool IsAdmin { get; private set; }

    public IReadOnlyCollection<GameClientEvent> DrainEvents()
    {
        var events = _events.ToArray();
        _events.Clear();
        return events;
    }

    public bool ConnectAndLogin(string host, int port, string username, string password, TimeSpan timeout)
    {
        if (!TryConnect(host, port, timeout))
        {
            return false;
        }

        SendVersion();

        Span<byte> loginPayload = stackalloc byte[ClientLoginPacket.Size];
        new ClientLoginPacket(username, password).Write(loginPayload);
        Send(ClientMessageId.Login, loginPayload);

        if (!WaitForLogin(timeout))
        {
            return false;
        }

        return true;
    }

    public bool ConnectAndCreateAccount(string host, int port, ClientNewAccountPacket account, TimeSpan timeout)
    {
        if (!TryConnect(host, port, timeout))
        {
            return false;
        }

        SendVersion();

        Span<byte> payload = stackalloc byte[ClientNewAccountPacket.Size];
        account.Write(payload);
        Send(ClientMessageId.NewAccount, payload);

        if (!WaitForCreateAccount(timeout))
        {
            Disconnect();
            return false;
        }

        Disconnect();
        return true;
    }

    public void SendUpdate(ClientUpdatePacket update)
    {
        if (!IsInGame)
        {
            return;
        }

        Span<byte> payload = stackalloc byte[ClientUpdatePacket.Size];
        update.Write(payload);
        Send(ClientMessageId.Update, payload);
    }

    public void SendItemDrop(ItemType type, bool active)
    {
        if (!IsInGame)
        {
            return;
        }

        Span<byte> payload = stackalloc byte[ClientItemDropPacket.Size];
        new ClientItemDropPacket((byte)type, (byte)(active ? 1 : 0)).Write(payload);
        Send(ClientMessageId.ItemDrop, payload);
    }

    public void SendShoot(in ClientShotPacket shot)
    {
        if (!IsInGame)
        {
            return;
        }

        Span<byte> payload = stackalloc byte[ClientShotPacket.Size];
        shot.Write(payload);
        Send(ClientMessageId.Shoot, payload);
    }

    public void SendItemPickup(in ClientItemPickupPacket pickup)
    {
        if (!IsInGame)
        {
            return;
        }

        Span<byte> payload = stackalloc byte[ClientItemPickupPacket.Size];
        pickup.Write(payload);
        Send(ClientMessageId.ItemUp, payload);
    }

    public void SendMedKit()
    {
        if (!IsInGame)
        {
            return;
        }

        Send(ClientMessageId.MedKit, ReadOnlySpan<byte>.Empty);
    }

    public void SendCloak()
    {
        if (!IsInGame)
        {
            return;
        }

        Send(ClientMessageId.Cloak, ReadOnlySpan<byte>.Empty);
    }

    public void SendBuild(in ClientBuildPacket build)
    {
        if (!IsInGame)
        {
            return;
        }

        Span<byte> payload = stackalloc byte[ClientBuildPacket.Size];
        build.Write(payload);
        Send(ClientMessageId.Build, payload);
    }

    public void SendDemolish(in ClientDemolishPacket demolish)
    {
        if (!IsInGame)
        {
            return;
        }

        Span<byte> payload = stackalloc byte[ClientDemolishPacket.Size];
        demolish.Write(payload);
        Send(ClientMessageId.Demolish, payload);
    }

    public void SendDeath(in ClientDeathPacket death)
    {
        if (!IsInGame)
        {
            return;
        }

        Span<byte> payload = stackalloc byte[ClientDeathPacket.Size];
        death.Write(payload);
        Send(ClientMessageId.Death, payload);
    }

    public void EnterMeetingRoom() => Send(ClientMessageId.SetState, "C"u8);

    public void RefreshCityList() => Send(ClientMessageId.RefreshList, " "u8);

    public void ApplyToCity(byte cityId)
    {
        Span<byte> payload = stackalloc byte[1];
        payload[0] = cityId;
        Send(ClientMessageId.JobApp, payload);
    }

    public void CancelJobApplication() => Send(ClientMessageId.JobCancel, " "u8);

    public void SendMeetingChat(string message) => SendLobbyChat(ClientMessageId.ChatMessage, message);

    public void SendInterviewChat(string message) => SendLobbyChat(ClientMessageId.ChatMessage, message);

    public void SendComms(string message) => SendLobbyChat(ClientMessageId.Comms, message);

    public void SetDenyApplicants(bool deny)
    {
        Span<byte> payload = stackalloc byte[1];
        payload[0] = deny ? (byte)1 : (byte)0;
        Send(ClientMessageId.IsHiring, payload);
    }

    public void FirePlayer(byte targetPlayerId)
    {
        if (!IsInGame)
        {
            return;
        }

        Span<byte> payload = stackalloc byte[1];
        payload[0] = targetPlayerId;
        Send(ClientMessageId.Fired, payload);
    }

    public void AcceptApplicant() => Send(ClientMessageId.HireAccept, " "u8);

    public void DeclineApplicant() => Send(ClientMessageId.HireDecline, " "u8);

    public void SendWalkie(string message)
    {
        SendChatPayload(ClientMessageId.Walkie, message);
    }

    public void SendGlobal(string message)
    {
        SendChatPayload(ClientMessageId.Global, message);
    }

    public void SendWhisper(byte recipientId, string message)
    {
        if (!IsInGame || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        message = message.Trim();
        if (message.Length > ClientWhisperPacket.MaxMessageLength - 1)
        {
            message = message[..(ClientWhisperPacket.MaxMessageLength - 1)];
        }

        Span<byte> payload = stackalloc byte[ClientWhisperPacket.Size];
        new ClientWhisperPacket(recipientId, message).Write(payload);
        Send(ClientMessageId.Whisper, payload);
    }

    private void SendLobbyChat(ClientMessageId messageId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        message = message.Trim();
        if (message.Length > ChatPacketLimits.MaxMessageLength)
        {
            message = message[..ChatPacketLimits.MaxMessageLength];
        }

        Span<byte> payload = stackalloc byte[ChatPacketLimits.MaxMessageLength + 1];
        var bytes = System.Text.Encoding.ASCII.GetBytes(message);
        bytes.AsSpan(0, Math.Min(bytes.Length, ChatPacketLimits.MaxMessageLength)).CopyTo(payload);
        Send(messageId, payload[..bytes.Length]);
    }

    private void SendChatPayload(ClientMessageId messageId, string message)
    {
        if (!IsInGame || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        message = message.Trim();
        if (message.Length > ChatPacketLimits.MaxMessageLength)
        {
            message = message[..ChatPacketLimits.MaxMessageLength];
        }

        Span<byte> payload = stackalloc byte[ChatPacketLimits.MaxMessageLength + 1];
        var bytes = System.Text.Encoding.ASCII.GetBytes(message);
        bytes.AsSpan(0, Math.Min(bytes.Length, ChatPacketLimits.MaxMessageLength)).CopyTo(payload);
        Send(messageId, payload[..bytes.Length]);
    }

    public void Poll()
    {
        if (_stream is null || !_stream.DataAvailable)
        {
            return;
        }

        var scratch = new byte[512];
        var read = _stream.Read(scratch, 0, scratch.Length);
        if (read <= 0)
        {
            _events.Enqueue(new GameClientEvent(GameClientEventKind.Disconnected));
            Disconnect();
            return;
        }

        _receiveBuffer.Append(scratch.AsSpan(0, read));
        while (_receiveBuffer.TryRead(out var packet))
        {
            HandlePacket(packet);
        }
    }

    public void PollAvailable()
    {
        while (_stream is not null && _stream.DataAvailable)
        {
            Poll();
        }
    }

    public void Dispose() => Disconnect();

    private bool TryConnect(string host, int port, TimeSpan timeout)
    {
        Disconnect();
        LastError = null;

        _client = new TcpClient { NoDelay = true };
        try
        {
            var connectTask = _client.ConnectAsync(host, port);
            if (!connectTask.Wait(timeout))
            {
                LastError = "Connection timed out.";
                Disconnect();
                return false;
            }

            if (connectTask.IsFaulted)
            {
                LastError = "Could not connect to server. Start BattleCity.Server first.";
                Disconnect();
                return false;
            }
        }
        catch (AggregateException ex)
        {
            LastError = ex.InnerException?.Message ?? "Could not connect to server.";
            Disconnect();
            return false;
        }
        catch (SocketException ex)
        {
            LastError = ex.Message;
            Disconnect();
            return false;
        }

        _stream = _client.GetStream();
        return true;
    }

    private void SendVersion()
    {
        Span<byte> versionPayload = stackalloc byte[ClientVersionPacket.Size];
        ClientVersionPacket.CreateDefault(_uniqueId).Write(versionPayload);
        Send(ClientMessageId.Version, versionPayload);
    }

    private bool WaitForCreateAccount(TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Poll();
            foreach (var pending in _events.ToArray())
            {
                if (pending.Kind != GameClientEventKind.Error)
                {
                    continue;
                }

                _events.Clear();
                return pending.ErrorCode switch
                {
                    'A' => true,
                    'D' => SetError("Username is already in use."),
                    'K' => SetError("Invalid account info. Username: 1-15 letters, numbers, - or _."),
                    'F' => SetError("Server version mismatch. Update the client."),
                    _ => SetError($"Server rejected account ({pending.ErrorCode})."),
                };
            }

            Thread.Sleep(10);
        }

        LastError = "Timed out waiting for server response.";
        return false;

        bool SetError(string message)
        {
            LastError = message;
            return false;
        }
    }

    private bool WaitForLogin(TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Poll();
            foreach (var pending in _events.ToArray())
            {
                if (pending.Kind == GameClientEventKind.LoginCorrect)
                {
                    PlayerId = pending.PlayerId;
                    _events.Clear();
                    return true;
                }

                if (pending.Kind == GameClientEventKind.Error)
                {
                    LastError = $"Server rejected login ({pending.ErrorCode}).";
                    _events.Clear();
                    return false;
                }
            }

            Thread.Sleep(10);
        }

        LastError = "Timed out waiting for login response.";
        return false;
    }

    public bool TryWaitForGameStart(TimeSpan timeout) => WaitForGameStart(timeout);

    private bool WaitForGameStart(TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Poll();
            foreach (var pending in _events.ToArray())
            {
                if (pending.Kind == GameClientEventKind.StateGame)
                {
                    SpawnState = pending.StateGame;
                    IsInGame = true;
                    PollAvailable();
                    return true;
                }

                if (pending.Kind == GameClientEventKind.Error)
                {
                    LastError = $"Server rejected join ({pending.ErrorCode}).";
                    _events.Clear();
                    return false;
                }
            }

            Thread.Sleep(10);
        }

        LastError = "Timed out waiting for game start.";
        return false;
    }

    private void HandlePacket(LegacyPacket packet)
    {
        switch ((ServerMessageId)packet.MessageId)
        {
            case ServerMessageId.LoginCorrect when packet.Payload.Length >= 2:
                IsAdmin = (packet.Payload.Span[1] & 2) != 0;
                _events.Enqueue(new GameClientEvent(GameClientEventKind.LoginCorrect)
                {
                    PlayerId = packet.Payload.Span[0],
                });
                break;
            case ServerMessageId.StateGame when packet.Payload.Length >= ServerStateGamePacket.Size:
                var stateGame = ServerStateGamePacket.Read(packet.Payload.Span);
                SpawnState = stateGame;
                IsInGame = true;
                _events.Enqueue(new GameClientEvent(GameClientEventKind.StateGame)
                {
                    StateGame = stateGame,
                });
                break;
            case ServerMessageId.JoinData when packet.Payload.Length >= ServerJoinDataPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.JoinData)
                {
                    JoinData = ServerJoinDataPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.Update when packet.Payload.Length >= ServerUpdatePacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.PlayerUpdate)
                {
                    Update = ServerUpdatePacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.PlayerData when packet.Payload.Length >= ServerPlayerDataPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.PlayerData)
                {
                    PlayerData = ServerPlayerDataPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.AddItem when packet.Payload.Length >= ServerAddItemPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.AddItem)
                {
                    AddItem = ServerAddItemPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.Shoot when packet.Payload.Length >= ServerShotPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Shoot)
                {
                    Shot = ServerShotPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.RemItem when packet.Payload.Length >= ServerRemoveItemPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.RemoveItem)
                {
                    RemoveItem = ServerRemoveItemPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.PickedUp when packet.Payload.Length >= ServerPickedUpPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.PickedUp)
                {
                    PickedUp = ServerPickedUpPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.NewBuilding when packet.Payload.Length >= ServerBuildingPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.NewBuilding)
                {
                    Building = ServerBuildingPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.RemBuilding when packet.Payload.Length >= ServerBuildingPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.RemoveBuilding)
                {
                    Building = ServerBuildingPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.UnderAttack:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.UnderAttack));
                break;
            case ServerMessageId.Death when packet.Payload.Length >= ServerDeathPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Death)
                {
                    Death = ServerDeathPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.Hp when packet.Payload.Length >= ServerHpPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Hp)
                {
                    Hp = ServerHpPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.ChatMessage when packet.Payload.Length > 0:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.ChatMessage)
                {
                    ChatMessage = ServerChatMessagePacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.Global when packet.Payload.Length > 0:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.GlobalChat)
                {
                    ChatMessage = ServerChatMessagePacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.Whisper when packet.Payload.Length > 0:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.WhisperChat)
                {
                    ChatMessage = ServerChatMessagePacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.AddRemCity when packet.Payload.Length == 1 && packet.Payload.Span[0] == 255:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.CityListClear));
                break;
            case ServerMessageId.AddRemCity when packet.Payload.Length >= 2:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.AddRemCity)
                {
                    AddRemCity = ServerAddRemCityPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.ChatCommand when packet.Payload.Length >= 2:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.ChatCommand)
                {
                    PlayerId = packet.Payload.Span[0],
                    ChatCommandCode = packet.Payload.Span[1],
                });
                break;
            case ServerMessageId.ClearPlayer when packet.Payload.Length >= 1:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.ClearPlayer)
                {
                    PlayerId = packet.Payload.Span[0],
                });
                break;
            case ServerMessageId.CanBuild when packet.Payload.Length >= ServerCanBuildPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.CanBuild)
                {
                    CanBuild = ServerCanBuildPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.UpdatePop when packet.Payload.Length >= ServerUpdatePopPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.UpdatePop)
                {
                    UpdatePop = ServerUpdatePopPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.ItemCount when packet.Payload.Length >= ServerItemCountPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.ItemCount)
                {
                    ItemCount = ServerItemCountPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.PointsUpdate when packet.Payload.Length >= ServerPointsUpdatePacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.PointsUpdate)
                {
                    PointsUpdate = ServerPointsUpdatePacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.MedKit:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.MedKit));
                break;
            case ServerMessageId.Cloak when packet.Payload.Length >= ServerCloakPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Cloak)
                {
                    Cloak = ServerCloakPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.Explosion when packet.Payload.Length >= ServerExplosionPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Explosion)
                {
                    Explosion = ServerExplosionPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.Warp when packet.Payload.Length >= ServerStateGamePacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Warp)
                {
                    Warp = ServerStateGamePacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.Respawn when packet.Payload.Length >= ServerRespawnPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Respawn)
                {
                    Respawn = ServerRespawnPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.Comms when packet.Payload.Length > 0:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Comms)
                {
                    ChatMessage = ServerChatMessagePacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.Fired:
                IsInGame = false;
                SpawnState = null;
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Fired));
                break;
            case ServerMessageId.Interview:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Interview));
                break;
            case ServerMessageId.MayorInInterview:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.MayorInInterview));
                break;
            case ServerMessageId.MayorHire when packet.Payload.Length >= ServerMayorHirePacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.MayorHire)
                {
                    MayorHire = ServerMayorHirePacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.MayorDeclined:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.MayorDeclined));
                break;
            case ServerMessageId.InterviewCancel:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.InterviewCancel));
                break;
            case ServerMessageId.MayorUpdate when packet.Payload.Length >= ServerMayorUpdatePacket.Size:
                var mayorUpdate = ServerMayorUpdatePacket.Read(packet.Payload.Span);
                if (mayorUpdate.PlayerId == PlayerId)
                {
                    IsMayor = mayorUpdate.IsMayor;
                }

                _events.Enqueue(new GameClientEvent(GameClientEventKind.MayorUpdate)
                {
                    MayorUpdate = mayorUpdate,
                });
                break;
            case ServerMessageId.Orbed when packet.Payload.Length >= ServerOrbedCityPacket.Size:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Orbed)
                {
                    Orbed = ServerOrbedCityPacket.Read(packet.Payload.Span),
                });
                break;
            case ServerMessageId.Error when packet.Payload.Length > 0:
                _events.Enqueue(new GameClientEvent(GameClientEventKind.Error)
                {
                    ErrorCode = (char)packet.Payload.Span[0],
                });
                break;
        }
    }

    private void Send(ClientMessageId messageId, ReadOnlySpan<byte> payload)
    {
        if (_stream is null)
        {
            return;
        }

        var packet = LegacyPacketCodec.EncodeClient(messageId, payload);
        _stream.Write(packet);
    }

    private void Disconnect()
    {
        IsInGame = false;
        IsMayor = false;
        IsAdmin = false;
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        _receiveBuffer.Clear();
    }
}
