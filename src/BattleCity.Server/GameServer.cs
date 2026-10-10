using System.Net;
using System.Net.Sockets;
using System.Numerics;

using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Core.Network;
using BattleCity.Server.Accounts;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Chat;
using BattleCity.Shared.Gameplay;
using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Server;

public sealed class GameServer : IDisposable
{
    public const int MaxPlayers = 32;

    private readonly GameSimulation _simulation = new();
    private readonly Dictionary<byte, ClientSession> _sessions = new();
    private readonly object _sync = new();
    private readonly AccountDatabase _accounts;
    private readonly ServerNewsStore _news;

    private TcpListener? _listener;
    private readonly CityMayorRegistry _mayors = new();
    private readonly CityRegistry _cities = new();
    private readonly AiCityController _aiCity = new();
    private readonly HashSet<byte> _botPlayerIds = [];
    private readonly CityBuildPopSync _buildPopSync = new();
    private readonly FactoryItemCountSync _factoryItemCountSync = new();
    private readonly CityDestructSchedule _cityDestruct = new();
    private byte _nextPlayerId = 1;
    private bool _worldReady;
    private bool _started;
    private bool _shutdownRequested;
    private float _aiGoalRefreshSeconds;
    private Dictionary<byte, int> _citySizes = new();
    private string _aiCitiesSummary = "AI: none";

    public GameServer(string databasePath)
    {
        DatabasePath = databasePath;
        _accounts = new AccountDatabase(databasePath);
        _news = new ServerNewsStore(databasePath);
    }

    public GameServer()
        : this(Path.Combine(AppContext.BaseDirectory, "accounts.db"))
    {
    }

    public int Port { get; private set; }

    /// <summary>Cities currently staffed by AI, for the host window.</summary>
    public string AiCitiesSummary => _aiCitiesSummary;

    public string BoundHost { get; private set; } = "0.0.0.0";

    public bool IsRunning => _started;

    public string DatabasePath { get; }

    public AccountDatabase Accounts => _accounts;

    public GameSimulation Simulation => _simulation;

    /// <param name="cityName">Ignored — multiplayer boots CC-only from map.dat (legacy MP).</param>
    /// <param name="cityDesign">Ignored — demo layouts are offline-only.</param>
    public void Start(string host, int port, string cityName = "Buenos Aires", string cityDesign = "demo")
    {
        if (_started)
        {
            return;
        }

        _ = cityName;
        _ = cityDesign;

        _simulation.TileMap = LoadTileMap();
        var ccCount = _simulation.LoadMultiplayerWorld();
        _worldReady = true;
        _simulation.AssignNetworkItemIds();
        _simulation.NetworkPlayersUseLocalBulletDamage = false;
        // Server owns HP→death for all network tanks (humans + AI City bots).
        _simulation.NetworkPlayersUseLocalHealthDeath = true;
        _simulation.ReportBombEventsToNetwork = true;
        _simulation.ReportFactoryItemSpawnsToNetwork = true;
        _simulation.ReportItemLifeToNetwork = true;
        _simulation.ReportRespawnEventsToNetwork = true;
        _simulation.ReturnInventoryPlaceablesOnDeath = true;
        _buildPopSync.Reset(_simulation);
        _factoryItemCountSync.Reset(_simulation);
        _cities.ResetStartingCity();
        Console.WriteLine($"Multiplayer world: {ccCount} command centers (CC-only, no demo.city)");
        Console.WriteLine($"Meeting room starting city: {_cities.StartingCityId}");

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

        DisableAiCity();
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
                    CityCatalog.IsValidCityId(session.CityId) ? CityCatalog.GetName(session.CityId) : "-",
                    GetCitySize(session.CityId),
                    session.Points,
                    session.IsAdmin,
                    session.IsMayor,
                    session.IsGuest))
                .ToList();
        }
    }

    private int GetCitySize(byte cityId) =>
        _citySizes.TryGetValue(cityId, out var size) ? size : 0;

    private void RefreshHostSnapshot()
    {
        var sizes = new Dictionary<byte, int>();
        foreach (var cityId in _simulation.EnumerateCityBuildIds())
        {
            if (cityId is < 0 or > byte.MaxValue || !_simulation.TryGetCityBuild(cityId, out var build))
            {
                continue;
            }

            sizes[(byte)cityId] = build.CurrentBuildingCount;
        }

        var aiIds = _aiCity.CityIds;
        _aiCitiesSummary = aiIds.Count == 0
            ? "AI: none"
            : "AI: " + string.Join(
                ", ",
                aiIds.Select(id =>
                {
                    var size = sizes.TryGetValue(id, out var count) ? count : 0;
                    return $"{CityCatalog.GetName(id)} ({id}, {size} bldgs)";
                }));
        _citySizes = sizes;
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
        BroadcastPendingItemLifeEvents();
        BroadcastAiCityUpdates(deltaSeconds);
        TickAbandonedCities(deltaSeconds);
        RemoveDisconnectedSessions();
        RefreshHostSnapshot();

        if (_shutdownRequested)
        {
            _shutdownRequested = false;
            DisableAiCity();
            Stop();
        }
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

        List<ClientSession>? updated = null;
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
                updated ??= [];
                updated.Add(session);
            }
        }

        // Push admin bit so live clients can use /kick etc. without relogin.
        if (updated is not null)
        {
            Span<byte> loginCorrect = stackalloc byte[2];
            foreach (var session in updated)
            {
                loginCorrect[0] = session.PlayerId;
                loginCorrect[1] = (byte)(1 | (session.IsAdmin ? 2 : 0));
                session.SendServer(ServerMessageId.LoginCorrect, loginCorrect);
            }
        }

        return true;
    }

    public bool TryUnbanAccount(string username) => _accounts.TryRemoveBan(username);

    public IReadOnlyList<BanRecord> ListBans() => _accounts.ListBans();

    public byte StartingCityId => _cities.StartingCityId;

    public bool TrySetStartingCity(byte cityId)
    {
        if (!_cities.TrySetStartingCity(cityId))
        {
            return false;
        }

        BroadcastCityListToMeetingClients();
        Console.WriteLine($"StartingCity::{cityId}");
        return true;
    }

    public bool TryGetAccountForAdminEdit(string username, out AccountRecord? account) =>
        _accounts.TryGetAccountForAdminEdit(username, out account);

    public bool TryApplyAdminEdit(in AdminEditPacket edit) => ApplyAdminEditFromPacket(edit);

    public string SeasonName => _accounts.SeasonName;

    public bool TryStartSeason(string name, out string message)
    {
        if (!_started)
        {
            message = "Server is not running.";
            return false;
        }

        _accounts.StartSeason(name);
        message = $"Season started: {_accounts.SeasonName}. Seasonal points were reset.";
        return true;
    }

    public bool IsAiCityArmed => _aiCity.IsArmed;

    public bool IsAiCityActive => _aiCity.IsActive;

    public int AiCityCount => _aiCity.RequestedCityCount;

    public AiCityStance AiCityStance => _aiCity.Stance;

    public bool TryArmAiCities(int cityCount, AiCityStance stance, out string message)
    {
        if (!_started || !_worldReady)
        {
            message = "Server is not running.";
            return false;
        }

        _aiCity.Arm(cityCount, stance);
        var change = SyncAiCities();
        message = change.Message
            ?? $"AI cities armed ({AiCityController.StanceLabel(stance)}, {Math.Clamp(cityCount, 1, AiCityController.MaxCities)}). They appear when a player enters a city.";
        Console.WriteLine(message);
        return true;
    }

    public void DisableAiCity()
    {
        if (!_aiCity.IsArmed && !_aiCity.IsActive)
        {
            return;
        }

        _aiCity.Disarm();
        var change = SyncAiCities();
        if (!string.IsNullOrWhiteSpace(change.Message))
        {
            Console.WriteLine(change.Message);
        }
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

                if (!TryAllocatePlayerId(out var playerId))
                {
                    _listener.AcceptTcpClient().Dispose();
                    continue;
                }

                var tcpClient = _listener.AcceptTcpClient();
                tcpClient.NoDelay = true;
                _sessions[playerId] = new ClientSession(playerId, tcpClient);
                Console.WriteLine($"Player slot {playerId} connected from {tcpClient.Client.RemoteEndPoint}");
            }
        }
    }

    private bool TryAllocatePlayerId(out byte playerId)
    {
        // Prefer lowest free id in 1..255 (legacy never uses 0).
        for (var attempt = 0; attempt < 255; attempt++)
        {
            var candidate = _nextPlayerId;
            _nextPlayerId = (byte)(_nextPlayerId == 255 ? 1 : _nextPlayerId + 1);
            if (candidate == 0 || _sessions.ContainsKey(candidate) || _botPlayerIds.Contains(candidate))
            {
                continue;
            }

            playerId = candidate;
            return true;
        }

        playerId = 0;
        return false;
    }

    private byte AllocateBotPlayerId()
    {
        if (!TryAllocatePlayerId(out var playerId) || playerId == 0)
        {
            return 0;
        }

        _botPlayerIds.Add(playerId);
        return playerId;
    }

    private void ReleaseBotPlayerId(byte playerId) => _botPlayerIds.Remove(playerId);

    private bool HumanBlocksAiRedeploy(byte cityId)
    {
        if (!TryGetCityAnchor(cityId, out var anchorX, out var anchorY))
        {
            return false;
        }

        foreach (var session in GetInGameSessions())
        {
            if (!_simulation.TryGetNetworkPlayerPosition(session.PlayerId, out var position))
            {
                continue;
            }

            var (gridX, gridY) = TankPlacement.GetTileFromTopLeft(position);
            if (AiCityController.TankBlocksRedeploy(anchorX, anchorY, gridX, gridY))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetCityAnchor(byte cityId, out int anchorX, out int anchorY)
    {
        if (_simulation.TryGetCityBuild(cityId, out var build)
            && (build.CommandCenterGridX != 0 || build.CommandCenterGridY != 0))
        {
            anchorX = build.CommandCenterGridX;
            anchorY = build.CommandCenterGridY;
            return true;
        }

        return CityBuildInitializer.TryGetCommandCenterGridForCity(
            cityId,
            _simulation.TileMap,
            out anchorX,
            out anchorY);
    }

    private List<(byte PlayerId, byte CityId, bool InGame)> SnapshotHumanPlayers()
    {
        lock (_sync)
        {
            return _sessions.Values
                .Select(session => (session.PlayerId, session.CityId, session.IsInGame))
                .ToList();
        }
    }

    private AiCityPresenceChange SyncAiCities()
    {
        var change = _aiCity.Sync(
            _simulation,
            _cities,
            _mayors,
            AllocateBotPlayerId,
            ReleaseBotPlayerId,
            SnapshotHumanPlayers());
        ApplyAiPresenceChange(change);
        return change;
    }

    private void ApplyAiPresenceChange(AiCityPresenceChange change)
    {
        if (change.Removed.Count > 0)
        {
            BroadcastBotClears(change.Removed);
        }

        if (change.Spawned.Count > 0)
        {
            BroadcastBotJoins(change.Spawned);
        }

        BroadcastAiCityBase(change);

        if (change.Removed.Count > 0 || change.Spawned.Count > 0)
        {
            BroadcastCityListToMeetingClients();
        }
    }

    private void BroadcastAiCityBase(AiCityPresenceChange change)
    {
        Span<byte> buildingPayload = stackalloc byte[ServerBuildingPacket.Size];
        foreach (var building in change.Buildings)
        {
            building.Write(buildingPayload);
            BroadcastAll(ServerMessageId.NewBuilding, buildingPayload);
        }

        Span<byte> itemPayload = stackalloc byte[ServerAddItemPacket.Size];
        foreach (var wall in change.Walls)
        {
            wall.Write(itemPayload);
            BroadcastAll(ServerMessageId.AddItem, itemPayload);
        }
    }

    private void BroadcastBotClears(IReadOnlyList<AiBotPlayer> bots)
    {
        Span<byte> clearPlayer = stackalloc byte[1];
        foreach (var bot in bots)
        {
            clearPlayer[0] = bot.PlayerId;
            BroadcastAll(ServerMessageId.ClearPlayer, clearPlayer);
            BroadcastLobbyExcept(bot.PlayerId, ServerMessageId.ClearPlayer, clearPlayer);
        }
    }

    private void BroadcastBotJoins(IReadOnlyList<AiBotPlayer> bots)
    {
        Span<byte> joinData = stackalloc byte[ServerJoinDataPacket.Size];
        Span<byte> playerData = stackalloc byte[ServerPlayerDataPacket.Size];
        Span<byte> updatePayload = stackalloc byte[ServerUpdatePacket.Size];
        Span<byte> points = stackalloc byte[ServerPointsUpdatePacket.Size];

        foreach (var bot in bots)
        {
            new ServerJoinDataPacket(
                bot.PlayerId,
                mayor: bot.IsMayor ? (byte)1 : (byte)0,
                bot.CityId).Write(joinData);
            BroadcastAll(ServerMessageId.JoinData, joinData);

            WriteBotPlayerDataPacket(playerData, bot);
            BroadcastAll(ServerMessageId.PlayerData, playerData);
            BroadcastLobbyExcept(0, ServerMessageId.PlayerData, playerData);

            if (_simulation.TryGetNetworkPlayerSnapshot(bot.PlayerId, out var snapshot))
            {
                snapshot.ToPacket().Write(updatePayload);
                BroadcastAll(ServerMessageId.Update, updatePayload);
            }

            new ServerPointsUpdatePacket(bot.PlayerId, 0, 0).Write(points);
            BroadcastAll(ServerMessageId.PointsUpdate, points);
        }
    }

    private void BroadcastAiCityUpdates(float deltaSeconds)
    {
        var change = SyncAiCities();
        if (!string.IsNullOrWhiteSpace(change.Message))
        {
            Console.WriteLine(change.Message);
        }

        _aiCity.AdvanceRedeploy(deltaSeconds);
        var redeploy = _aiCity.TryRedeployDue(
            _simulation,
            _cities,
            _mayors,
            AllocateBotPlayerId,
            ReleaseBotPlayerId,
            SnapshotHumanPlayers(),
            HumanBlocksAiRedeploy);
        if (redeploy.Spawned.Count > 0 || !string.IsNullOrWhiteSpace(redeploy.Message))
        {
            if (!string.IsNullOrWhiteSpace(redeploy.Message))
            {
                Console.WriteLine(redeploy.Message);
            }

            ApplyAiPresenceChange(redeploy);
        }

        if (!_aiCity.IsActive)
        {
            return;
        }

        _aiGoalRefreshSeconds -= deltaSeconds;
        if (_aiGoalRefreshSeconds <= 0f)
        {
            _aiGoalRefreshSeconds = 2.5f;
            _aiCity.RefreshAttackGoals(_simulation, SnapshotHumanPlayers());
        }

        Span<byte> updatePayload = stackalloc byte[ServerUpdatePacket.Size];
        foreach (var bot in _aiCity.Bots)
        {
            if (_simulation.IsNetworkPlayerDead(bot.PlayerId))
            {
                continue;
            }

            if (!_simulation.TryGetNetworkPlayerSnapshot(bot.PlayerId, out var snapshot))
            {
                continue;
            }

            snapshot.ToPacket().Write(updatePayload);
            BroadcastAll(ServerMessageId.Update, updatePayload);
        }

        Span<byte> shotPayload = stackalloc byte[ServerShotPacket.Size];
        while (_simulation.TryConsumeBotNetworkShot(out var shot))
        {
            shot.Write(shotPayload);
            BroadcastAll(ServerMessageId.Shoot, shotPayload);
        }
    }

    private void SendAiBotsToJoiner(ClientSession joiner)
    {
        if (!_aiCity.IsActive)
        {
            return;
        }

        Span<byte> joinData = stackalloc byte[ServerJoinDataPacket.Size];
        Span<byte> updatePayload = stackalloc byte[ServerUpdatePacket.Size];
        Span<byte> playerData = stackalloc byte[ServerPlayerDataPacket.Size];
        foreach (var bot in _aiCity.Bots)
        {
            new ServerJoinDataPacket(
                bot.PlayerId,
                mayor: bot.IsMayor ? (byte)1 : (byte)0,
                bot.CityId).Write(joinData);
            joiner.SendServer(ServerMessageId.JoinData, joinData);

            WriteBotPlayerDataPacket(playerData, bot);
            joiner.SendServer(ServerMessageId.PlayerData, playerData);

            if (_simulation.TryGetNetworkPlayerSnapshot(bot.PlayerId, out var snapshot))
            {
                snapshot.ToPacket().Write(updatePayload);
                joiner.SendServer(ServerMessageId.Update, updatePayload);
            }
        }
    }

    private static void WriteBotPlayerDataPacket(Span<byte> playerData, AiBotPlayer bot)
    {
        playerData.Clear();
        playerData[0] = bot.PlayerId;
        WriteFixedAscii(playerData.Slice(1, 16), bot.DisplayName);
        WriteFixedAscii(playerData.Slice(17, 16), "AI");
        playerData[33] = 1;
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
                // Ban/shutdown may remove the session mid-loop; stop reading its buffer.
                if (!_sessions.ContainsKey(session.PlayerId))
                {
                    break;
                }
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
                if (session.State == PlayerSessionState.Meeting)
                {
                    SendLobbyStatus(session);
                }

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
            case ClientMessageId.AutoBuild:
                HandleAutoBuild(session, packet.Payload.Span);
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
            case ClientMessageId.Successor:
                HandleSuccessor(session, packet.Payload.Span);
                break;
            case ClientMessageId.SetMayor:
                HandleSetMayor(session, packet.Payload.Span);
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
            case ClientMessageId.Admin:
                HandleAdmin(session, packet.Payload.Span);
                break;
            case ClientMessageId.ChangeNews:
                HandleChangeNews(session, packet.Payload.Span);
                break;
            case ClientMessageId.StartingCity:
                HandleStartingCityRequest(session);
                break;
            case ClientMessageId.ChangeStartingCity:
                HandleChangeStartingCity(session, packet.Payload.Span);
                break;
            case ClientMessageId.RequestRankBoard:
                HandleRankBoardRequest(session, packet.Payload.Span);
                break;
            case ClientMessageId.AdminEditRequest:
                HandleAdminEditRequest(session, packet.Payload.Span);
                break;
            case ClientMessageId.AdminEdit:
                HandleAdminEdit(session, packet.Payload.Span);
                break;
            case ClientMessageId.TcpPing:
                session.SendServer(ServerMessageId.TcpPong, " "u8);
                break;
            case ClientMessageId.ClickPlayer:
                HandleClickPlayer(session, packet.Payload.Span);
                break;
            case ClientMessageId.RightClickCity:
                HandleRightClickCity(session, packet.Payload.Span);
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
            if (_accounts.IsBanned(displayName))
            {
                session.SendServer(ServerMessageId.Error, "X"u8);
                return;
            }

            town = "Buenos Aires";
            session.IsGuest = true;
            session.RegisteredUsername = null;
        }
        else
        {
            if (_accounts.IsBanned(username))
            {
                session.SendServer(ServerMessageId.Error, "X"u8);
                return;
            }

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
            session.MonthlyPoints = account.MonthlyPoints;
            session.Orbs = account.Orbs;
            session.Assists = account.Assists;
        }

        session.DisplayName = displayName;
        session.Town = town;
        session.State = PlayerSessionState.LoggedIn;

        Span<byte> loginCorrect = stackalloc byte[2];
        loginCorrect[0] = session.PlayerId;
        // Bit0 = success, bit1 = admin (clients that ignore bit1 still see non-zero success).
        loginCorrect[1] = (byte)(1 | (session.IsAdmin ? 2 : 0));
        session.SendServer(ServerMessageId.LoginCorrect, loginCorrect);

        SendNews(session);

        Span<byte> playerData = stackalloc byte[ServerPlayerDataPacket.Size];
        WritePlayerDataPacket(playerData, session);
        BroadcastLobbyExcept(session.PlayerId, ServerMessageId.PlayerData, playerData);
        session.SendServer(ServerMessageId.PlayerData, playerData);
        SendCurrentPlayers(session);

        Span<byte> points = stackalloc byte[ServerPointsUpdatePacket.Size];
        CreatePointsUpdatePacket(session).Write(points);
        BroadcastLobbyExcept(session.PlayerId, ServerMessageId.PointsUpdate, points);
        session.SendServer(ServerMessageId.PointsUpdate, points);
    }

    /// <summary>Legacy <c>SendCurrentPlayers</c> — existing roster + points for the joiner.</summary>
    private void SendCurrentPlayers(ClientSession joiner)
    {
        Span<byte> playerData = stackalloc byte[ServerPlayerDataPacket.Size];
        Span<byte> points = stackalloc byte[ServerPointsUpdatePacket.Size];
        foreach (var other in GetLobbySessions())
        {
            if (other.PlayerId == joiner.PlayerId || other.State < PlayerSessionState.LoggedIn)
            {
                continue;
            }

            WritePlayerDataPacket(playerData, other);
            joiner.SendServer(ServerMessageId.PlayerData, playerData);

            CreatePointsUpdatePacket(other).Write(points);
            joiner.SendServer(ServerMessageId.PointsUpdate, points);
        }
    }

    private static void WritePlayerDataPacket(Span<byte> playerData, ClientSession session)
    {
        playerData.Clear();
        playerData[0] = session.PlayerId;
        WriteFixedAscii(playerData.Slice(1, 16), session.DisplayName);
        WriteFixedAscii(playerData.Slice(17, 16), session.Town);
        playerData[33] = 1;
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
        SendLobbyStatus(session);
    }

    private void SendLobbyStatus(ClientSession session)
    {
        var online = 0;
        var inCity = 0;
        var occupied = new Dictionary<byte, CityOccupancy>();
        foreach (var other in _sessions.Values)
        {
            online++;
            if (!other.IsInGame || !CityCatalog.IsValidCityId(other.CityId))
            {
                continue;
            }

            inCity++;
            if (!occupied.TryGetValue(other.CityId, out var row))
            {
                row = new CityOccupancy();
            }

            row.Humans++;
            if (other.IsMayor && string.IsNullOrEmpty(row.Mayor))
            {
                row.Mayor = other.DisplayName;
            }

            occupied[other.CityId] = row;
        }

        if (_aiCity.IsActive)
        {
            foreach (var bot in _aiCity.Bots)
            {
                if (!CityCatalog.IsValidCityId(bot.CityId))
                {
                    continue;
                }

                if (!occupied.TryGetValue(bot.CityId, out var row))
                {
                    row = new CityOccupancy();
                }

                row.Ai++;
                if (bot.IsMayor && string.IsNullOrEmpty(row.Mayor))
                {
                    row.Mayor = bot.DisplayName;
                }

                occupied[bot.CityId] = row;
            }
        }

        var lines = new List<string>
        {
            $"Server: {online} online, {inCity} in a city.",
        };
        if (occupied.Count == 0)
        {
            lines.Add("No cities are occupied.");
        }
        else
        {
            foreach (var pair in occupied.OrderBy(pair => pair.Key))
            {
                var row = pair.Value;
                var line = new System.Text.StringBuilder();
                line.Append(CityCatalog.GetName(pair.Key)).Append(": ");
                if (row.Humans > 0)
                {
                    line.Append(row.Humans).Append(row.Humans == 1 ? " player" : " players");
                }

                if (row.Ai > 0)
                {
                    if (row.Humans > 0)
                    {
                        line.Append(", ");
                    }

                    line.Append(row.Ai).Append(" AI");
                }

                if (!string.IsNullOrEmpty(row.Mayor))
                {
                    line.Append(" (Mayor: ").Append(row.Mayor).Append(')');
                }

                lines.Add(line.ToString());
            }
        }

        foreach (var line in lines)
        {
            var text = line.Length > 200 ? line[..200] : line;
            SendAsciiNews(session, text);
        }
    }

    private static void SendAsciiNews(ClientSession session, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        const int chunkSize = 220;
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);
        for (var offset = 0; offset < bytes.Length; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, bytes.Length - offset);
            session.SendServer(ServerMessageId.AppendNews, bytes.AsSpan(offset, length));
        }
    }

    private void HandleRankBoardRequest(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (session.State is not (PlayerSessionState.Meeting or PlayerSessionState.LoggedIn))
        {
            return;
        }

        var board = payload.Length > 0 ? payload[0] : (byte)0;
        if (board > 3)
        {
            board = 0;
        }

        List<(string Name, int Points)> rows;
        if (board == 3)
        {
            rows = _accounts.ListTopByPointsPerDeath(RankBoardPacket.MaxRows);
        }
        else
        {
            var column = board switch
            {
                1 => "monthly_points",
                2 => "season_points",
                _ => "points",
            };

            var top = _accounts.ListTopByPoints(column, RankBoardPacket.MaxRows);
            rows = new List<(string Name, int Points)>(top.Count);
            foreach (var account in top)
            {
                var name = string.IsNullOrWhiteSpace(account.DisplayName) ? account.Username : account.DisplayName;
                var points = board switch
                {
                    1 => account.MonthlyPoints,
                    2 => account.SeasonPoints,
                    _ => account.Points,
                };
                rows.Add((name, points));
            }
        }

        foreach (var chunk in RankBoardPacket.CreateChunks(board, _accounts.SeasonName, rows))
        {
            session.SendServer(ServerMessageId.RankBoard, chunk);
        }
    }

    private struct CityOccupancy
    {
        public int Humans;
        public int Ai;
        public string? Mayor;
    }

    private void SendCityList(ClientSession session)
    {
        // Remake: 1-byte AddRemCity with CityId=255 clears the meeting list before rebuild.
        Span<byte> clearList = stackalloc byte[1];
        clearList[0] = 255;
        session.SendServer(ServerMessageId.AddRemCity, clearList);

        Span<byte> payload = stackalloc byte[3];
        foreach (var entry in _cities.BuildCityList(_mayors, GetLobbySessions()))
        {
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

        if (IsSessionBanned(session))
        {
            session.SendServer(ServerMessageId.Error, "X"u8);
            RemoveSession(session.PlayerId);
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

        if (slot.DenyApplicants)
        {
            session.SendServer(ServerMessageId.MayorDeclined, " "u8);
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
        BroadcastCityListToMeetingClients();
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

    private void LeaveGame(ClientSession session, bool showLeftMessage = true, bool transferMayor = true)
    {
        if (session.State != PlayerSessionState.InGame)
        {
            return;
        }

        if (showLeftMessage)
        {
            NotifyPlayerLeftBattlefield(session);
        }

        var slot = _cities.GetOrCreate(session.CityId);
        if (slot.SuccessorPlayerId == session.PlayerId)
        {
            slot.SuccessorPlayerId = null;
        }

        // Applicant who somehow leaves mid-interview (or was the hiring target).
        CancelInterviewForApplicant(session.PlayerId);

        if (session.IsMayor)
        {
            // Mayor leaving must release a stuck interview applicant.
            if (slot.HiringApplicantId.HasValue
                && TryGetSession(slot.HiringApplicantId.Value, out var applicant))
            {
                slot.HiringApplicantId = null;
                if (applicant.State == PlayerSessionState.Interview)
                {
                    applicant.State = PlayerSessionState.Meeting;
                    applicant.SendServer(ServerMessageId.MayorDeclined, " "u8);
                }
            }
            else
            {
                slot.HiringApplicantId = null;
            }

            // Notify client IsMayor=false before transferring (TransferMayor promotes successor).
            SetMayor(session, isMayor: false);
            if (transferMayor)
            {
                TransferMayor(session.CityId, excludedPlayerId: session.PlayerId);
            }

            slot.SuccessorPlayerId = null;
        }

        _simulation.TryRemoveNetworkPlayer(session.PlayerId);
        session.State = PlayerSessionState.Meeting;
        session.IsMayor = false;
        session.HasCityAssignment = false;
        BroadcastCityListToMeetingClients();
    }

    /// <summary>
    /// Legacy leave: <c>smChatCommand</c> id+69 to in-game peers, then <c>smClearPlayer</c> to lobby.
    /// </summary>
    private void NotifyPlayerLeftBattlefield(ClientSession session)
    {
        Span<byte> chatCommand = stackalloc byte[2];
        chatCommand[0] = session.PlayerId;
        chatCommand[1] = 69; // 'E' — has left the battlefield
        BroadcastExcept(session.PlayerId, ServerMessageId.ChatCommand, chatCommand);

        Span<byte> clearPlayer = stackalloc byte[1];
        clearPlayer[0] = session.PlayerId;
        BroadcastLobbyExcept(session.PlayerId, ServerMessageId.ClearPlayer, clearPlayer);
    }

    private int CountInGamePlayersInCity(byte cityId, byte? excludedPlayerId = null)
    {
        var count = 0;
        foreach (var player in GetInGameSessions())
        {
            if (player.CityId != cityId || player.PlayerId == excludedPlayerId)
            {
                continue;
            }

            count++;
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

    private bool IsSessionBanned(ClientSession session)
    {
        if (!string.IsNullOrEmpty(session.RegisteredUsername)
            && _accounts.IsBanned(session.RegisteredUsername))
        {
            return true;
        }

        return !string.IsNullOrEmpty(session.DisplayName) && _accounts.IsBanned(session.DisplayName);
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
        if (!_worldReady || !session.HasCityAssignment)
        {
            return;
        }

        if (IsSessionBanned(session))
        {
            session.SendServer(ServerMessageId.Error, "X"u8);
            RemoveSession(session.PlayerId);
            return;
        }

        Vector2 spawn;
        if (_simulation.TryGetCityRespawnPosition(session.CityId, out var openSpawn, out _))
        {
            spawn = openSpawn;
        }
        else if (CityBuildInitializer.TryGetCommandCenterGridForCity(
                     session.CityId,
                     _simulation.TileMap,
                     out var gridX,
                     out var gridY))
        {
            spawn = _simulation.FindOpenTankSpawnNear(
                CommandCenterLookup.GetRespawnPositionFromGridAnchor(gridX, gridY));
        }
        else
        {
            spawn = Vector2.Zero;
        }

        _simulation.CreateNetworkPlayerEntity(spawn, session.PlayerId, session.CityId);
        session.State = PlayerSessionState.InGame;
        EnsureMayorAssigned(session);
        _simulation.EnsureCityBuild(session.CityId);

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

        Span<byte> existingJoin = stackalloc byte[ServerJoinDataPacket.Size];
        Span<byte> updatePayload = stackalloc byte[ServerUpdatePacket.Size];
        foreach (var other in GetInGameSessions())
        {
            if (other.PlayerId == session.PlayerId)
            {
                continue;
            }

            new ServerJoinDataPacket(
                other.PlayerId,
                mayor: other.IsMayor ? (byte)1 : (byte)0,
                other.CityId).Write(existingJoin);
            session.SendServer(ServerMessageId.JoinData, existingJoin);

            if (_simulation.TryGetNetworkPlayerSnapshot(other.PlayerId, out var snapshot))
            {
                snapshot.ToPacket().Write(updatePayload);
                session.SendServer(ServerMessageId.Update, updatePayload);
            }
        }

        SendJoinWorldSnapshot(session);
        SendCanBuildSnapshot(session);
        SendFactoryItemCountSnapshot(session);
        SendPlayerDataSnapshot(session);
        SendPointsSnapshot(session);
        BroadcastPointsUpdate(session);
        SendAiBotsToJoiner(session);

        Span<byte> joinerPlayerData = stackalloc byte[ServerPlayerDataPacket.Size];
        WritePlayerDataPacket(joinerPlayerData, session);
        BroadcastExcept(session.PlayerId, ServerMessageId.PlayerData, joinerPlayerData);

        BroadcastCityListToMeetingClients();

        Console.WriteLine($"Player {session.DisplayName} joined at ({spawn.X}, {spawn.Y})");
    }

    /// <summary>Legacy roster names for players already on the battlefield.</summary>
    private void SendPlayerDataSnapshot(ClientSession session)
    {
        Span<byte> playerData = stackalloc byte[ServerPlayerDataPacket.Size];
        foreach (var other in GetInGameSessions())
        {
            if (other.PlayerId == session.PlayerId)
            {
                continue;
            }

            WritePlayerDataPacket(playerData, other);
            session.SendServer(ServerMessageId.PlayerData, playerData);
        }
    }

    private void SendCanBuildSnapshot(ClientSession session)
    {
        if (!session.IsMayor && !session.IsAdmin)
        {
            return;
        }

        Span<byte> canBuildPayload = stackalloc byte[ServerCanBuildPacket.Size];
        foreach (var packet in _buildPopSync.CreateCanBuildSnapshot(_simulation, session.CityId))
        {
            packet.Write(canBuildPayload);
            session.SendServer(ServerMessageId.CanBuild, canBuildPayload);
        }

        _buildPopSync.Reset(_simulation, session.CityId);
    }

    private void SendFactoryItemCountSnapshot(ClientSession session)
    {
        Span<byte> payload = stackalloc byte[ServerItemCountPacket.Size];
        foreach (var packet in _factoryItemCountSync.CreateItemCountSnapshot(_simulation))
        {
            packet.Write(payload);
            session.SendServer(ServerMessageId.ItemCount, payload);
        }

        _factoryItemCountSync.Reset(_simulation);
    }

    private void BroadcastBuildPopSync()
    {
        Span<byte> canBuildPayload = stackalloc byte[ServerCanBuildPacket.Size];
        foreach (var cityId in _simulation.EnumerateCityBuildIds())
        {
            foreach (var packet in _buildPopSync.CollectCanBuildChanges(_simulation, cityId))
            {
                packet.Write(canBuildPayload);
                BroadcastCanBuild(canBuildPayload, cityId);
            }
        }

        Span<byte> popPayload = stackalloc byte[ServerUpdatePopPacket.Size];
        foreach (var packet in _buildPopSync.CollectPopulationChanges(_simulation))
        {
            packet.Write(popPayload);
            BroadcastAll(ServerMessageId.UpdatePop, popPayload);
        }
    }

    private void BroadcastFactoryItemCountSync()
    {
        Span<byte> payload = stackalloc byte[ServerItemCountPacket.Size];
        foreach (var packet in _factoryItemCountSync.CollectItemCountChanges(_simulation))
        {
            packet.Write(payload);
            BroadcastAll(ServerMessageId.ItemCount, payload);
        }
    }

    private void BroadcastFactoryAddItems()
    {
        Span<byte> payload = stackalloc byte[ServerAddItemPacket.Size];
        while (_simulation.TryConsumeFactoryAddItem(out var addItem))
        {
            addItem.Write(payload);
            BroadcastAll(ServerMessageId.AddItem, payload);
        }

        Span<byte> pickedUp = stackalloc byte[ServerPickedUpPacket.Size];
        while (_simulation.TryConsumeFactoryDeposit(out var deposit))
        {
            if (!TryGetSession(deposit.PlayerId, out var session))
            {
                continue;
            }

            new ServerPickedUpPacket(0, active: 0, (byte)deposit.ItemType).Write(pickedUp);
            session.SendServer(ServerMessageId.PickedUp, pickedUp);
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

    private void BroadcastCanBuild(ReadOnlySpan<byte> payload, int cityId)
    {
        foreach (var session in GetInGameSessions())
        {
            if ((session.IsMayor || session.IsAdmin) && session.CityId == cityId)
            {
                session.SendServer(ServerMessageId.CanBuild, payload);
            }
        }
    }

    private void SendJoinWorldSnapshot(ClientSession session)
    {
        var snapshot = new JoinWorldSnapshot();
        _simulation.CollectJoinSnapshot(snapshot);

        Span<byte> buildingPayload = stackalloc byte[ServerBuildingPacket.Size];
        foreach (var removed in snapshot.RemovedBuildings)
        {
            removed.Write(buildingPayload);
            session.SendServer(ServerMessageId.RemBuilding, buildingPayload);
        }

        foreach (var building in snapshot.Buildings)
        {
            building.Write(buildingPayload);
            session.SendServer(ServerMessageId.NewBuilding, buildingPayload);
        }

        Span<byte> itemPayload = stackalloc byte[ServerAddItemPacket.Size];
        foreach (var item in snapshot.Items)
        {
            item.Write(itemPayload);
            session.SendServer(ServerMessageId.AddItem, itemPayload);
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
            // Legacy anti-cheat: warp the cheater/desynced client back to last good position.
            if (_simulation.TryGetNetworkPlayerPosition(session.PlayerId, out var position))
            {
                Span<byte> warp = stackalloc byte[ServerStateGamePacket.Size];
                new ServerStateGamePacket(
                    (ushort)Math.Clamp((int)position.X, 0, ushort.MaxValue),
                    (ushort)Math.Clamp((int)position.Y, 0, ushort.MaxValue),
                    session.CityId).Write(warp);
                session.SendServer(ServerMessageId.Warp, warp);
            }

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
                out var addItem,
                session.CityId))
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

    private void HandleAutoBuild(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || payload.Length < AutoBuildPacket.LegacySize)
        {
            return;
        }

        var request = AutoBuildPacket.Read(payload);
        if (!AutoBuildDesign.TryNormalize(request.Filename, out var designName))
        {
            ReplyAutoBuild(session, AutoBuildOutcome.Denied, request.Filename, 0);
            return;
        }

        var cityId = session.CityId;
        var isOrbable = _simulation.TryGetCityBuild(cityId, out var build) && build.IsOrbable;
        var isDead = _simulation.IsNetworkPlayerDead(session.PlayerId);
        if (!AutoBuildRules.MayLoad(session.IsAdmin, session.IsMayor, isDead, isOrbable)
            || (!session.IsAdmin && !_simulation.IsNetworkPlayerNearCommandCenter(session.PlayerId)))
        {
            ReplyAutoBuild(session, AutoBuildOutcome.Denied, designName, 0);
            return;
        }

        if (!CityCatalog.IsValidCityId(cityId))
        {
            ReplyAutoBuild(session, AutoBuildOutcome.MissingFile, designName, 0);
            return;
        }

        var path = CityLayoutPaths.FindLegacyCityLayout(CityCatalog.GetName(cityId), designName);
        if (path is null)
        {
            ReplyAutoBuild(session, AutoBuildOutcome.MissingFile, designName, 0);
            return;
        }

        CityLayout layout;
        try
        {
            layout = CityLayoutParser.ParseFile(path);
        }
        catch (IOException)
        {
            ReplyAutoBuild(session, AutoBuildOutcome.MissingFile, designName, 0);
            return;
        }

        var placed = new List<ServerBuildingPacket>();
        _simulation.AutoBuildLayout(cityId, layout.Buildings, placed);

        Span<byte> broadcast = stackalloc byte[ServerBuildingPacket.Size];
        foreach (var building in placed)
        {
            building.Write(broadcast);
            BroadcastAll(ServerMessageId.NewBuilding, broadcast);
        }

        ReplyAutoBuild(session, AutoBuildOutcome.Loaded, designName, (byte)Math.Min(placed.Count, byte.MaxValue));
    }

    private static void ReplyAutoBuild(ClientSession session, AutoBuildOutcome outcome, string designName, byte placedCount)
    {
        Span<byte> payload = stackalloc byte[AutoBuildPacket.Size];
        new AutoBuildPacket(outcome == AutoBuildOutcome.Loaded, designName, outcome, placedCount).Write(payload);
        session.SendServer(ServerMessageId.AutoBuild, payload);
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
        BroadcastTeamUnderAttack(building.City);
    }

    private void BroadcastTeamUnderAttack(byte cityId)
    {
        foreach (var session in GetInGameSessions())
        {
            if (session.CityId != cityId)
            {
                continue;
            }

            // Legacy SendTeam(smUnderAttack) — payload content is ignored by the client.
            session.SendServer(ServerMessageId.UnderAttack, ReadOnlySpan<byte>.Empty);
        }
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

    private void HandleAdmin(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsAdmin || payload.Length < ClientAdminPacket.Size)
        {
            return;
        }

        var request = ClientAdminPacket.Read(payload);
        switch (request.Command)
        {
            case AdminCommands.JoinCity:
                ApplyAdminJoinCity(session, (byte)Math.Min(request.TargetId, (ushort)byte.MaxValue));
                break;
            case AdminCommands.Shutdown:
                ApplyAdminShutdown(session);
                break;
            case AdminCommands.SpawnItem:
                ApplyAdminSpawnItem(session, request.TargetId);
                break;
            case AdminCommands.RequestBans:
                ApplyAdminRequestBans(session);
                break;
            case AdminCommands.Unban:
                ApplyAdminUnban(session, request.TargetId, payload);
                break;
            case AdminCommands.RequestNews:
                ApplyAdminRequestNews(session);
                break;
            case AdminCommands.Warp:
            case AdminCommands.Summon:
            case AdminCommands.Kick:
            case AdminCommands.Ban:
                if (request.TargetId is 0 or > byte.MaxValue)
                {
                    return;
                }

                var targetId = (byte)request.TargetId;
                if (targetId == session.PlayerId || !TryGetSession(targetId, out var target))
                {
                    return;
                }

                if (target.IsAdmin && request.Command is AdminCommands.Kick or AdminCommands.Ban)
                {
                    return;
                }

                switch (request.Command)
                {
                    case AdminCommands.Warp:
                        ApplyAdminWarp(session, target);
                        break;
                    case AdminCommands.Summon:
                        ApplyAdminSummon(session, target);
                        break;
                    case AdminCommands.Kick:
                        ApplyAdminKickOrBan(session, target, AdminCommands.Kick, banAccount: false);
                        break;
                    case AdminCommands.Ban:
                        ApplyAdminKickOrBan(session, target, AdminCommands.Ban, banAccount: true);
                        break;
                }

                break;
        }
    }

    private void ApplyAdminShutdown(ClientSession admin)
    {
        Console.WriteLine($"Shutdown::{admin.DisplayName}");
        // Defer Stop() until after the current ReadSessions/Update pass so we don't
        // dispose sockets mid-iteration.
        _shutdownRequested = true;
    }

    private void ApplyAdminSpawnItem(ClientSession admin, ushort itemTypeId)
    {
        if (!admin.IsInGame || !Enum.IsDefined(typeof(ItemType), (int)itemTypeId))
        {
            return;
        }

        var itemType = (ItemType)itemTypeId;
        if (!_simulation.TryAdminSpawnItemForNetworkPlayer(admin.PlayerId, itemType, out var pickedUp))
        {
            return;
        }

        Console.WriteLine($"Spawn Item::{admin.DisplayName}::{itemTypeId}");
        Span<byte> pickedUpPayload = stackalloc byte[ServerPickedUpPacket.Size];
        pickedUp.Write(pickedUpPayload);
        admin.SendServer(ServerMessageId.PickedUp, pickedUpPayload);
    }

    private void ApplyAdminRequestBans(ClientSession admin)
    {
        Console.WriteLine($"Request Bans::{admin.DisplayName}");
        var bans = _accounts.ListBans();
        Span<byte> payload = stackalloc byte[ServerBanPacket.Size];
        foreach (var ban in bans)
        {
            // Remake: IpAddress field carries BannedBy (no IP column in SQLite bans).
            new ServerBanPacket(ban.Username, ban.BannedBy, ban.Reason).Write(payload);
            admin.SendServer(ServerMessageId.Ban, payload);
        }
    }

    private void ApplyAdminUnban(ClientSession admin, ushort banIndex, ReadOnlySpan<byte> payload)
    {
        string? username = null;
        if (payload.Length > ClientAdminPacket.Size)
        {
            username = ReadNullTerminatedAscii(payload.Slice(ClientAdminPacket.Size));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            var bans = _accounts.ListBans();
            if (banIndex >= bans.Count)
            {
                return;
            }

            username = bans[banIndex].Username;
        }

        if (!_accounts.TryRemoveBan(username))
        {
            return;
        }

        Console.WriteLine($"Unban::{admin.DisplayName}::{username}");
    }

    private void ApplyAdminRequestNews(ClientSession admin)
    {
        Console.WriteLine($"Request News::{admin.DisplayName}");
        SendNews(admin);
    }

    private void HandleChangeNews(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsAdmin)
        {
            return;
        }

        var news = ReadNullTerminatedAscii(payload);
        if (news.Length > ServerNewsStore.MaxNewsLength)
        {
            return;
        }

        if (!_news.TrySetNews(news))
        {
            return;
        }

        Console.WriteLine($"ChangeNews::{session.DisplayName}");
        // Push updated news to the editing admin (legacy smAppendNews).
        SendNews(session);
    }

    private void HandleStartingCityRequest(ClientSession session)
    {
        Span<byte> payload = stackalloc byte[StartingCityPacket.Size];
        new StartingCityPacket(_cities.StartingCityId).Write(payload);
        session.SendServer(ServerMessageId.StartingCity, payload);
    }

    private void HandleChangeStartingCity(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsAdmin || payload.Length < StartingCityPacket.Size)
        {
            return;
        }

        var request = StartingCityPacket.Read(payload);
        if (request.CityId is < 0 or > byte.MaxValue
            || !_cities.TrySetStartingCity((byte)request.CityId))
        {
            return;
        }

        Console.WriteLine($"ChangeStartingCity::{session.DisplayName}::{request.CityId}");
        BroadcastCityListToMeetingClients();
        HandleStartingCityRequest(session);
    }

    private void HandleAdminEditRequest(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsAdmin || payload.Length < ClientAdminEditRequestPacket.Size)
        {
            return;
        }

        var request = ClientAdminEditRequestPacket.Read(payload);
        if (!_accounts.TryGetAccountForAdminEdit(request.Username, out var account) || account is null)
        {
            return;
        }

        Console.WriteLine($"AdminEditRequest::{session.DisplayName}::{account.Username}");
        Span<byte> response = stackalloc byte[AdminEditPacket.Size];
        ToAdminEditPacket(account, password: string.Empty).Write(response);
        session.SendServer(ServerMessageId.AdminEdit, response);
    }

    private void HandleAdminEdit(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsAdmin || payload.Length < AdminEditPacket.Size)
        {
            return;
        }

        var edit = AdminEditPacket.Read(payload);
        if (!ApplyAdminEditFromPacket(edit))
        {
            return;
        }

        Console.WriteLine($"AdminEdit::{session.DisplayName}::{edit.Username}");
    }

    private bool ApplyAdminEditFromPacket(in AdminEditPacket edit)
    {
        if (!_accounts.TryApplyAdminEdit(
                edit.Username,
                string.IsNullOrWhiteSpace(edit.Password) ? null : edit.Password,
                edit.FullName,
                edit.Town,
                edit.Email,
                edit.State,
                edit.Points,
                edit.Deaths,
                isAdmin: edit.PlayerType != 0))
        {
            return false;
        }

        lock (_sync)
        {
            Span<byte> loginCorrect = stackalloc byte[2];
            Span<byte> points = stackalloc byte[ServerPointsUpdatePacket.Size];
            Span<byte> playerData = stackalloc byte[ServerPlayerDataPacket.Size];
            foreach (var online in _sessions.Values)
            {
                if (!string.Equals(online.RegisteredUsername, edit.Username, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                online.Town = string.IsNullOrWhiteSpace(edit.Town) ? online.Town : edit.Town.Trim();
                online.Points = Math.Max(0, edit.Points);
                online.Deaths = Math.Max(0, edit.Deaths);
                online.IsAdmin = edit.PlayerType != 0
                    || string.Equals(edit.Username, "admin", StringComparison.OrdinalIgnoreCase);

                loginCorrect[0] = online.PlayerId;
                loginCorrect[1] = (byte)(1 | (online.IsAdmin ? 2 : 0));
                online.SendServer(ServerMessageId.LoginCorrect, loginCorrect);

                CreatePointsUpdatePacket(online).Write(points);
                online.SendServer(ServerMessageId.PointsUpdate, points);
                BroadcastLobbyExcept(online.PlayerId, ServerMessageId.PointsUpdate, points);

                WritePlayerDataPacket(playerData, online);
                online.SendServer(ServerMessageId.PlayerData, playerData);
                BroadcastLobbyExcept(online.PlayerId, ServerMessageId.PlayerData, playerData);
            }
        }

        return true;
    }

    private static AdminEditPacket ToAdminEditPacket(AccountRecord account, string password) =>
        new(
            account.Username,
            password,
            account.Email,
            account.DisplayName,
            account.Town,
            account.State,
            account.Points,
            account.MonthlyPoints,
            account.Deaths,
            orbs: 0,
            assists: 0,
            playerType: account.IsAdmin ? 1 : 0);

    private void SendNews(ClientSession session)
    {
        var news = _news.News;
        if (string.IsNullOrEmpty(news))
        {
            return;
        }

        // Legacy SendNews chunks ~220 bytes; admin news is capped at 240.
        const int chunkSize = 220;
        var bytes = System.Text.Encoding.ASCII.GetBytes(news);
        for (var offset = 0; offset < bytes.Length; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, bytes.Length - offset);
            session.SendServer(ServerMessageId.AppendNews, bytes.AsSpan(offset, length));
        }
    }

    private static string ReadNullTerminatedAscii(ReadOnlySpan<byte> buffer)
    {
        var length = buffer.IndexOf((byte)0);
        if (length < 0)
        {
            length = buffer.Length;
        }

        if (length <= 0)
        {
            return string.Empty;
        }

        return System.Text.Encoding.ASCII.GetString(buffer[..length]).Trim();
    }

    private void ApplyAdminJoinCity(ClientSession admin, byte cityId)
    {
        if (!admin.IsInGame || !CityCatalog.IsValidCityId(cityId))
        {
            return;
        }

        Console.WriteLine($"JoinCity::{admin.DisplayName}::{cityId}");
        LeaveGame(admin);
        admin.CityId = cityId;
        admin.HasCityAssignment = true;
        JoinGame(admin);
    }

    private void ApplyAdminWarp(ClientSession admin, ClientSession target)
    {
        if (!admin.IsInGame || !target.IsInGame)
        {
            return;
        }

        if (!_simulation.TryGetNetworkPlayerPosition(target.PlayerId, out var destination))
        {
            return;
        }

        Console.WriteLine($"Warp::{admin.DisplayName}::{target.DisplayName}");
        TeleportNetworkPlayer(admin, destination);
    }

    private void ApplyAdminSummon(ClientSession admin, ClientSession target)
    {
        if (!admin.IsInGame || !target.IsInGame)
        {
            return;
        }

        if (!_simulation.TryGetNetworkPlayerPosition(admin.PlayerId, out var destination))
        {
            return;
        }

        Console.WriteLine($"Summon::{admin.DisplayName}::{target.DisplayName}");
        TeleportNetworkPlayer(target, destination);
    }

    private void TeleportNetworkPlayer(ClientSession player, Vector2 destination)
    {
        if (!_simulation.TryForceNetworkPlayerPosition(player.PlayerId, destination))
        {
            return;
        }

        Span<byte> warp = stackalloc byte[ServerStateGamePacket.Size];
        new ServerStateGamePacket(
            (ushort)Math.Clamp((int)destination.X, 0, ushort.MaxValue),
            (ushort)Math.Clamp((int)destination.Y, 0, ushort.MaxValue),
            player.CityId).Write(warp);
        player.SendServer(ServerMessageId.Warp, warp);

        if (_simulation.TryGetNetworkPlayerSnapshot(player.PlayerId, out var snapshot))
        {
            Span<byte> update = stackalloc byte[ServerUpdatePacket.Size];
            snapshot.ToPacket().Write(update);
            BroadcastAll(ServerMessageId.Update, update);
        }
    }

    private void ApplyAdminKickOrBan(
        ClientSession admin,
        ClientSession target,
        byte command,
        bool banAccount)
    {
        if (banAccount)
        {
            var banName = target.RegisteredUsername ?? target.DisplayName;
            _accounts.TryAddBan(banName, admin.DisplayName, reason: "ban");
        }

        if (target.IsInGame)
        {
            LeaveGame(target);
        }

        Span<byte> adminPayload = stackalloc byte[ServerAdminPacket.Size];
        new ServerAdminPacket(admin.PlayerId, target.PlayerId, command).Write(adminPayload);
        BroadcastExcept(target.PlayerId, ServerMessageId.Admin, adminPayload);
        BroadcastLobbyExcept(target.PlayerId, ServerMessageId.Admin, adminPayload);

        // Payload byte = Kick/Ban command so the victim can show the correct verb.
        Span<byte> kicked = stackalloc byte[1];
        kicked[0] = command;
        target.SendServer(ServerMessageId.Kicked, kicked);
        Console.WriteLine(
            $"{(banAccount ? "Ban" : "Kick")}::{admin.DisplayName}::{target.DisplayName}");

        // Ban must drop the TCP session; otherwise the banned client stays in the lobby
        // and can rejoin without hitting the login IsBanned check.
        if (banAccount)
        {
            RemoveSession(target.PlayerId);
        }
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
        Span<byte> payload = stackalloc byte[ServerHpPacket.Size];
        while (_simulation.TryConsumeNetworkHpEvent(out var hpEvent))
        {
            hpEvent.Write(payload);
            BroadcastAll(ServerMessageId.Hp, payload);
        }
    }

    private void ApplyDeathPointTransfers(ClientSession victim, byte killerCityId)
    {
        DeathPointTransfers.Apply(victim, killerCityId, GetInGameSessions(), (session, pointDelta) =>
        {
            AdjustSessionPoints(session, pointDelta);
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

    private void AdjustSessionPoints(ClientSession session, int delta)
    {
            if (delta != 0)
            {
                var oldRank = PlayerRankCatalog.GetRank(session.Points);
                session.Points += delta;
                if (delta > 0)
                {
                    session.MonthlyPoints += delta;
                }
            var newRank = PlayerRankCatalog.GetRank(session.Points);
            if (oldRank != newRank)
            {
                BroadcastPromotion(session.PlayerId, newRank);
            }

            if (!session.IsGuest && session.RegisteredUsername is not null)
            {
                _accounts.AdjustPoints(session.RegisteredUsername, delta);
            }
        }

        BroadcastPointsUpdate(session);
    }

    private void BroadcastPromotion(byte playerId, string rank)
    {
        var packet = new ServerPromotionPacket(playerId, rank);
        Span<byte> payload = stackalloc byte[packet.GetWriteLength()];
        packet.Write(payload);
        BroadcastAll(ServerMessageId.Promotion, payload);
    }

    private void BroadcastPendingItemLifeEvents()
    {
        Span<byte> payload = stackalloc byte[ServerItemLifePacket.Size];
        while (_simulation.TryConsumeNetworkItemLifeEvent(out var itemLifeEvent))
        {
            itemLifeEvent.Write(payload);
            BroadcastAll(ServerMessageId.ItemLife, payload);
        }
    }

    private void SendPointsSnapshot(ClientSession session)
    {
        Span<byte> payload = stackalloc byte[ServerPointsUpdatePacket.Size];
        foreach (var player in GetInGameSessions())
        {
            CreatePointsUpdatePacket(player).Write(payload);
            session.SendServer(ServerMessageId.PointsUpdate, payload);
        }
    }

    private static ServerPointsUpdatePacket CreatePointsUpdatePacket(ClientSession session) =>
        new(
            session.PlayerId,
            (uint)Math.Max(0, session.Points),
            (uint)Math.Max(0, session.Deaths),
            (uint)Math.Max(0, session.Orbs),
            (uint)Math.Max(0, session.Assists),
            (uint)Math.Max(0, session.MonthlyPoints));

    private void BroadcastPendingDeathEvents()
    {
        Span<byte> payload = stackalloc byte[ServerDeathPacket.Size];
        while (_simulation.TryConsumeNetworkDeathEvent(out var deathEvent))
        {
            deathEvent.Write(payload);
            BroadcastAll(ServerMessageId.Death, payload);

            if (TryGetSession(deathEvent.PlayerId, out var session))
            {
                RecordDeathPointChanges(session, deathEvent.KillerCity);
            }
            else if (CityCatalog.IsValidCityId(deathEvent.KillerCity))
            {
                // AI tanks are not accounts. A city that kills one still pays the legacy +2.
                foreach (var ally in GetInGameSessions())
                {
                    if (ally.CityId == deathEvent.KillerCity)
                    {
                        AdjustSessionPoints(ally, DeathPointTransfers.PointTransferAmount);
                    }
                }
            }

            Console.WriteLine($"Player {deathEvent.PlayerId} died (server combat)");
        }
    }

    private void BroadcastPendingOrbEvents()
    {
        Span<byte> payload = stackalloc byte[ServerOrbedCityPacket.Size];
        while (_simulation.TryConsumeOrbEvent(out var orbEvent))
        {
            new ServerOrbedCityPacket(
                (byte)orbEvent.VictimCityId,
                (byte)orbEvent.AttackerCityId,
                orbEvent.VictimPoints,
                orbEvent.AttackerPoints).Write(payload);
            BroadcastAll(ServerMessageId.Orbed, payload);

            ApplyOrbPointAwards(orbEvent);
            _cityDestruct.Cancel((byte)orbEvent.VictimCityId);
            BootOrbedVictims((byte)orbEvent.VictimCityId);
            KillOrbedAiCity((byte)orbEvent.VictimCityId, (byte)orbEvent.AttackerCityId);

            Console.WriteLine(
                $"City {orbEvent.VictimCityId} orbed by city {orbEvent.AttackerCityId} ({orbEvent.VictimPoints} points)");
        }
    }

    /// <summary>Legacy <c>CCity::didOrb</c> — award orb points to every in-game teammate of the orber.</summary>
    private void ApplyOrbPointAwards(in OrbEvent orbEvent)
    {
        var points = (int)orbEvent.VictimPoints;
        var awarded = false;
        foreach (var session in GetInGameSessions())
        {
            if (session.CityId != orbEvent.AttackerCityId)
            {
                continue;
            }

            if (session.PlayerId == orbEvent.OrberPlayerId)
            {
                IncrementSessionOrbs(session);
            }
            else
            {
                IncrementSessionAssists(session);
            }

            if (points <= 0)
            {
                BroadcastPointsUpdate(session);
                continue;
            }

            var before = session.Points;
            AdjustSessionPoints(session, points);
            Console.WriteLine($"Points::{session.DisplayName}::{before}->{session.Points} (+{points})");
            awarded = true;
        }

        if (!awarded && points > 0)
        {
            Console.WriteLine(
                $"Points::none city {orbEvent.AttackerCityId} worth {points} (no in-game player in that city)");
        }
    }

    private void IncrementSessionOrbs(ClientSession session)
    {
        session.Orbs++;
        if (!session.IsGuest && session.RegisteredUsername is not null)
        {
            _accounts.IncrementOrbs(session.RegisteredUsername);
        }
    }

    private void IncrementSessionAssists(ClientSession session)
    {
        session.Assists++;
        if (!session.IsGuest && session.RegisteredUsername is not null)
        {
            _accounts.IncrementAssists(session.RegisteredUsername);
        }
    }

    private void HandleClickPlayer(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || payload.Length < 1)
        {
            return;
        }

        var clicked = payload[0];
        var points = 0;
        var monthly = 0;
        var orbs = 0;
        var assists = 0;
        var deaths = 0;
        if (TryGetSession(clicked, out var target))
        {
            points = target.Points;
            monthly = target.MonthlyPoints;
            orbs = target.Orbs;
            assists = target.Assists;
            deaths = target.Deaths;
        }

        var packet = new ServerClickPlayerPacket(clicked, points, monthly, orbs, assists, deaths);
        Span<byte> buffer = stackalloc byte[ServerClickPlayerPacket.Size];
        packet.Write(buffer);
        session.SendServer(ServerMessageId.ClickPlayer, buffer);
    }

    private void HandleRightClickCity(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsInGame || payload.Length < 1 || !CityCatalog.IsValidCityId(payload[0]))
        {
            return;
        }

        var cityId = payload[0];
        var build = _simulation.EnsureCityBuild(cityId);
        build.NoteOrbableClock();
        var packet = new ServerRightClickCityPacket(
            cityId,
            build.CurrentBuildingCount,
            build.IsOrbable,
            build.Orbs,
            build.GetOrbValue(),
            build.GetUptimeMinutes());
        Span<byte> buffer = stackalloc byte[ServerRightClickCityPacket.Size];
        packet.Write(buffer);
        session.SendServer(ServerMessageId.RightClickCity, buffer);
    }

    /// <summary>Legacy <c>wasOrbed</c> removes every tank in the city. AI tanks are not sessions.</summary>
    private void KillOrbedAiCity(byte victimCityId, byte attackerCityId)
    {
        var bots = _aiCity.RetireCity(victimCityId, _simulation, _mayors, ReleaseBotPlayerId);
        if (bots.Count == 0)
        {
            return;
        }

        Span<byte> death = stackalloc byte[ServerDeathPacket.Size];
        foreach (var bot in bots)
        {
            new ServerDeathPacket(bot.PlayerId, deathType: 0, attackerCityId).Write(death);
            BroadcastAll(ServerMessageId.Death, death);
        }

        BroadcastBotClears(bots);
    }

    /// <summary>Legacy <c>CCity::wasOrbed</c> — boot victims to the meeting room.</summary>
    private void BootOrbedVictims(byte victimCityId)
    {
        foreach (var session in GetInGameSessions().Where(s => s.CityId == victimCityId).ToList())
        {
            // Legacy wasOrbed calls LeaveGame(showMessage: false, transferMayor: false).
            // The orbed packet already returns the client to the meeting room.
            LeaveGame(session, showLeftMessage: false, transferMayor: false);
        }
    }

    private void BroadcastPendingExplosionEvents()
    {
        Span<byte> payload = stackalloc byte[ServerExplosionPacket.Size];
        Span<byte> removePayload = stackalloc byte[ServerRemoveItemPacket.Size];
        while (_simulation.TryConsumeNetworkExplosionEvent(out var explosionEvent))
        {
            explosionEvent.Explosion.Write(payload);
            BroadcastAll(ServerMessageId.Explosion, payload);

            if (explosionEvent.RemovedItemId != 0)
            {
                new ServerRemoveItemPacket(explosionEvent.RemovedItemId).Write(removePayload);
                BroadcastAll(ServerMessageId.RemItem, removePayload);
            }
        }

        while (_simulation.TryConsumeDestroyedNetworkItem(out var destroyedItemId))
        {
            new ServerRemoveItemPacket(destroyedItemId).Write(removePayload);
            BroadcastAll(ServerMessageId.RemItem, removePayload);
        }
    }

    private void BroadcastPendingBombBuildingRemovals()
    {
        Span<byte> payload = stackalloc byte[ServerBuildingPacket.Size];
        while (_simulation.TryConsumeBombBuildingRemoval(out var building))
        {
            building.Write(payload);
            BroadcastAll(ServerMessageId.RemBuilding, payload);
            BroadcastTeamUnderAttack(building.City);
        }
    }

    private void BroadcastPendingRespawnEvents()
    {
        Span<byte> warpPayload = stackalloc byte[ServerStateGamePacket.Size];
        Span<byte> respawnPayload = stackalloc byte[ServerRespawnPacket.Size];
        Span<byte> updatePayload = stackalloc byte[ServerUpdatePacket.Size];
        while (_simulation.TryConsumeNetworkRespawnEvent(out var respawnEvent))
        {
            new ServerStateGamePacket(
                (ushort)respawnEvent.Position.X,
                (ushort)respawnEvent.Position.Y,
                respawnEvent.CityId).Write(warpPayload);

            if (TryGetSession(respawnEvent.PlayerId, out var session))
            {
                session.SendServer(ServerMessageId.Warp, warpPayload);
            }

            new ServerRespawnPacket(respawnEvent.PlayerId).Write(respawnPayload);
            BroadcastExcept(respawnEvent.PlayerId, ServerMessageId.Respawn, respawnPayload);

            if (_simulation.TryGetNetworkPlayerSnapshot(respawnEvent.PlayerId, out var snapshot))
            {
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

        if (session.State == PlayerSessionState.InGame)
        {
            NotifyPlayerLeftBattlefield(session);
            var slot = _cities.GetOrCreate(session.CityId);
            if (slot.SuccessorPlayerId == session.PlayerId)
            {
                slot.SuccessorPlayerId = null;
            }
        }
        else if (session.State >= PlayerSessionState.LoggedIn)
        {
            Span<byte> clearPlayer = stackalloc byte[1];
            clearPlayer[0] = session.PlayerId;
            BroadcastLobbyExcept(session.PlayerId, ServerMessageId.ClearPlayer, clearPlayer);
        }

        if (session.IsMayor)
        {
            _mayors.Remove(session.CityId, session.PlayerId);
            TransferMayor(session.CityId, excludedPlayerId: session.PlayerId);
            _cities.GetOrCreate(session.CityId).SuccessorPlayerId = null;
        }

        _simulation.TryRemoveNetworkPlayer(playerId);
        session.Dispose();
        Console.WriteLine($"Player slot {playerId} disconnected");
        BroadcastCityListToMeetingClients();
    }

    private void BroadcastLobbyExcept(byte excludedPlayerId, ServerMessageId messageId, ReadOnlySpan<byte> payload)
    {
        foreach (var session in GetLobbySessions())
        {
            if (session.PlayerId == excludedPlayerId || session.State < PlayerSessionState.LoggedIn)
            {
                continue;
            }

            session.SendServer(messageId, payload);
        }
    }

    private void EnsureMayorAssigned(ClientSession session)
    {
        _simulation.EnsureCityBuild(session.CityId);

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
                // Joining a city that already has a mayor — demote explicitly so the client
                // clears IsMayor (e.g. admin /city from a city they previously owned).
                if (session.IsMayor)
                {
                    SetMayor(session, isMayor: false);
                }
                else
                {
                    session.IsMayor = false;
                }
            }

            return;
        }

        SetMayor(session, isMayor: true);
    }

    private void TransferMayor(byte cityId, byte excludedPlayerId)
    {
        var slot = _cities.GetOrCreate(cityId);
        var roster = GetInGameSessions()
            .Select(s => (s.PlayerId, s.CityId));

        if (!MayorSuccessorResolver.TryResolve(
                cityId,
                excludedPlayerId,
                slot.SuccessorPlayerId,
                roster,
                out var successorId)
            || !TryGetSession(successorId, out var successor))
        {
            slot.SuccessorPlayerId = null;
            BeginCityAbandon(cityId, excludedPlayerId);
            return;
        }

        slot.SuccessorPlayerId = null;
        SetMayor(successor, isMayor: true);
    }

    private void HandleSuccessor(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsMayor || !session.IsInGame || payload.Length == 0)
        {
            return;
        }

        var successorId = payload[0];
        var slot = _cities.GetOrCreate(session.CityId);

        // 0 clears the designated heir (remake convenience; legacy used -1 server-side).
        if (successorId == 0)
        {
            slot.SuccessorPlayerId = null;
            return;
        }

        if (successorId == session.PlayerId
            || !TryGetSession(successorId, out var successor)
            || !successor.IsInGame
            || successor.CityId != session.CityId)
        {
            return;
        }

        slot.SuccessorPlayerId = successorId;
    }

    /// <summary>Legacy <c>cmSetMayor</c> — the mayor hands the city to a teammate and stays in the city.</summary>
    private void HandleSetMayor(ClientSession session, ReadOnlySpan<byte> payload)
    {
        if (!session.IsMayor || !session.IsInGame || payload.Length == 0)
        {
            return;
        }

        var targetId = payload[0];
        if (targetId == session.PlayerId
            || !TryGetSession(targetId, out var target)
            || !target.IsInGame
            || target.CityId != session.CityId)
        {
            return;
        }

        SetMayor(session, isMayor: false);
        SetMayor(target, isMayor: true);
        _cityDestruct.Cancel(session.CityId);
    }

    private void BeginCityAbandon(byte cityId, byte excludedPlayerId)
    {
        if (_mayors.HasMayor(cityId) || CountInGamePlayersInCity(cityId, excludedPlayerId) > 0)
        {
            _cityDestruct.Cancel(cityId);
            return;
        }

        if (!_simulation.TryGetCityBuild(cityId, out var build) || !build.IsOrbable)
        {
            DestroyAbandonedCity(cityId);
            return;
        }

        var seconds = EconomyConstants.TimerCityDestruct / 1000f;
        _cityDestruct.Schedule(cityId, seconds);
        var name = CityCatalog.IsValidCityId(cityId) ? CityCatalog.GetName(cityId) : cityId.ToString();
        Console.WriteLine($"{name} has no mayor. It will be destroyed in {seconds:0} seconds.");
    }

    private void TickAbandonedCities(float deltaSeconds)
    {
        foreach (var cityId in _cityDestruct.Tick(deltaSeconds))
        {
            if (_mayors.HasMayor(cityId))
            {
                continue;
            }

            var occupant = GetInGameSessions().FirstOrDefault(session => session.CityId == cityId);
            if (occupant is not null)
            {
                SetMayor(occupant, isMayor: true);
                continue;
            }

            DestroyAbandonedCity(cityId);
        }
    }

    private void DestroyAbandonedCity(byte cityId)
    {
        _cityDestruct.Cancel(cityId);
        _simulation.DestroyAbandonedCity(cityId);
        Span<byte> payload = stackalloc byte[1];
        payload[0] = cityId;
        BroadcastAll(ServerMessageId.DestroyCity, payload);
        var name = CityCatalog.IsValidCityId(cityId) ? CityCatalog.GetName(cityId) : cityId.ToString();
        Console.WriteLine($"{name} was destroyed.");
        BroadcastCityListToMeetingClients();
    }

    private void SetMayor(ClientSession session, bool isMayor)
    {
        session.IsMayor = isMayor;
        _simulation.SetNetworkPlayerMayor(session.PlayerId, isMayor);

        if (isMayor)
        {
            _mayors.Assign(session.CityId, session.PlayerId);
            _cityDestruct.Cancel(session.CityId);
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
