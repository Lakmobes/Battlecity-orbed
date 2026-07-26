using System.Net;
using System.Net.Sockets;
using System.Numerics;

using BattleCity.Core.Ecs;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Core.Network;
using BattleCity.Server.Accounts;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Chat;
using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Server;

public sealed class GameServer : IDisposable
{
    public const int MaxPlayers = 32;
    private const byte DefaultCityId = 0;

    private readonly GameSimulation _simulation = new();
    private readonly Dictionary<byte, ClientSession> _sessions = new();
    private readonly object _sync = new();
    private readonly AccountDatabase _accounts;

    private TcpListener? _listener;
    private readonly CityMayorRegistry _mayors = new();
    private readonly CityRegistry _cities = new();
    private readonly CityBuildPopSync _buildPopSync = new();
    private readonly FactoryItemCountSync _factoryItemCountSync = new();
    private byte _nextPlayerId = 1;
    private CityLayout? _cityLayout;
    private bool _started;

    public GameServer(string databasePath)
    {
        DatabasePath = databasePath;
        _accounts = new AccountDatabase(databasePath);
    }

    public GameServer()
        : this(Path.Combine(AppContext.BaseDirectory, "accounts.db"))
    {
    }

    public int Port { get; private set; }

    public string BoundHost { get; private set; } = "0.0.0.0";

    public bool IsRunning => _started;

    public string DatabasePath { get; }

    public AccountDatabase Accounts => _accounts;

    public GameSimulation Simulation => _simulation;

    public void Start(string host, int port, string cityName = "Buenos Aires", string cityDesign = "demo")
    {
        if (_started)
        {
            return;
        }

        _simulation.TileMap = LoadTileMap();
        _cityLayout = LevelLoader.LoadLegacyCity(cityName, cityDesign);
        _simulation.LoadCityLayout(_cityLayout);
        _simulation.SpawnDemoItems(DefaultCityId);
        _simulation.AssignNetworkItemIds();
        _simulation.NetworkPlayersUseLocalBulletDamage = false;
        _simulation.NetworkPlayersUseLocalHealthDeath = false;
        _simulation.ReportBombEventsToNetwork = true;
        _simulation.ReportFactoryItemSpawnsToNetwork = true;
        _simulation.ReportRespawnEventsToNetwork = true;
        _simulation.ReturnInventoryPlaceablesOnDeath = true;
        _buildPopSync.Reset(_simulation);
        _factoryItemCountSync.Reset(_simulation);

        BoundHost = string.IsNullOrWhiteSpace(host) ? "0.0.0.0" : host.Trim();
        var listenAddress = BoundHost is "0.0.0.0" or "*"
            ? IPAddress.Any
            : IPAddress.Parse(BoundHost);

        _listener = new TcpListener(listenAddress, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _started = true;

        Console.WriteLine($"Battle City server listening on {BoundHost}:{Port}");
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        try
        {
            _listener?.Stop();
        }
        catch
        {
            // Listener may already be closed.
        }

        _listener = null;

        lock (_sync)
        {
            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }

            _sessions.Clear();
        }

        Console.WriteLine("Battle City server stopped.");
    }

    public IReadOnlyList<ConnectedPlayerInfo> GetConnectedPlayers()
    {
        lock (_sync)
        {
            return _sessions.Values
                .OrderBy(session => session.PlayerId)
                .Select(session => new ConnectedPlayerInfo(
                    session.PlayerId,
                    session.DisplayName,
                    session.State.ToString(),
                    session.CityId,
                    session.IsAdmin,
                    session.IsMayor,
                    session.IsGuest))
                .ToList();
        }
    }

    public void Update(float deltaSeconds)
    {
        if (!_started)
        {
            return;
        }

        AcceptPendingConnections();
        ReadSessions();
        _simulation.Update(deltaSeconds);
        BroadcastBuildPopSync();
        BroadcastFactoryItemCountSync();
        BroadcastFactoryAddItems();
        BroadcastPendingOrbEvents();
        BroadcastPendingExplosionEvents();
        BroadcastPendingBombBuildingRemovals();
        BroadcastPendingDeathEvents();
        BroadcastPendingRespawnEvents();
        BroadcastPendingHpEvents();
        RemoveDisconnectedSessions();
    }

    public void Dispose()
    {
        Stop();
        _simulation.Dispose();
        _accounts.Dispose();
    }

    public bool TrySetAccountAdmin(string username, bool isAdmin)
    {
        if (!_accounts.TrySetAdmin(username, isAdmin))
        {
            return false;
        }

        lock (_sync)
        {
            foreach (var session in _sessions.Values)
            {
                if (!string.Equals(session.RegisteredUsername, username, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                session.IsAdmin = isAdmin
                    || string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase);
            }
        }

        return true;
    }

    private void AcceptPendingConnections()
    {
        if (_listener is null)
        {
            return;
        }

        while (_listener.Pending())
        {
            lock (_sync)
            {
                if (_sessions.Count >= MaxPlayers)
                {
                    _listener.AcceptTcpClient().Dispose();
                    continue;
                }

                var playerId = _nextPlayerId++;
                if (_nextPlayerId == 0)
                {
                    _nextPlayerId = 1;
                }

                var tcpClient = _listener.AcceptTcpClient();
                tcpClient.NoDelay = true;
                _sessions[playerId] = new ClientSession(playerId, tcpClient);
                Console.WriteLine($"Player slot {playerId} connected from {tcpClient.Client.RemoteEndPoint}");
            }
        }
    }

    private void ReadSessions()
    {
        List<ClientSession> sessions;
        lock (_sync)
        {
            sessions = _sessions.Values.ToList();
        }

        foreach (var session in sessions)
        {
            session.ReadAvailable();
            while (session.ReceiveBuffer.TryRead(out var packet))
            {
                HandlePacket(session, packet);
            }
        }
    }

    private void HandlePacket(ClientSession session, LegacyPacket packet)
    {
        switch ((ClientMessageId)packet.MessageId)
        {
            case ClientMessageId.Version:
                HandleVersion(session, packet.Payload.Span);
                break;
            case ClientMessageId.Login:
                HandleLogin(session, packet.Payload.Span);
                break;
            case ClientMessageId.NewAccount:
                HandleNewAccount(session, packet.Payload.Span);
                break;
            case ClientMessageId.NextStep:
                HandleNextStep(session, packet.Payload.Span);
                break;
            case ClientMessageId.SetState:
                HandleSetState(session, packet.Payload.Span);
                break;
            case ClientMessageId.JobApp:
                HandleJobApplication(session, packet.Payload.Span);
                break;
            case ClientMessageId.JobCancel:
                HandleJobCancel(session);
                break;
            case ClientMessageId.HireAccept:
                HandleHireAccept(session);
                break;
            case ClientMessageId.HireDecline:
                HandleHireDecline(session);
                break;
            case ClientMessageId.RefreshList:
                SendCityList(session);
                break;
            case ClientMessageId.Update:
                HandleUpdate(session, packet.Payload.Span);
                break;
            case ClientMessageId.ItemDrop:
                HandleItemDrop(session, packet.Payload.Span);
                break;
            case ClientMessageId.Shoot:
                HandleShoot(session, packet.Payload.Span);
                break;
            case ClientMessageId.ItemUp:
                HandleItemPickup(session, packet.Payload.Span);
                break;
            case ClientMessageId.MedKit:
                HandleMedKit(session);
                break;
            case ClientMessageId.Cloak:
                HandleCloak(session);
                break;
            case ClientMessageId.Build:
                HandleBuild(session, packet.Payload.Span);
                break;
            case ClientMessageId.Demolish:
                HandleDemolish(session, packet.Payload.Span);
                break;
            case ClientMessageId.Death:
                HandleDeath(session, packet.Payload.Span);
                break;
            case ClientMessageId.ChatMessage:
                if (session.State == PlayerSessionState.Meeting)
                {
                    HandleMeetingChat(session, packet.Payload.Span);
                }
                else if (session.State == PlayerSessionState.Interview)
                {
                    HandleInterviewChat(session, packet.Payload.Span);
                }
                else
                {
                    HandleProximityChat(session, packet.Payload.Span, RadarChatDeliveryMode.RadarOnly);
                }

                break;
            case ClientMessageId.Comms:
                HandleComms(session, packet.Payload.Span);
                break;
            case ClientMessageId.IsHiring:
                HandleIsHiring(session, packet.Payload.Span);
                break;
            case ClientMessageId.Fired:
                HandleFired(session, packet.Payload.Span);
                break;
            case ClientMessageId.Walkie:
                HandleProximityChat(session, packet.Payload.Span, RadarChatDeliveryMode.RadarAndTeam);
                break;
            case ClientMessageId.Global:
                HandleGlobalChat(session, packet.Payload.Span);
                break;
            case ClientMessageId.Whisper:
                HandleWhisper(session, packet.Payload.Span);
                break;
            case ClientMessageId.TcpPing:
                session.SendServer(ServerMessageId.TcpPong, " "u8);
                break;
        }
    }

    private void HandleVersion(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (payload.Length < ClientVersionPacket.Size)
        {
            return;
        }

        var version = System.Text.Encoding.ASCII.GetString(payload.Slice(50, 10)).TrimEnd('\0');
        if (!string.Equals(version, NetworkConstants.LegacyVersion, StringComparison.Ordinal))
        {
            session.SendServer(ServerMessageId.Error, "F"u8);
            return;
        }

        session.State = PlayerSessionState.Verified;
    }

    private void HandleLogin(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (session.State < PlayerSessionState.Verified || payload.Length < ClientLoginPacket.Size)
        {
            return;
        }

        var login = ClientLoginPacket.Read(payload);
        var username = login.Username.Trim();
        var password = login.Password.Trim();

        string displayName;
        string town;

        if (AccountDatabase.IsGuestLogin(username, password))
        {
            displayName = string.IsNullOrWhiteSpace(username)
                ? $"Guest{session.PlayerId}"
                : username;
            town = "Buenos Aires";
            session.IsGuest = true;
            session.RegisteredUsername = null;
        }
        else
        {
            var result = _accounts.TryLogin(
                username,
                password,
                IsUsernameAlreadyOnline,
                out var account);

            switch (result)
            {
                case AccountLoginResult.NotFound:
                    session.SendServer(ServerMessageId.Error, "C"u8);
                    return;
                case AccountLoginResult.WrongPassword:
                    session.SendServer(ServerMessageId.Error, "B"u8);
                    return;
                case AccountLoginResult.AlreadyLoggedIn:
                    session.SendServer(ServerMessageId.Error, "E"u8);
                    return;
                case AccountLoginResult.Success:
                    break;
            }

            displayName = account!.Username;
            town = account.Town;
            session.IsGuest = false;
            session.IsAdmin = account.IsAdmin
                || string.Equals(account.Username, "admin", StringComparison.OrdinalIgnoreCase);
            session.RegisteredUsername = account.Username;
            session.Points = account.Points;
            session.Deaths = account.Deaths;
        }

        session.DisplayName = displayName;
        session.Town = town;
        session.State = PlayerSessionState.LoggedIn;

        Span<byte> loginCorrect = stackalloc byte[2];
        loginCorrect[0] = session.PlayerId;
        // Bit0 = success, bit1 = admin (clients that ignore bit1 still see non-zero success).
        loginCorrect[1] = (byte)(1 | (session.IsAdmin ? 2 : 0));
        session.SendServer(ServerMessageId.LoginCorrect, loginCorrect);

        Span<byte> playerData = stackalloc byte[ServerPlayerDataPacket.Size];
        playerData.Clear();
        playerData[0] = (byte)session.PlayerId;
        WriteFixedAscii(playerData.Slice(1, 16), displayName);
        WriteFixedAscii(playerData.Slice(17, 16), town);
        playerData[33] = 1;
        BroadcastExcept(session.PlayerId, ServerMessageId.PlayerData, playerData);
        session.SendServer(ServerMessageId.PlayerData, playerData);
    }

    private void HandleNewAccount(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (session.State < PlayerSessionState.Verified || payload.Length < ClientNewAccountPacket.Size)
        {
            return;
        }

        var request = ClientNewAccountPacket.Read(payload);
        var result = _accounts.TryCreateAccount(
            request.Username,
            request.Password,
            request.Town,
            request.Email,
            request.FullName,
            request.State);

        session.SendServer(
            ServerMessageId.Error,
            result switch
            {
                AccountCreateResult.Created => "A"u8,
                AccountCreateResult.UsernameTaken => "D"u8,
                _ => "K"u8,
            });
    }

    private bool IsUsernameAlreadyOnline(string username)
    {
        lock (_sync)
        {
            return _sessions.Values.Any(session =>
                session.State >= PlayerSessionState.LoggedIn
                && !session.IsGuest
                && string.Equals(session.RegisteredUsername, username, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void HandleNextStep(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (session.State < PlayerSessionState.LoggedIn || payload.Length == 0)
        {
            return;
        }

        if (payload[0] != (byte)'A')
        {
            return;
        }

        JoinGame(session);
        session.SendServer(ServerMessageId.NextStep, "B"u8);
    }

    private void HandleSetState(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (session.State < PlayerSessionState.LoggedIn || payload.Length == 0)
        {
            return;
        }

        if (payload[0] != (byte)'C')
        {
            return;
        }

        if (session.State == PlayerSessionState.InGame)
        {
            LeaveGame(session);
        }

        EnterMeetingRoom(session);
    }

    private void EnterMeetingRoom(ClientSession session)
    {
        session.State = PlayerSessionState.Meeting;
        SendCityList(session);
    }

    private void SendCityList(ClientSession session)
    {
        foreach (var entry in _cities.BuildCityList(_mayors, GetLobbySessions(), DefaultCityId))
        {
            Span<byte> payload = stackalloc byte[3];
            entry.Write(payload);
            session.SendServer(ServerMessageId.AddRemCity, payload);
        }
    }

    private void HandleJobApplication(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (session.State is not (PlayerSessionState.Meeting or PlayerSessionState.LoggedIn)
            || payload.Length == 0)
        {
            return;
        }

        var cityId = payload[0];
        if (!CityCatalog.IsValidCityId(cityId))
        {
            return;
        }

        var slot = _cities.GetOrCreate(cityId);
        var inGameCount = CountInGamePlayersInCity(cityId);

        if (!_mayors.HasMayor(cityId))
        {
            session.CityId = cityId;
            session.HasCityAssignment = true;
            session.State = PlayerSessionState.Meeting;
            SetMayor(session, isMayor: true);
            JoinGame(session);
            return;
        }

        if (inGameCount >= GameConstants.MaxPlayersPerCity)
        {
            session.SendServer(ServerMessageId.MayorInInterview, " "u8);
            return;
        }

        if (slot.HiringApplicantId.HasValue)
        {
            session.SendServer(ServerMessageId.MayorInInterview, " "u8);
            return;
        }

        if (!_mayors.TryGetMayorPlayerId(cityId, out var mayorId)
            || !TryGetSession(mayorId, out var mayorSession))
        {
            return;
        }

        slot.HiringApplicantId = session.PlayerId;
        session.CityId = cityId;
        session.HasCityAssignment = true;
        session.State = PlayerSessionState.Interview;

        Span<byte> hire = stackalloc byte[ServerMayorHirePacket.Size];
        new ServerMayorHirePacket(session.PlayerId).Write(hire);
        mayorSession.SendServer(ServerMessageId.MayorHire, hire);
        session.SendServer(ServerMessageId.Interview, " "u8);
        SendComms(session, mayorSession.PlayerId, "The mayor has arrived - start talking.");
    }

    private void HandleJobCancel(ClientSession session)
    {
        CancelInterviewForApplicant(session.PlayerId);
        if (session.State == PlayerSessionState.Interview)
        {
            session.State = PlayerSessionState.Meeting;
        }
    }

    private void HandleHireAccept(ClientSession session)
    {
        if (!session.IsMayor)
        {
            return;
        }

        var slot = _cities.GetOrCreate(session.CityId);
        if (slot.DenyApplicants)
        {
            HandleHireDecline(session);
            return;
        }

        if (!slot.HiringApplicantId.HasValue
            || !TryGetSession(slot.HiringApplicantId.Value, out var applicant))
        {
            return;
        }

        slot.HiringApplicantId = null;
        applicant.IsMayor = false;
        applicant.HasCityAssignment = true;
        applicant.State = PlayerSessionState.Meeting;
        JoinGame(applicant);
    }

    private void HandleHireDecline(ClientSession session)
    {
        if (!session.IsMayor)
        {
            return;
        }

        var slot = _cities.GetOrCreate(session.CityId);
        if (slot.HiringApplicantId.HasValue
            && TryGetSession(slot.HiringApplicantId.Value, out var applicant))
        {
            applicant.State = PlayerSessionState.Meeting;
            applicant.SendServer(ServerMessageId.MayorDeclined, " "u8);
        }

        slot.HiringApplicantId = null;
    }

    private void CancelInterviewForApplicant(byte applicantId)
    {
        if (!_cities.TryFindSlotByApplicant(applicantId, out var slot))
        {
            return;
        }

        if (_mayors.TryGetMayorPlayerId(slot.CityId, out var mayorId)
            && TryGetSession(mayorId, out var mayorSession))
        {
            mayorSession.SendServer(ServerMessageId.InterviewCancel, " "u8);
        }

        slot.HiringApplicantId = null;
    }

    private void HandleMeetingChat(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (session.State != PlayerSessionState.Meeting || payload.Length == 0)
        {
            return;
        }

        var message = ReadChatPayload(payload);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Span<byte> broadcast = stackalloc byte[1 + ChatPacketLimits.MaxMessageLength];
        var length = ServerChatMessagePacket.Write(broadcast, session.PlayerId, message);
        var packet = broadcast[..length];

        foreach (var recipient in GetLobbySessions())
        {
            if (recipient.PlayerId == session.PlayerId
                || recipient.State != PlayerSessionState.Meeting)
            {
                continue;
            }

            recipient.SendServer(ServerMessageId.ChatMessage, packet);
        }
    }

    private void HandleInterviewChat(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (session.State != PlayerSessionState.Interview || payload.Length == 0)
        {
            return;
        }

        var message = ReadChatPayload(payload);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!_mayors.TryGetMayorPlayerId(session.CityId, out var mayorId)
            || !TryGetSession(mayorId, out var mayorSession))
        {
            return;
        }

        SendComms(mayorSession, session.PlayerId, message);
    }

    private void HandleComms(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsMayor || !session.IsInGame || payload.Length == 0)
        {
            return;
        }

        var message = ReadChatPayload(payload);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var slot = _cities.GetOrCreate(session.CityId);
        if (!slot.HiringApplicantId.HasValue
            || !TryGetSession(slot.HiringApplicantId.Value, out var applicant))
        {
            return;
        }

        SendComms(applicant, session.PlayerId, message);
    }

    private void HandleIsHiring(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsMayor || !session.IsInGame || payload.Length == 0)
        {
            return;
        }

        var slot = _cities.GetOrCreate(session.CityId);
        slot.DenyApplicants = payload[0] != 0;
    }

    private void HandleFired(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsMayor || !session.IsInGame || payload.Length == 0)
        {
            return;
        }

        var targetId = payload[0];
        if (!TryGetSession(targetId, out var target)
            || target.CityId != session.CityId
            || target.IsMayor
            || target.PlayerId == session.PlayerId)
        {
            return;
        }

        LeaveGame(target);
        target.HasCityAssignment = false;
        target.State = PlayerSessionState.Meeting;
        target.SendServer(ServerMessageId.Fired, " "u8);
    }

    private static void SendComms(ClientSession recipient, byte senderId, string message)
    {
        Span<byte> packet = stackalloc byte[1 + ChatPacketLimits.MaxMessageLength];
        var length = ServerChatMessagePacket.Write(packet, senderId, message);
        recipient.SendServer(ServerMessageId.Comms, packet[..length]);
    }

    private void LeaveGame(ClientSession session)
    {
        if (session.State != PlayerSessionState.InGame)
        {
            return;
        }

        if (session.IsMayor)
        {
            _mayors.Remove(session.CityId, session.PlayerId);
            TransferMayor(session.CityId, excludedPlayerId: session.PlayerId);
        }

        _simulation.TryRemoveNetworkPlayer(session.PlayerId);
        session.State = PlayerSessionState.Meeting;
        session.IsMayor = false;
        BroadcastCityListToMeetingClients();
    }

    private int CountInGamePlayersInCity(byte cityId)
    {
        var count = 0;
        foreach (var player in GetInGameSessions())
        {
            if (player.CityId == cityId)
            {
                count++;
            }
        }

        return count;
    }

    private bool TryGetSession(byte playerId, out ClientSession session)
    {
        lock (_sync)
        {
            return _sessions.TryGetValue(playerId, out session!);
        }
    }

    private IEnumerable<ClientSession> GetLobbySessions()
    {
        lock (_sync)
        {
            return _sessions.Values
                .Where(session => session.State is PlayerSessionState.Meeting
                    or PlayerSessionState.Interview
                    or PlayerSessionState.LoggedIn
                    or PlayerSessionState.InGame)
                .ToList();
        }
    }

    private void JoinGame(ClientSession session)
    {
        if (_cityLayout is null || !session.HasCityAssignment)
        {
            return;
        }

        Vector2 spawn;
        if (_simulation.TryGetCityRespawnPosition(session.CityId, out var openSpawn, out _))
        {
            spawn = openSpawn;
        }
        else
        {
            spawn = _cityLayout.GetSpawnPosition();
            spawn = _simulation.FindOpenTankSpawnNear(spawn);
        }

        _simulation.CreateNetworkPlayerEntity(spawn, session.PlayerId, session.CityId);
        session.State = PlayerSessionState.InGame;
        EnsureMayorAssigned(session);

        Span<byte> stateGame = stackalloc byte[ServerStateGamePacket.Size];
        new ServerStateGamePacket(
            (ushort)Math.Clamp((int)spawn.X, 0, ushort.MaxValue),
            (ushort)Math.Clamp((int)spawn.Y, 0, ushort.MaxValue),
            session.CityId).Write(stateGame);
        session.SendServer(ServerMessageId.StateGame, stateGame);

        Span<byte> joinData = stackalloc byte[ServerJoinDataPacket.Size];
        new ServerJoinDataPacket(
            session.PlayerId,
            mayor: session.IsMayor ? (byte)1 : (byte)0,
            session.CityId).Write(joinData);
        BroadcastExcept(session.PlayerId, ServerMessageId.JoinData, joinData);

        foreach (var other in GetInGameSessions())
        {
            if (other.PlayerId == session.PlayerId)
            {
                continue;
            }

            Span<byte> existingJoin = stackalloc byte[ServerJoinDataPacket.Size];
            new ServerJoinDataPacket(
                other.PlayerId,
                mayor: other.IsMayor ? (byte)1 : (byte)0,
                other.CityId).Write(existingJoin);
            session.SendServer(ServerMessageId.JoinData, existingJoin);

            if (_simulation.TryGetNetworkPlayerSnapshot(other.PlayerId, out var snapshot))
            {
                Span<byte> updatePayload = stackalloc byte[ServerUpdatePacket.Size];
                snapshot.ToPacket().Write(updatePayload);
                session.SendServer(ServerMessageId.Update, updatePayload);
            }
        }

        SendJoinWorldSnapshot(session);
        SendCanBuildSnapshot(session);
        SendFactoryItemCountSnapshot(session);
        SendPointsSnapshot(session);
        BroadcastPointsUpdate(session);
        BroadcastCityListToMeetingClients();

        Console.WriteLine($"Player {session.DisplayName} joined at ({spawn.X}, {spawn.Y})");
    }

    private void SendCanBuildSnapshot(ClientSession session)
    {
        if (!session.IsMayor && !session.IsAdmin)
        {
            return;
        }

        foreach (var packet in _buildPopSync.CreateCanBuildSnapshot(_simulation))
        {
            Span<byte> payload = stackalloc byte[ServerCanBuildPacket.Size];
            packet.Write(payload);
            session.SendServer(ServerMessageId.CanBuild, payload);
        }

        _buildPopSync.Reset(_simulation);
    }

    private void SendFactoryItemCountSnapshot(ClientSession session)
    {
        foreach (var packet in _factoryItemCountSync.CreateItemCountSnapshot(_simulation))
        {
            Span<byte> payload = stackalloc byte[ServerItemCountPacket.Size];
            packet.Write(payload);
            session.SendServer(ServerMessageId.ItemCount, payload);
        }

        _factoryItemCountSync.Reset(_simulation);
    }

    private void BroadcastBuildPopSync()
    {
        foreach (var packet in _buildPopSync.CollectCanBuildChanges(_simulation))
        {
            Span<byte> payload = stackalloc byte[ServerCanBuildPacket.Size];
            packet.Write(payload);
            BroadcastCanBuild(payload);
        }

        foreach (var packet in _buildPopSync.CollectPopulationChanges(_simulation))
        {
            Span<byte> payload = stackalloc byte[ServerUpdatePopPacket.Size];
            packet.Write(payload);
            BroadcastAll(ServerMessageId.UpdatePop, payload);
        }
    }

    private void BroadcastFactoryItemCountSync()
    {
        foreach (var packet in _factoryItemCountSync.CollectItemCountChanges(_simulation))
        {
            Span<byte> payload = stackalloc byte[ServerItemCountPacket.Size];
            packet.Write(payload);
            BroadcastAll(ServerMessageId.ItemCount, payload);
        }
    }

    private void BroadcastFactoryAddItems()
    {
        while (_simulation.TryConsumeFactoryAddItem(out var addItem))
        {
            Span<byte> payload = stackalloc byte[ServerAddItemPacket.Size];
            addItem.Write(payload);
            BroadcastAll(ServerMessageId.AddItem, payload);
        }
    }

    private void BroadcastCityListToMeetingClients()
    {
        foreach (var session in GetLobbySessions())
        {
            if (session.State != PlayerSessionState.Meeting)
            {
                continue;
            }

            SendCityList(session);
        }
    }

    private void BroadcastCanBuild(ReadOnlySpan<byte> payload)
    {
        foreach (var session in GetInGameSessions())
        {
            if (session.IsMayor || session.IsAdmin)
            {
                session.SendServer(ServerMessageId.CanBuild, payload);
            }
        }
    }

    private void SendJoinWorldSnapshot(ClientSession session)
    {
        var snapshot = new JoinWorldSnapshot();
        _simulation.CollectJoinSnapshot(snapshot);

        foreach (var removed in snapshot.RemovedBuildings)
        {
            Span<byte> payload = stackalloc byte[ServerBuildingPacket.Size];
            removed.Write(payload);
            session.SendServer(ServerMessageId.RemBuilding, payload);
        }

        foreach (var building in snapshot.Buildings)
        {
            Span<byte> payload = stackalloc byte[ServerBuildingPacket.Size];
            building.Write(payload);
            session.SendServer(ServerMessageId.NewBuilding, payload);
        }

        foreach (var item in snapshot.Items)
        {
            Span<byte> payload = stackalloc byte[ServerAddItemPacket.Size];
            item.Write(payload);
            session.SendServer(ServerMessageId.AddItem, payload);
        }
    }

    private void HandleUpdate(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || payload.Length < ClientUpdatePacket.Size)
        {
            return;
        }

        var update = ClientUpdatePacket.Read(payload);
        if (!_simulation.TryApplyNetworkUpdate(
                session.PlayerId,
                update.X,
                update.Y,
                update.TurnInput,
                update.MoveInput,
                update.Direction))
        {
            return;
        }

        Span<byte> broadcast = stackalloc byte[ServerUpdatePacket.Size];
        new ServerUpdatePacket(
            session.PlayerId,
            update.X,
            update.Y,
            update.Turn,
            update.Move,
            update.Direction).Write(broadcast);
        BroadcastExcept(session.PlayerId, ServerMessageId.Update, broadcast);
    }

    private void HandleItemDrop(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || payload.Length < ClientItemDropPacket.Size)
        {
            return;
        }

        var request = ClientItemDropPacket.Read(payload);
        if (!Enum.IsDefined(typeof(ItemType), (int)request.ItemType))
        {
            return;
        }

        if (!_simulation.TryDropItemForNetworkPlayer(
                session.PlayerId,
                (ItemType)request.ItemType,
                request.Active != 0,
                out var addItem))
        {
            return;
        }

        Span<byte> broadcast = stackalloc byte[ServerAddItemPacket.Size];
        addItem.Write(broadcast);
        BroadcastAll(ServerMessageId.AddItem, broadcast);
    }

    private void HandleShoot(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || payload.Length < ClientShotPacket.Size)
        {
            return;
        }

        var request = ClientShotPacket.Read(payload);
        if (!_simulation.TryFireShotForNetworkPlayer(session.PlayerId, request, out var shot))
        {
            return;
        }

        Span<byte> broadcast = stackalloc byte[ServerShotPacket.Size];
        shot.Write(broadcast);
        BroadcastExcept(session.PlayerId, ServerMessageId.Shoot, broadcast);
    }

    private void HandleItemPickup(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || payload.Length < ClientItemPickupPacket.Size)
        {
            return;
        }

        var request = ClientItemPickupPacket.Read(payload);
        if (!_simulation.TryPickupItemForNetworkPlayer(
                session.PlayerId,
                request,
                out var removeItem,
                out var pickedUp))
        {
            return;
        }

        Span<byte> removePayload = stackalloc byte[ServerRemoveItemPacket.Size];
        removeItem.Write(removePayload);
        BroadcastAll(ServerMessageId.RemItem, removePayload);

        Span<byte> pickedUpPayload = stackalloc byte[ServerPickedUpPacket.Size];
        pickedUp.Write(pickedUpPayload);
        session.SendServer(ServerMessageId.PickedUp, pickedUpPayload);
    }

    private void HandleMedKit(ClientSession session)
    {
        if (!session.IsInGame)
        {
            return;
        }

        if (!_simulation.TryUseMedKitForNetworkPlayer(session.PlayerId, out var hpPacket))
        {
            return;
        }

        Span<byte> hpPayload = stackalloc byte[ServerHpPacket.Size];
        hpPacket.Write(hpPayload);
        BroadcastAll(ServerMessageId.Hp, hpPayload);
        session.SendServer(ServerMessageId.MedKit, ReadOnlySpan<byte>.Empty);
    }

    private void HandleCloak(ClientSession session)
    {
        if (!session.IsInGame)
        {
            return;
        }

        if (!_simulation.TryUseCloakForNetworkPlayer(session.PlayerId))
        {
            return;
        }

        Span<byte> payload = stackalloc byte[ServerCloakPacket.Size];
        new ServerCloakPacket(session.PlayerId).Write(payload);
        BroadcastAll(ServerMessageId.Cloak, payload);
    }

    private void HandleBuild(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || payload.Length < ClientBuildPacket.Size)
        {
            return;
        }

        if (!session.IsMayor && !session.IsAdmin)
        {
            return;
        }

        var request = ClientBuildPacket.Read(payload);
        if (!_simulation.TryBuildForNetworkPlayer(session.PlayerId, request, out var building))
        {
            return;
        }

        Span<byte> broadcast = stackalloc byte[ServerBuildingPacket.Size];
        building.Write(broadcast);
        BroadcastAll(ServerMessageId.NewBuilding, broadcast);
    }

    private void HandleDemolish(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || payload.Length < ClientDemolishPacket.Size)
        {
            return;
        }

        var request = ClientDemolishPacket.Read(payload);
        if (!_simulation.TryDemolishForNetworkPlayer(session.PlayerId, request, out var building))
        {
            return;
        }

        Span<byte> broadcast = stackalloc byte[ServerBuildingPacket.Size];
        building.Write(broadcast);
        BroadcastAll(ServerMessageId.RemBuilding, broadcast);
    }

    private void HandleDeath(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || payload.Length < ClientDeathPacket.Size)
        {
            return;
        }

        var request = ClientDeathPacket.Read(payload);
        if (!_simulation.TryApplyDeathForNetworkPlayer(session.PlayerId, request.KillerCity, out var death))
        {
            return;
        }

        RecordDeathPointChanges(session, request.KillerCity);

        Span<byte> broadcast = stackalloc byte[ServerDeathPacket.Size];
        death.Write(broadcast);
        BroadcastAll(ServerMessageId.Death, broadcast);
    }

    private void HandleProximityChat(
        ClientSession session,
        ReadOnlySpan<byte> payload,
        RadarChatDeliveryMode deliveryMode)
    {
        if (!session.IsInGame || payload.Length == 0)
        {
            return;
        }

        if (!_simulation.TryGetNetworkPlayerPosition(session.PlayerId, out var senderPosition))
        {
            return;
        }

        var message = ReadChatPayload(payload);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Span<byte> broadcast = stackalloc byte[1 + ChatPacketLimits.MaxMessageLength];
        var length = ServerChatMessagePacket.Write(broadcast, session.PlayerId, message);
        var packet = broadcast[..length];
        var senderX = (int)senderPosition.X;
        var senderY = (int)senderPosition.Y;

        foreach (var recipient in GetInGameSessions())
        {
            if (recipient.PlayerId == session.PlayerId)
            {
                continue;
            }

            if (!_simulation.TryGetNetworkPlayerPosition(recipient.PlayerId, out var recipientPosition))
            {
                continue;
            }

            if (!RadarChatRouter.ShouldDeliver(
                    deliveryMode,
                    senderX,
                    senderY,
                    session.CityId,
                    (int)recipientPosition.X,
                    (int)recipientPosition.Y,
                    recipient.CityId))
            {
                continue;
            }

            recipient.SendServer(ServerMessageId.ChatMessage, packet);
        }
    }

    private void HandleGlobalChat(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || !session.IsAdmin || payload.Length == 0)
        {
            return;
        }

        var message = ReadChatPayload(payload);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Span<byte> broadcast = stackalloc byte[1 + ChatPacketLimits.MaxMessageLength];
        var length = ServerChatMessagePacket.Write(broadcast, session.PlayerId, message);
        BroadcastExcept(session.PlayerId, ServerMessageId.Global, broadcast[..length]);
    }

    private void HandleWhisper(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || payload.Length < ClientWhisperPacket.MessageOffset)
        {
            return;
        }

        var whisper = ClientWhisperPacket.Read(payload);
        if (string.IsNullOrWhiteSpace(whisper.Message))
        {
            return;
        }

        ClientSession? recipient;
        lock (_sync)
        {
            recipient = _sessions.GetValueOrDefault(whisper.RecipientId);
        }

        if (recipient is null || !recipient.IsInGame || recipient.PlayerId == session.PlayerId)
        {
            return;
        }

        Span<byte> packet = stackalloc byte[1 + ChatPacketLimits.MaxMessageLength];
        var length = ServerChatMessagePacket.Write(packet, session.PlayerId, whisper.Message);
        recipient.SendServer(ServerMessageId.Whisper, packet[..length]);
    }

    private static string ReadChatPayload(ReadOnlySpan<byte> payload)
    {
        var length = payload.IndexOf((byte)0);
        if (length < 0)
        {
            length = payload.Length;
        }

        length = Math.Min(length, ChatPacketLimits.MaxMessageLength);
        var message = System.Text.Encoding.ASCII.GetString(payload.Slice(0, length)).Trim();
        return message;
    }

    private void BroadcastPendingHpEvents()
    {
        while (_simulation.TryConsumeNetworkHpEvent(out var hpEvent))
        {
            Span<byte> payload = stackalloc byte[ServerHpPacket.Size];
            hpEvent.Write(payload);
            BroadcastAll(ServerMessageId.Hp, payload);
        }
    }

    private void ApplyDeathPointTransfers(ClientSession victim, byte killerCityId)
    {
        DeathPointTransfers.Apply(victim, killerCityId, GetInGameSessions(), (session, pointDelta) =>
        {
            if (pointDelta != 0 && !session.IsGuest && session.RegisteredUsername is not null)
            {
                _accounts.AdjustPoints(session.RegisteredUsername, pointDelta);
            }

            BroadcastPointsUpdate(session);
        });
    }

    private void RecordDeathPointChanges(ClientSession victim, byte killerCityId)
    {
        if (!victim.IsGuest && victim.RegisteredUsername is not null)
        {
            _accounts.IncrementDeaths(victim.RegisteredUsername);
        }

        victim.Deaths++;
        ApplyDeathPointTransfers(victim, killerCityId);
    }

    private void BroadcastPointsUpdate(ClientSession session)
    {
        Span<byte> payload = stackalloc byte[ServerPointsUpdatePacket.Size];
        CreatePointsUpdatePacket(session).Write(payload);
        BroadcastAll(ServerMessageId.PointsUpdate, payload);
    }

    private void SendPointsSnapshot(ClientSession session)
    {
        foreach (var player in GetInGameSessions())
        {
            Span<byte> payload = stackalloc byte[ServerPointsUpdatePacket.Size];
            CreatePointsUpdatePacket(player).Write(payload);
            session.SendServer(ServerMessageId.PointsUpdate, payload);
        }
    }

    private static ServerPointsUpdatePacket CreatePointsUpdatePacket(ClientSession session) =>
        new(
            session.PlayerId,
            (uint)Math.Max(0, session.Points),
            (uint)Math.Max(0, session.Deaths));

    private void BroadcastPendingDeathEvents()
    {
        while (_simulation.TryConsumeNetworkDeathEvent(out var deathEvent))
        {
            Span<byte> payload = stackalloc byte[ServerDeathPacket.Size];
            deathEvent.Write(payload);
            BroadcastAll(ServerMessageId.Death, payload);

            if (TryGetSession(deathEvent.PlayerId, out var session))
            {
                RecordDeathPointChanges(session, deathEvent.KillerCity);
            }

            Console.WriteLine($"Player {deathEvent.PlayerId} died (server combat)");
        }
    }

    private void BroadcastPendingOrbEvents()
    {
        while (_simulation.TryConsumeOrbEvent(out var orbEvent))
        {
            Span<byte> payload = stackalloc byte[ServerOrbedCityPacket.Size];
            new ServerOrbedCityPacket(
                (byte)orbEvent.VictimCityId,
                (byte)orbEvent.AttackerCityId,
                orbEvent.VictimPoints,
                orbEvent.AttackerPoints).Write(payload);
            BroadcastAll(ServerMessageId.Orbed, payload);
            Console.WriteLine(
                $"City {orbEvent.VictimCityId} orbed by city {orbEvent.AttackerCityId} ({orbEvent.VictimPoints} points)");
        }
    }

    private void BroadcastPendingExplosionEvents()
    {
        while (_simulation.TryConsumeNetworkExplosionEvent(out var explosionEvent))
        {
            Span<byte> payload = stackalloc byte[ServerExplosionPacket.Size];
            explosionEvent.Explosion.Write(payload);
            BroadcastAll(ServerMessageId.Explosion, payload);

            if (explosionEvent.RemovedItemId != 0)
            {
                Span<byte> removePayload = stackalloc byte[ServerRemoveItemPacket.Size];
                new ServerRemoveItemPacket(explosionEvent.RemovedItemId).Write(removePayload);
                BroadcastAll(ServerMessageId.RemItem, removePayload);
            }
        }
    }

    private void BroadcastPendingBombBuildingRemovals()
    {
        while (_simulation.TryConsumeBombBuildingRemoval(out var building))
        {
            Span<byte> payload = stackalloc byte[ServerBuildingPacket.Size];
            building.Write(payload);
            BroadcastAll(ServerMessageId.RemBuilding, payload);
        }
    }

    private void BroadcastPendingRespawnEvents()
    {
        while (_simulation.TryConsumeNetworkRespawnEvent(out var respawnEvent))
        {
            Span<byte> warpPayload = stackalloc byte[ServerStateGamePacket.Size];
            new ServerStateGamePacket(
                (ushort)respawnEvent.Position.X,
                (ushort)respawnEvent.Position.Y,
                respawnEvent.CityId).Write(warpPayload);

            if (TryGetSession(respawnEvent.PlayerId, out var session))
            {
                session.SendServer(ServerMessageId.Warp, warpPayload);
            }

            Span<byte> respawnPayload = stackalloc byte[ServerRespawnPacket.Size];
            new ServerRespawnPacket(respawnEvent.PlayerId).Write(respawnPayload);
            BroadcastExcept(respawnEvent.PlayerId, ServerMessageId.Respawn, respawnPayload);

            if (_simulation.TryGetNetworkPlayerSnapshot(respawnEvent.PlayerId, out var snapshot))
            {
                Span<byte> updatePayload = stackalloc byte[ServerUpdatePacket.Size];
                snapshot.ToPacket().Write(updatePayload);
                BroadcastAll(ServerMessageId.Update, updatePayload);
            }
        }
    }

    private void BroadcastAll(ServerMessageId messageId, ReadOnlySpan<byte> payload)
    {
        foreach (var session in GetInGameSessions())
        {
            session.SendServer(messageId, payload);
        }
    }

    private void BroadcastExcept(byte excludedPlayerId, ServerMessageId messageId, ReadOnlySpan<byte> payload)
    {
        foreach (var session in GetInGameSessions())
        {
            if (session.PlayerId == excludedPlayerId)
            {
                continue;
            }

            session.SendServer(messageId, payload);
        }
    }

    private IEnumerable<ClientSession> GetInGameSessions()
    {
        lock (_sync)
        {
            return _sessions.Values.Where(session => session.IsInGame).ToList();
        }
    }

    private void RemoveDisconnectedSessions()
    {
        List<byte> disconnected = new();
        lock (_sync)
        {
            foreach (var (playerId, session) in _sessions)
            {
                if (session.IsConnected)
                {
                    continue;
                }

                disconnected.Add(playerId);
            }

            foreach (var playerId in disconnected)
            {
                RemoveSession(playerId);
            }
        }
    }

    private void RemoveSession(byte playerId)
    {
        if (!_sessions.Remove(playerId, out var session))
        {
            return;
        }

        CancelInterviewForApplicant(playerId);

        if (session.IsMayor)
        {
            _mayors.Remove(session.CityId, session.PlayerId);
            TransferMayor(session.CityId, excludedPlayerId: session.PlayerId);
        }

        _simulation.TryRemoveNetworkPlayer(playerId);
        session.Dispose();
        Console.WriteLine($"Player slot {playerId} disconnected");
    }

    private void EnsureMayorAssigned(ClientSession session)
    {
        if (_mayors.HasMayor(session.CityId))
        {
            if (_mayors.IsMayor(session.CityId, session.PlayerId))
            {
                // Re-apply after the network entity exists, and re-send MayorUpdate
                // (first promotion may have happened while still in Meeting).
                SetMayor(session, isMayor: true);
            }
            else
            {
                session.IsMayor = false;
            }

            return;
        }

        SetMayor(session, isMayor: true);
    }

    private void TransferMayor(byte cityId, byte excludedPlayerId)
    {
        foreach (var successor in GetInGameSessions())
        {
            if (successor.PlayerId == excludedPlayerId || successor.CityId != cityId)
            {
                continue;
            }

            SetMayor(successor, isMayor: true);
            return;
        }
    }

    private void SetMayor(ClientSession session, bool isMayor)
    {
        session.IsMayor = isMayor;
        _simulation.SetNetworkPlayerMayor(session.PlayerId, isMayor);

        if (isMayor)
        {
            _mayors.Assign(session.CityId, session.PlayerId);
        }
        else
        {
            _mayors.Remove(session.CityId, session.PlayerId);
        }

        Span<byte> update = stackalloc byte[ServerMayorUpdatePacket.Size];
        new ServerMayorUpdatePacket(session.PlayerId, isMayor).Write(update);
        // Always notify the assignee — they may not be InGame yet (Meeting → Join).
        session.SendServer(ServerMessageId.MayorUpdate, update);
        BroadcastExcept(session.PlayerId, ServerMessageId.MayorUpdate, update);
        BroadcastCityListToMeetingClients();
    }

    private static TileMap LoadTileMap()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "legacy", "data", "map.dat");
            if (File.Exists(candidate))
            {
                return TileMap.LoadFromLegacyMapDat(candidate);
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return TileMap.CreateEmpty();
    }

    private static string ReadFixedAscii(ReadOnlySpan<byte> buffer)
    {
        var length = buffer.IndexOf((byte)0);
        if (length < 0)
        {
            length = buffer.Length;
        }

        return System.Text.Encoding.ASCII.GetString(buffer.Slice(0, length));
    }

    private static void WriteFixedAscii(Span<byte> destination, string value)
    {
        destination.Clear();
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }
}
