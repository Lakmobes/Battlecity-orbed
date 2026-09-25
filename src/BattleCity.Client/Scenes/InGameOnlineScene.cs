using BattleCity.Client.Assets;
using BattleCity.Client.Audio;
using BattleCity.Client.Chat;
using BattleCity.Client.Input;
using BattleCity.Client.Network;
using BattleCity.Client.Rendering;
using Arch.Core;

using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Maps;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Chat;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Gameplay;
using BattleCity.Shared.Network;
using BattleCity.Shared.Network.Packets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using NumericsVector2 = System.Numerics.Vector2;

namespace BattleCity.Client.Scenes;

public sealed class InGameOnlineScene : IScene
{
    private readonly SceneContext _context;
    private readonly GameClient _client;
    private readonly Camera2D _camera = new();
    private readonly GameSimulation _simulation = new();
    private readonly InputManager _input = new();
    private readonly MenuInputReader _menuInput = new();
    private GameplayAudioController _gameplayAudio = null!;
    private RemotePlayerSync _remotePlayers = null!;

    private RenderPipeline _renderPipeline = null!;
    private TileMap _tileMap = null!;
    private Vector2 _cameraFocus;
    private Vector2 _cameraPanOffset;
    private Vector2 _buildMenuAnchor;
    private bool _showMiniMap;
    private bool _showStatusPanel = true;
    private bool _showBuildMenu;
    private int _buildModeSlot;
    private bool _showBuildPreview;
    private int _buildPreviewGridX;
    private int _buildPreviewGridY;
    private int _buildPreviewTypeCode;
    private bool _buildPreviewIsValid;
    private bool _buildPreviewIsDemolish;
    private bool _showVirtualCursor;
    private Vector2 _virtualCursorLogical;
    private float _animationTime;
    private bool _loaded;
    private float _updateSendCooldown;
    private bool _pickUpItemPressedLastFrame;
    private bool _useMedKitPressedLastFrame;
    private bool _useCloakPressedLastFrame;
    private bool _deathReported;
    private byte? _pendingApplicantId;
    private string? _pendingApplicantName;
    private bool _denyApplicants;
    private KeyboardState _previousKeyboard;
    private bool _returnToMeeting;
    private bool _keepNetworkClientOnDispose;
    private readonly InGameChatLog _chatLog = new();
    private readonly InGameChatInput _chatInput = new();
    private bool _showSettingsMenu;
    private int _settingsSelectedIndex;

    public InGameOnlineScene(SceneContext context, GameClient client)
    {
        _context = context;
        _client = client;
        _cameraFocus = new Vector2(GameConstants.WorldSizePixels / 2f, GameConstants.WorldSizePixels / 2f);
    }

    public bool DrawsWorld => true;

    public Matrix WorldViewMatrix => _camera.ViewMatrix;

    public void LoadContent()
    {
        _renderPipeline = new RenderPipeline(
            new TerrainRenderer(_context.Assets),
            new EntityRenderer(_context.Assets),
            new BuildingOverlayRenderer(_context.Assets),
            new BuildPreviewRenderer(_context.Assets),
            new MiniMapRenderer(_context.Assets),
            CreateUiRenderer(),
            CreateDeathOverlayRenderer(),
            CreateOrbedOverlayRenderer(),
            CreateResearchCompleteOverlayRenderer(),
            CreateChatOverlayRenderer());

        _tileMap = InGameWorldLoader.LoadTileMap();
        _simulation.TileMap = _tileMap;
        _simulation.SuppressLocalOrbEffects = true;
        _simulation.SuppressLocalItemDrops = true;
        _simulation.SuppressLocalBombDetonation = true;
        _simulation.SuppressLocalFactoryProduction = true;
        _simulation.ReturnInventoryPlaceablesOnDeath = false;
        _simulation.SuppressLocalPlayerRespawn = true;
        _simulation.DeferRemotePlayerRespawn = true;
        _simulation.NetworkPlayersUseLocalHealthDeath = false;
        _simulation.NetworkPlayersUseLocalBulletDamage = false;
        _simulation.ReportLocalShot = shot => _client.SendShoot(shot);

        // Online multiplayer: CC-only shared world (legacy MP). Buildings arrive via join snapshot.
        _simulation.LoadMultiplayerWorld();

        _remotePlayers = new RemotePlayerSync(_simulation.World);
        _remotePlayers.SetDisplayName(_client.PlayerId, _context.PlayerName);
        _client.PollAvailable();
        ApplyPendingNetworkEvents();
        _client.PollAvailable();
        ApplyPendingNetworkEvents();

        NumericsVector2 spawn;
        var localCityId = _client.SpawnState?.City ?? 0;
        if (_client.SpawnState is { } stateGame)
        {
            spawn = new NumericsVector2(stateGame.X, stateGame.Y);
        }
        else if (_simulation.TryGetCityRespawnPosition(localCityId, out var fallbackSpawn, out _))
        {
            spawn = fallbackSpawn;
        }
        else
        {
            // Last resort when map CC scan failed — prefer world origin over layout centroid.
            spawn = NumericsVector2.Zero;
        }

        spawn = _simulation.FindOpenTankSpawnNear(spawn);

        if (CityCatalog.IsValidCityId(localCityId))
        {
            _context.SelectedCity = CityCatalog.GetName(localCityId);
        }

        _simulation.EnsureCityBuild(localCityId);
        _simulation.CreatePlayerEntity(
            spawn,
            isMayor: false,
            isAdmin: IsLocalAdminVisual(),
            cityId: localCityId);
        _remotePlayers.ObserverCityId = localCityId;
        ApplyLocalMayorVisual(_client.IsMayor);
        _cameraFocus = new Vector2(spawn.X + GameConstants.TileSize / 2f, spawn.Y + GameConstants.TileSize / 2f);

        // Practice bots are offline-only; online enemies are other players.

        _gameplayAudio = new GameplayAudioController(_context.Audio);
        _camera.SetViewport(UiLayout.LogicalWidth, UiLayout.LogicalHeight);
        _camera.WorldViewportWidth = UiLayout.WorldViewportWidth;
        _camera.WorldViewportHeight = UiLayout.WorldViewportHeight;
        _camera.Zoom = DisplaySettings.DefaultGameplayZoom;
        _cameraPanOffset = Vector2.Zero;
        _camera.CenterOn(_cameraFocus);
        _chatLog.Append("Press Enter to chat.", ChatColorResolver.System);
        _loaded = true;
    }

    public SceneTransition Update(GameTime gameTime, int screenWidth, int screenHeight)
    {
        if (!_loaded)
        {
            return SceneTransition.None;
        }

        var menuInput = default(MenuInputState);
        if (_pendingApplicantId.HasValue && !_showSettingsMenu)
        {
            menuInput = _menuInput.Poll();
            if (menuInput.ConfirmPressed)
            {
                _client.AcceptApplicant();
                _pendingApplicantId = null;
                _pendingApplicantName = null;
                InGameChatService.AppendSystem(_chatLog, "Applicant accepted.");
            }
            else if (menuInput.CancelPressed)
            {
                _client.DeclineApplicant();
                _pendingApplicantId = null;
                _pendingApplicantName = null;
                InGameChatService.AppendSystem(_chatLog, "Applicant declined.");
            }
        }

        _client.Poll();
        ApplyPendingNetworkEvents();
        if (_returnToMeeting)
        {
            _context.NetworkClient = _client;
            return SceneTransition.Meeting;
        }

        var keyboard = Keyboard.GetState();

        // Legacy Personnel "not recruiting" checkbox (cmIsHiring).
        if (_client.IsMayor
            && !_chatInput.IsActive
            && !_showSettingsMenu
            && keyboard.IsKeyDown(Keys.N)
            && !_previousKeyboard.IsKeyDown(Keys.N))
        {
            _denyApplicants = !_denyApplicants;
            _client.SetDenyApplicants(_denyApplicants);
            InGameChatService.AppendSystem(
                _chatLog,
                _denyApplicants
                    ? "Not hiring: ON — new applicants are auto-rejected."
                    : "Not hiring: OFF — applicants can interview again.");
        }

        _previousKeyboard = keyboard;

        var worldWidth = UiLayout.WorldViewportWidth;
        _camera.SetViewport(UiLayout.LogicalWidth, UiLayout.LogicalHeight);
        _camera.WorldViewportWidth = worldWidth;
        _camera.WorldViewportHeight = UiLayout.WorldViewportHeight;

        var playerCenter = _cameraFocus;
        if (_simulation.TryGetPlayerPosition(out var playerPosition))
        {
            playerCenter = new Vector2(
                playerPosition.X + GameConstants.TileSize / 2f,
                playerPosition.Y + GameConstants.TileSize / 2f);
        }

        // Settings must win over chat so Esc / Enter are not stolen by chat or leave-to-menu.
        var deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var networkGameplay = default(GameplayInputState);
        var hasNetworkGameplay = false;
        if (_showSettingsMenu)
        {
            var settingsFrame = _input.Poll(
                _camera,
                playerCenter,
                worldWidth,
                _context.Presentation,
                deltaSeconds);
            CaptureVirtualCursor(settingsFrame.Ui);
            if (HandleSettingsInput(settingsFrame.Ui, out var leaveToMenu, out var abandonCity))
            {
                if (abandonCity)
                {
                    AbandonCityToLobby();
                    return SceneTransition.Meeting;
                }

                if (leaveToMenu)
                {
                    _context.Audio.StopEngine();
                    return SceneTransition.MainMenu;
                }
            }
        }
        else
        {
            var chatUpdate = _chatInput.Update(keyboard);
            if (chatUpdate.Submitted)
            {
                SendChatMessage(chatUpdate.Message);
            }

            if (!_chatInput.IsActive)
            {
                var frameInput = _input.Poll(
                    _camera,
                    playerCenter,
                    worldWidth,
                    _context.Presentation,
                    deltaSeconds);
                CaptureVirtualCursor(frameInput.Ui);
                if (HandleSettingsInput(frameInput.Ui, out var leaveToMenu, out var abandonCity))
                {
                    if (abandonCity)
                    {
                        AbandonCityToLobby();
                        return SceneTransition.Meeting;
                    }

                    if (leaveToMenu)
                    {
                        _context.Audio.StopEngine();
                        return SceneTransition.MainMenu;
                    }
                }
                else
                {
                    SendNetworkItemDrops(frameInput.Gameplay);
                    SendNetworkItemPickup(frameInput.Gameplay);
                    SendNetworkMedKit(frameInput.Gameplay);
                    SendNetworkCloak(frameInput.Gameplay);
                    SendNetworkDeathReport();
                    InputCommandWriter.Apply(_simulation.World, frameInput.Gameplay);
                    HandleBuildInput(frameInput.Ui, playerCenter, worldWidth);
                    UpdateBuildPreview(frameInput.Ui, playerCenter, worldWidth);
                    ApplyUiInput(frameInput.Ui, gameTime);
                    networkGameplay = frameInput.Gameplay;
                    hasNetworkGameplay = true;
                }
            }
        }

        if (_simulation.TryGetPlayerPosition(out playerPosition))
        {
            _cameraFocus = new Vector2(
                playerPosition.X + GameConstants.TileSize / 2f,
                playerPosition.Y + GameConstants.TileSize / 2f);
            playerCenter = _cameraFocus;
        }

        if (!_showSettingsMenu)
        {
            if (_simulation.TryGetPlayerPosition(out playerPosition))
            {
                SendNetworkUpdate(
                    new NumericsVector2(playerPosition.X, playerPosition.Y),
                    hasNetworkGameplay ? networkGameplay : default,
                    (float)gameTime.ElapsedGameTime.TotalSeconds);
            }

            _simulation.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        _animationTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _camera.CenterOn(_cameraFocus + _cameraPanOffset);

        _gameplayAudio.PlaySimulationEvents(
            _simulation.ConsumeSoundEvents(),
            playerCenter);

        _context.Audio.SetEngineRunning(
            !_chatInput.IsActive
            && !_showSettingsMenu
            && _simulation.TryGetPlayerInputMove(out var move)
            && move != 0);

        return SceneTransition.None;
    }

    public void DrawWorld(SpriteBatch spriteBatch) =>
        _renderPipeline.DrawWorld(spriteBatch, CreateRenderContext());

    public void DrawScreen(SpriteBatch spriteBatch) =>
        _renderPipeline.DrawScreen(spriteBatch, CreateRenderContext());

    public void Dispose()
    {
        _context.Audio.StopEngine();
        _remotePlayers?.Clear();
        if (!_keepNetworkClientOnDispose)
        {
            _client.Dispose();
            _context.NetworkClient = null;
        }

        _simulation.Dispose();
        _loaded = false;
    }

    private void ApplyPendingNetworkEvents()
    {
        foreach (var networkEvent in _client.DrainEvents())
        {
            switch (networkEvent.Kind)
            {
                case GameClientEventKind.JoinData when networkEvent.JoinData.PlayerId != _client.PlayerId:
                    {
                        var joinSpawn = _simulation.TryGetCityRespawnPosition(
                                networkEvent.JoinData.City,
                                out var citySpawn,
                                out _)
                            ? citySpawn
                            : new NumericsVector2(0f, 0f);
                        _remotePlayers.HandleJoin(networkEvent.JoinData, joinSpawn);
                    }
                    break;
                case GameClientEventKind.PlayerUpdate when networkEvent.Update.PlayerId != _client.PlayerId:
                    _remotePlayers.ApplyUpdate(networkEvent.Update);
                    break;
                case GameClientEventKind.PlayerData:
                    _remotePlayers.SetDisplayName(
                        networkEvent.PlayerData.Index,
                        networkEvent.PlayerData.Name);
                    break;
                case GameClientEventKind.ChatCommand when networkEvent.ChatCommandCode == 69:
                    // Legacy 'E' — player left the battlefield.
                    {
                        var leftName = _remotePlayers.GetDisplayName(networkEvent.PlayerId)
                            ?? $"Player{networkEvent.PlayerId}";
                        _remotePlayers.Remove(networkEvent.PlayerId);
                        InGameChatService.AppendSystem(_chatLog, $"{leftName} has left");
                    }

                    break;
                case GameClientEventKind.ClearPlayer:
                    _remotePlayers.Remove(networkEvent.PlayerId);
                    break;
                case GameClientEventKind.AddItem:
                    _simulation.ApplyNetworkAddItem(networkEvent.AddItem);
                    break;
                case GameClientEventKind.Shoot:
                    _simulation.ApplyNetworkShot(networkEvent.Shot);
                    break;
                case GameClientEventKind.RemoveItem:
                    _simulation.ApplyNetworkRemoveItem(networkEvent.RemoveItem);
                    break;
                case GameClientEventKind.PickedUp:
                    _simulation.ApplyNetworkPickedUp(networkEvent.PickedUp);
                    _context.Audio.Play(SoundId.Click);
                    break;
                case GameClientEventKind.NewBuilding:
                    _simulation.ApplyNetworkNewBuilding(networkEvent.Building);
                    break;
                case GameClientEventKind.RemoveBuilding:
                    _simulation.ApplyNetworkRemoveBuilding(networkEvent.Building);
                    break;
                case GameClientEventKind.UnderAttack:
                    _simulation.ApplyNetworkUnderAttack();
                    break;
                case GameClientEventKind.ItemLife:
                    _simulation.ApplyNetworkItemLife(networkEvent.ItemLife);
                    break;
                case GameClientEventKind.Promotion:
                    InGameChatService.AppendPromotion(
                        _chatLog,
                        _remotePlayers,
                        _client,
                        _context.PlayerName,
                        networkEvent.Promotion);
                    break;
                case GameClientEventKind.CanBuild:
                    _simulation.ApplyNetworkCanBuild(networkEvent.CanBuild);
                    break;
                case GameClientEventKind.UpdatePop:
                    _simulation.ApplyNetworkUpdatePop(networkEvent.UpdatePop);
                    break;
                case GameClientEventKind.ItemCount:
                    _simulation.ApplyNetworkItemCount(networkEvent.ItemCount);
                    break;
                case GameClientEventKind.PointsUpdate:
                    _remotePlayers.ApplyPointsUpdate(networkEvent.PointsUpdate);
                    break;
                case GameClientEventKind.MedKit:
                    _simulation.TryConsumeLocalPlayerItem(ItemType.MedKit);
                    _context.Audio.Play(SoundId.Click);
                    break;
                case GameClientEventKind.Cloak:
                    _simulation.ApplyNetworkCloak(networkEvent.Cloak.PlayerId, _client.PlayerId);
                    break;
                case GameClientEventKind.Explosion:
                    _simulation.ApplyNetworkExplosion(networkEvent.Explosion);
                    break;
                case GameClientEventKind.Warp:
                    ApplyLocalWarp(networkEvent.Warp);
                    break;
                case GameClientEventKind.StateGame:
                    // Admin /city rejoin uses smStateGame while already in-game.
                    _simulation.ApplyAdminCityRejoin(
                        networkEvent.StateGame,
                        isMayor: _client.IsMayor,
                        isAdmin: IsLocalAdminVisual());
                    _remotePlayers.ObserverCityId = networkEvent.StateGame.City;
                    ApplyLocalWarpCamera(networkEvent.StateGame);
                    if (CityCatalog.IsValidCityId(networkEvent.StateGame.City))
                    {
                        _context.SelectedCity = CityCatalog.GetName(networkEvent.StateGame.City);
                    }

                    break;
                case GameClientEventKind.Respawn:
                    _remotePlayers.ApplyRespawn(networkEvent.Respawn.PlayerId);
                    _simulation.ApplyNetworkRespawn(networkEvent.Respawn.PlayerId);
                    break;
                case GameClientEventKind.Death:
                    _simulation.ApplyPlayerDeath(networkEvent.Death, _client.PlayerId);
                    if (networkEvent.Death.PlayerId == _client.PlayerId)
                    {
                        _cameraPanOffset = Vector2.Zero;
                        var localCityId = GetLocalCityId();
                        if (_simulation.TryGetCityBuild(localCityId, out var homeCity)
                            && CommandCenterLookup.TryGetHomeReferenceWorldPosition(
                                _simulation.World,
                                homeCity.CommandCenterGridX,
                                homeCity.CommandCenterGridY,
                                out var homeReference))
                        {
                            _cameraFocus = new Vector2(homeReference.X, homeReference.Y);
                        }
                        else if (_simulation.TryGetCityRespawnPosition(localCityId, out var spawn, out _))
                        {
                            _cameraFocus = new Vector2(
                                spawn.X + GameConstants.TileSize / 2f,
                                spawn.Y + GameConstants.TileSize / 2f);
                        }
                    }

                    InGameChatService.AppendDeath(
                        _chatLog,
                        _remotePlayers,
                        _client,
                        _context.PlayerName,
                        GetLocalCityId(),
                        networkEvent.Death);
                    break;
                case GameClientEventKind.Hp:
                    _simulation.ApplyNetworkHp(networkEvent.Hp, _client.PlayerId);
                    break;
                case GameClientEventKind.ChatMessage:
                    InGameChatService.AppendIncoming(
                        _chatLog,
                        _remotePlayers,
                        _client,
                        _context.PlayerName,
                        GetLocalCityId(),
                        networkEvent.ChatMessage,
                        IsPlayerDeadForChat);
                    break;
                case GameClientEventKind.GlobalChat:
                    InGameChatService.AppendGlobal(
                        _chatLog,
                        _remotePlayers,
                        _client,
                        _context.PlayerName,
                        networkEvent.ChatMessage);
                    break;
                case GameClientEventKind.WhisperChat:
                    InGameChatService.AppendWhisper(
                        _chatLog,
                        _remotePlayers,
                        _client,
                        _context.PlayerName,
                        networkEvent.ChatMessage);
                    break;
                case GameClientEventKind.MayorUpdate:
                    if (networkEvent.MayorUpdate.PlayerId == _client.PlayerId)
                    {
                        ApplyLocalMayorVisual(networkEvent.MayorUpdate.IsMayor);
                    }
                    else
                    {
                        _remotePlayers.SetMayor(
                            networkEvent.MayorUpdate.PlayerId,
                            networkEvent.MayorUpdate.IsMayor);
                    }

                    break;
                case GameClientEventKind.MayorHire:
                    _pendingApplicantId = networkEvent.MayorHire.ApplicantPlayerId;
                    _pendingApplicantName = _remotePlayers.GetDisplayName(networkEvent.MayorHire.ApplicantPlayerId)
                        ?? $"Player{networkEvent.MayorHire.ApplicantPlayerId}";
                    if (_denyApplicants)
                    {
                        // Legacy auto-decline when "not recruiting" is checked.
                        _client.DeclineApplicant();
                        _pendingApplicantId = null;
                        _pendingApplicantName = null;
                        InGameChatService.AppendSystem(
                            _chatLog,
                            "Auto-rejected applicant (Not hiring is ON).");
                        break;
                    }

                    InGameChatService.AppendSystem(
                        _chatLog,
                        $"{_pendingApplicantName} is applying to join your city.");
                    InGameChatService.AppendSystem(
                        _chatLog,
                        "Personnel: Enter=Welcome  Esc=Reject  N=Toggle not hiring");
                    break;
                case GameClientEventKind.InterviewCancel:
                    if (_pendingApplicantId.HasValue)
                    {
                        var canceled = _pendingApplicantName ?? "Applicant";
                        InGameChatService.AppendSystem(
                            _chatLog,
                            $"{canceled} canceled their application.");
                    }

                    _pendingApplicantId = null;
                    _pendingApplicantName = null;
                    break;
                case GameClientEventKind.Comms:
                    AppendInterviewComms(networkEvent.ChatMessage);
                    break;
                case GameClientEventKind.Fired:
                    AbandonCityToLobby();
                    break;
                case GameClientEventKind.Kicked:
                    {
                        var verb = networkEvent.KickCommand == AdminCommands.Ban ? "banned" : "kicked";
                        InGameChatService.AppendSystem(_chatLog, $"You have been {verb} by an admin.");
                        AbandonCityToLobby();
                    }

                    break;
                case GameClientEventKind.AdminAction:
                    AppendAdminActionChat(networkEvent.AdminAction);
                    break;
                case GameClientEventKind.BanEntry:
                    AppendBanEntryChat(networkEvent.BanEntry);
                    break;
                case GameClientEventKind.AppendNews:
                    AppendNewsChat(networkEvent.NewsText);
                    break;
                case GameClientEventKind.Orbed:
                    _simulation.ApplyNetworkOrb(
                        networkEvent.Orbed.VictimCity,
                        networkEvent.Orbed.OrberCity);
                    if (GetLocalCityId() == networkEvent.Orbed.VictimCity)
                    {
                        AbandonCityToLobby();
                    }

                    break;
                case GameClientEventKind.Disconnected:
                    return;
            }
        }
    }

    private void SendNetworkUpdate(NumericsVector2 playerPosition, GameplayInputState gameplay, float deltaSeconds)
    {
        _updateSendCooldown -= deltaSeconds;
        if (_updateSendCooldown > 0f)
        {
            return;
        }

        _updateSendCooldown = 0.15f;

        var facing = 0;
        var facingQuery = new Arch.Core.QueryDescription().WithAll<InputControlled, TankFacing>();
        _simulation.World.Query(
            in facingQuery,
            (ref TankFacing tankFacing) => facing = tankFacing.Direction);

        _client.SendUpdate(new ClientUpdatePacket(
            (ushort)Math.Clamp((int)playerPosition.X, 0, ushort.MaxValue),
            (ushort)Math.Clamp((int)playerPosition.Y, 0, ushort.MaxValue),
            gameplay.Turn,
            gameplay.Move,
            (byte)facing));
    }

    private void SendNetworkItemPickup(GameplayInputState gameplay)
    {
        if (!gameplay.PickUpItemPressed || _pickUpItemPressedLastFrame)
        {
            _pickUpItemPressedLastFrame = gameplay.PickUpItemPressed;
            return;
        }

        _pickUpItemPressedLastFrame = gameplay.PickUpItemPressed;

        if (!_simulation.TryFindPickupItemAtLocalPlayer(out var itemId, out var itemType, out var active))
        {
            return;
        }

        _client.SendItemPickup(new ClientItemPickupPacket(itemId, active, itemType));
    }

    private void SendNetworkMedKit(GameplayInputState gameplay)
    {
        if (!gameplay.UseMedKitPressed || _useMedKitPressedLastFrame)
        {
            _useMedKitPressedLastFrame = gameplay.UseMedKitPressed;
            return;
        }

        _useMedKitPressedLastFrame = gameplay.UseMedKitPressed;

        if (!_simulation.TryGetPlayerInventory(out var inventory)
            || inventory.GetCount(ItemType.MedKit) <= 0)
        {
            return;
        }

        _client.SendMedKit();
    }

    private void SendNetworkCloak(GameplayInputState gameplay)
    {
        if (!gameplay.UseCloakPressed || _useCloakPressedLastFrame)
        {
            _useCloakPressedLastFrame = gameplay.UseCloakPressed;
            return;
        }

        _useCloakPressedLastFrame = gameplay.UseCloakPressed;

        if (!_simulation.CanLocalPlayerCloak())
        {
            return;
        }

        _client.SendCloak();
    }

    private void SendNetworkDeathReport()
    {
        if (!_simulation.TryGetPlayerLifeState(out var life))
        {
            return;
        }

        if (life.IsDead)
        {
            if (!_deathReported)
            {
                _client.SendDeath(new ClientDeathPacket(killerCity: life.KillerCityId));
                _deathReported = true;
            }

            return;
        }

        _deathReported = false;
    }

    private void HandleBuildInput(UiInputState ui, Vector2 playerCenter, int worldWidth)
    {
        if (!_client.IsMayor && !IsLocalAdmin())
        {
            _showBuildMenu = false;
            _buildModeSlot = 0;
            return;
        }

        if (ui.MouseRightClicked && ui.PointerOverWorld)
        {
            _showBuildMenu = true;
            _buildModeSlot = 0;
            _buildMenuAnchor = ui.MouseLogicalPosition;
            return;
        }

        if (!ui.MouseLeftClicked || !_simulation.TryGetCityBuild(GetLocalCityId(), out var build))
        {
            return;
        }

        if (_showBuildMenu
            && BuildMenuLayout.TryHitTest(
                (int)_buildMenuAnchor.X,
                (int)_buildMenuAnchor.Y,
                UiLayout.LogicalWidth,
                UiLayout.LogicalHeight,
                build,
                (int)ui.MouseLogicalPosition.X,
                (int)ui.MouseLogicalPosition.Y,
                out var buildSlot))
        {
            _showBuildMenu = false;
            _buildModeSlot = buildSlot;
            return;
        }

        if (_showBuildMenu
            && BuildMenuLayout.ContainsPoint(
                (int)_buildMenuAnchor.X,
                (int)_buildMenuAnchor.Y,
                UiLayout.LogicalWidth,
                UiLayout.LogicalHeight,
                build,
                (int)ui.MouseLogicalPosition.X,
                (int)ui.MouseLogicalPosition.Y))
        {
            return;
        }

        if (_showBuildMenu)
        {
            _showBuildMenu = false;
            return;
        }

        if (_buildModeSlot == 0 || !ui.PointerOverWorld)
        {
            return;
        }

        var mouseScreen = new Vector2(
            Math.Clamp(ui.MouseLogicalPosition.X, 0, worldWidth - 1),
            Math.Clamp(ui.MouseLogicalPosition.Y, 0, UiLayout.LogicalHeight - 1));
        var worldPosition = _camera.ScreenToWorld(mouseScreen);
        var (gridX, gridY) = BuildingPlacementValidator.WorldToGridAnchor(
            new NumericsVector2(worldPosition.X, worldPosition.Y));

        if (_buildModeSlot == -1)
        {
            if (_simulation.TryGetBuildingNetworkIdAt(gridX, gridY, out var buildingId))
            {
                _client.SendDemolish(new ClientDemolishPacket(buildingId));
                _buildModeSlot = 0;
            }

            return;
        }

        _client.SendBuild(new ClientBuildPacket(
            (ushort)gridX,
            (ushort)gridY,
            (byte)_buildModeSlot,
            isAutoBuild: false));
        _buildModeSlot = 0;
    }

    private void UpdateBuildPreview(UiInputState ui, Vector2 playerCenter, int worldWidth)
    {
        _showBuildPreview = false;

        if (_buildModeSlot == 0 || !ui.PointerOverWorld || !_simulation.TryGetCityBuild(GetLocalCityId(), out var build))
        {
            return;
        }

        var mouseScreen = new Vector2(
            Math.Clamp(ui.MouseLogicalPosition.X, 0, worldWidth - 1),
            Math.Clamp(ui.MouseLogicalPosition.Y, 0, UiLayout.LogicalHeight - 1));
        var worldPosition = _camera.ScreenToWorld(mouseScreen);
        var (gridX, gridY) = BuildingPlacementValidator.WorldToGridAnchor(
            new NumericsVector2(worldPosition.X, worldPosition.Y));

        _buildPreviewGridX = gridX;
        _buildPreviewGridY = gridY;
        _buildPreviewIsValid = BuildPreviewHelper.Evaluate(
            _simulation.World,
            build,
            _tileMap,
            _buildModeSlot,
            gridX,
            gridY,
            new NumericsVector2(playerCenter.X, playerCenter.Y),
            out _buildPreviewTypeCode,
            out _buildPreviewIsDemolish);
        _showBuildPreview = true;
    }

    private void SendNetworkItemDrops(GameplayInputState gameplay)
    {
        if (!_simulation.TryGetPlayerInventory(out var inventory))
        {
            return;
        }

        _simulation.TryGetCityBuild(GetLocalCityId(), out var cityBuild);

        if (gameplay.DropSelectedItemPressed && inventory.GetCount(inventory.SelectedItemType) > 0)
        {
            TrySendNetworkItemDrop(inventory.SelectedItemType, active: true, cityBuild);
        }

        if (gameplay.DropBombPressed && inventory.GetCount(ItemType.Bomb) > 0)
        {
            TrySendNetworkItemDrop(ItemType.Bomb, active: true, cityBuild);
        }

        if (gameplay.DropOrbPressed && inventory.GetCount(ItemType.Orb) > 0)
        {
            TrySendNetworkItemDrop(ItemType.Orb, active: false, cityBuild);
        }
    }

    private void TrySendNetworkItemDrop(ItemType type, bool active, CityBuildState? cityBuild)
    {
        if (!_simulation.TryGetPlayerPosition(out var position))
        {
            return;
        }

        var tankTopLeft = new NumericsVector2(position.X, position.Y);
        var player = GetLocalPlayerEntity();
        if (ItemDropFeedback.GetFailureMessage(_simulation.World, player, tankTopLeft, type, cityBuild)
            is { } failureMessage)
        {
            InGameChatService.AppendSystem(_chatLog, failureMessage);
            return;
        }

        if (!_simulation.TryPredictLocalItemDrop(type, active)
            || !_simulation.TryConsumeLocalPlayerItem(type))
        {
            return;
        }

        _client.SendItemDrop(type, active);
    }

    private RenderContext CreateRenderContext()
    {
        int? playerHealth = null;
        int? playerMaxHealth = null;
        float? playerRespawnSeconds = null;
        if (_simulation.TryGetPlayerHealth(out var health))
        {
            playerHealth = health.Current;
            playerMaxHealth = health.Max;
        }

        if (_simulation.TryGetPlayerLifeState(out var life) && life.IsDead)
        {
            playerRespawnSeconds = life.RespawnTimerSeconds;
            playerHealth = 0;
        }

        PlayerInventory? inventory = null;
        var isUnderAttack = false;
        var underAttackFlashVisible = false;
        var cloakRecharge = 0f;
        var flareRecharge = 0f;
        CityBuildState? cityBuild = null;
        if (_simulation.TryGetCityBuild(GetLocalCityId(), out var buildState))
        {
            cityBuild = buildState;
        }

        var playerQuery = new Arch.Core.QueryDescription().WithAll<InputControlled, PlayerInventory, CityAlertState, WeaponState>();
        _simulation.World.Query(
            in playerQuery,
            (ref PlayerInventory value, ref CityAlertState alert, ref WeaponState weapons) =>
            {
                inventory = value;
                isUnderAttack = alert.IsUnderAttack;
                underAttackFlashVisible = alert.FlashArrowVisible;
                cloakRecharge = weapons.CloakRechargeSeconds;
                flareRecharge = weapons.FlareRechargeSeconds;
            });

        var showOrbedOverlay = false;
        var orbedOverlayIsVictim = false;
        string? orbedOverlayMessage = null;
        var orbedQuery = new Arch.Core.QueryDescription().WithAll<InputControlled, CityOrbedState>();
        _simulation.World.Query(
            in orbedQuery,
            (ref CityOrbedState orbed) =>
            {
                if (!orbed.ShowOverlay)
                {
                    return;
                }

                showOrbedOverlay = true;
                orbedOverlayIsVictim = orbed.IsVictim;
                orbedOverlayMessage = orbed.Message;
            });

        var showResearchCompleteOverlay = false;
        string? researchCompleteOverlayMessage = null;
        var researchQuery = new Arch.Core.QueryDescription().WithAll<InputControlled, CityResearchCompleteState>();
        _simulation.World.Query(
            in researchQuery,
            (ref CityResearchCompleteState complete) =>
            {
                if (!complete.ShowOverlay)
                {
                    return;
                }

                showResearchCompleteOverlay = true;
                researchCompleteOverlayMessage = complete.Message;
            });

        var observerCityId = _simulation.TryGetPlayerCityId(out var playerCityId)
            ? playerCityId
            : _remotePlayers?.ObserverCityId ?? 0;
        var homeCcGridX = 0;
        var homeCcGridY = 0;
        var cityCenterWorldPosition = _cameraFocus;
        Vector2? nearestOrbableCity = null;
        if (_simulation.TryGetCityBuild(observerCityId, out var homeCityForCompass))
        {
            homeCcGridX = homeCityForCompass.CommandCenterGridX;
            homeCcGridY = homeCityForCompass.CommandCenterGridY;
            if (CommandCenterLookup.TryGetHomeReferenceWorldPosition(
                    _simulation.World,
                    homeCityForCompass.CommandCenterGridX,
                    homeCityForCompass.CommandCenterGridY,
                    out var homeReference))
            {
                cityCenterWorldPosition = new Vector2(homeReference.X, homeReference.Y);
            }

            if (CommandCenterLookup.TryFindNearestOtherWorldPosition(
                    _simulation.World,
                    homeCityForCompass.CommandCenterGridX,
                    homeCityForCompass.CommandCenterGridY,
                    new NumericsVector2(_cameraFocus.X, _cameraFocus.Y),
                    out var orbTarget,
                    cityId => _simulation.TryGetCityBuild(cityId, out var orbBuild) && orbBuild.IsOrbable))
            {
                nearestOrbableCity = new Vector2(orbTarget.X, orbTarget.Y);
            }
        }
        else if (CommandCenterLookup.TryGetHomeReferenceWorldPosition(_simulation.World, out var anyHomeReference))
        {
            cityCenterWorldPosition = new Vector2(anyHomeReference.X, anyHomeReference.Y);
        }

        return new RenderContext
        {
            Camera = _camera,
            TileMap = _tileMap,
            World = _simulation.World,
            FocusWorldPosition = _cameraFocus,
            CityCenterWorldPosition = cityCenterWorldPosition,
            NearestOrbableCityWorldPosition = nearestOrbableCity,
            HomeCommandCenterGridX = homeCcGridX,
            HomeCommandCenterGridY = homeCcGridY,
            ScreenWidth = _camera.ViewportWidth,
            ScreenHeight = _camera.ViewportHeight,
            ShowMiniMap = _showMiniMap,
            ShowStatusPanel = _showStatusPanel,
            LoadedCityName = _context.SelectedCity,
            BuildingCount = _simulation.CountBuildingsInWorld(),
            PlayerDisplayName = _context.PlayerName,
            PlayerHealth = playerHealth,
            PlayerMaxHealth = playerMaxHealth,
            PlayerRespawnSeconds = playerRespawnSeconds,
            PlayerInventory = inventory,
            CloakRechargeSeconds = cloakRecharge,
            FlareRechargeSeconds = flareRecharge,
            CloakRechargeUnlocked = CityEquipmentRules.HasRechargeableCloak(cityBuild),
            FlareRechargeUnlocked = CityEquipmentRules.HasRechargeableFlare(cityBuild),
            IsUnderAttack = isUnderAttack,
            UnderAttackFlashVisible = underAttackFlashVisible,
            CityBuild = cityBuild,
            AnimationTime = _animationTime,
            ShowBuildMenu = _showBuildMenu,
            BuildMenuAnchor = _buildMenuAnchor,
            BuildModeSlot = _buildModeSlot,
            ShowBuildPreview = _showBuildPreview,
            BuildPreviewGridX = _buildPreviewGridX,
            BuildPreviewGridY = _buildPreviewGridY,
            BuildPreviewTypeCode = _buildPreviewTypeCode,
            BuildPreviewIsValid = _buildPreviewIsValid,
            BuildPreviewIsDemolish = _buildPreviewIsDemolish,
            ShowOrbedOverlay = showOrbedOverlay,
            OrbedOverlayIsVictim = orbedOverlayIsVictim,
            OrbedOverlayMessage = orbedOverlayMessage,
            ShowResearchCompleteOverlay = showResearchCompleteOverlay,
            ResearchCompleteOverlayMessage = researchCompleteOverlayMessage,
            ChatLines = _chatLog.Lines,
            IsChatting = _chatInput.IsActive,
            ChatDraft = _chatInput.Draft,
            ShowHirePanel = _pendingApplicantId.HasValue && _client.IsMayor,
            HireApplicantName = _pendingApplicantName,
            DenyApplicants = _denyApplicants,
            ObserverCityId = observerCityId,
            ShowSettingsMenu = _showSettingsMenu,
            SettingsSelectedIndex = _settingsSelectedIndex,
            ShowVirtualCursor = _showVirtualCursor,
            VirtualCursorLogical = _virtualCursorLogical,
        };
    }

    private bool HandleSettingsInput(UiInputState ui, out bool leaveToMenu, out bool abandonCity)
    {
        leaveToMenu = false;
        abandonCity = false;
        var hamburgerClicked = ui.MouseLeftClicked
            && ModernHudLayout.HamburgerBounds.Contains(
                (int)ui.MouseLogicalPosition.X,
                (int)ui.MouseLogicalPosition.Y);

        if (ui.ToggleSettingsPressed || hamburgerClicked)
        {
            _showSettingsMenu = !_showSettingsMenu;
            if (_showSettingsMenu)
            {
                _settingsSelectedIndex = 0;
                _menuInput.Reset();
            }

            return true;
        }

        if (!_showSettingsMenu)
        {
            return false;
        }

        var menu = _menuInput.Poll();
        if (menu.MoveUpPressed)
        {
            _settingsSelectedIndex =
                (_settingsSelectedIndex - 1 + UiRenderer.SettingsMenuItems.Length)
                % UiRenderer.SettingsMenuItems.Length;
        }

        if (menu.MoveDownPressed)
        {
            _settingsSelectedIndex =
                (_settingsSelectedIndex + 1) % UiRenderer.SettingsMenuItems.Length;
        }

        if (menu.ConfirmPressed)
        {
            switch (_settingsSelectedIndex)
            {
                case 0:
                    _showSettingsMenu = false;
                    break;
                case 1:
                    _showStatusPanel = !_showStatusPanel;
                    break;
                case 2:
                    _showMiniMap = !_showMiniMap;
                    break;
                case 3:
                    abandonCity = true;
                    break;
                case 4:
                    leaveToMenu = true;
                    break;
            }
        }

        if (menu.CancelPressed)
        {
            _showSettingsMenu = false;
        }

        return true;
    }

    private void CaptureVirtualCursor(UiInputState ui)
    {
        _showVirtualCursor = ui.ShowVirtualCursor;
        _virtualCursorLogical = ui.MouseLogicalPosition;
        if (ui.GamepadJustConnected)
        {
            InGameChatService.AppendSystem(
                _chatLog,
                "Controller connected. RS click toggles pointer mode for building.");
        }

        if (ui.GamepadJustDisconnected)
        {
            InGameChatService.AppendSystem(_chatLog, "Controller disconnected.");
        }
    }

    private void AbandonCityToLobby()
    {
        _keepNetworkClientOnDispose = true;
        _context.NetworkClient = _client;
        _client.EnterMeetingRoom();
        _returnToMeeting = true;
        _showSettingsMenu = false;
        _context.Audio.StopEngine();
    }

    private bool IsLocalAdmin() => _client.IsAdmin;

    private bool IsLocalAdminVisual() =>
        _client.IsAdmin || TankSpriteSelector.IsAdminAccount(_context.PlayerName);

    private void ApplyLocalMayorVisual(bool isMayor)
    {
        var isAdmin = IsLocalAdminVisual();
        var query = new QueryDescription().WithAll<InputControlled, SpriteRef, MayorStatus, CityAffiliation>();
        _simulation.World.Query(
            in query,
            (ref SpriteRef sprite, ref MayorStatus mayor, ref CityAffiliation city) =>
            {
                mayor.IsMayor = isMayor;
                sprite.SourceY = TankSpriteSelector.GetSourceY(city.CityId, city.CityId, isMayor, isAdmin)
                    * GameConstants.TileSize;
            });
    }

    private byte GetLocalCityId() => _client.SpawnState?.City ?? 0;

    private string GetLocalChatDisplayName() =>
        _remotePlayers.GetChatDisplayName(_client.PlayerId);

    private bool IsPlayerDeadForChat(byte playerId)
    {
        if (playerId == _client.PlayerId)
        {
            return _simulation.TryGetPlayerLifeState(out var life) && life.IsDead;
        }

        return _simulation.IsNetworkPlayerDead(playerId);
    }

    private void AppendInterviewComms(in ServerChatMessagePacket comms)
    {
        var senderName = comms.SenderId == _client.PlayerId
            ? _context.PlayerName
            : _remotePlayers.GetDisplayName(comms.SenderId) ?? $"Player{comms.SenderId}";
        _chatLog.Append($"{senderName}: {comms.Message}", ChatColorResolver.ForRemoteMessage(0, 1, senderIsDead: false));
    }

    private void SendChatMessage(string message)
    {
        var command = ChatCommandParser.Parse(message);
        switch (command.Kind)
        {
            case ChatCommandKind.Global:
                SendGlobalChat(command.Message);
                break;
            case ChatCommandKind.Whisper:
                SendWhisperChat(command.WhisperRecipient, command.Message);
                break;
            case ChatCommandKind.Heir:
                SetMayorHeir(command.Message);
                break;
            case ChatCommandKind.Kick:
                SendAdminModeration(command.Message, AdminCommands.Kick, "kick");
                break;
            case ChatCommandKind.Ban:
                SendAdminModeration(command.Message, AdminCommands.Ban, "ban");
                break;
            case ChatCommandKind.Warp:
                SendAdminModeration(command.Message, AdminCommands.Warp, "warp");
                break;
            case ChatCommandKind.Summon:
                SendAdminModeration(command.Message, AdminCommands.Summon, "summon");
                break;
            case ChatCommandKind.JoinCity:
                SendAdminJoinCity(command.Message);
                break;
            case ChatCommandKind.Spawn:
                SendAdminSpawn(command.Message);
                break;
            case ChatCommandKind.Shutdown:
                SendAdminShutdown();
                break;
            case ChatCommandKind.Bans:
                SendAdminRequestBans();
                break;
            case ChatCommandKind.Unban:
                SendAdminUnban(command.Message);
                break;
            case ChatCommandKind.News:
                SendAdminRequestNews();
                break;
            case ChatCommandKind.SetNews:
                SendAdminSetNews(command.Message);
                break;
            default:
                if (string.IsNullOrWhiteSpace(command.Message))
                {
                    return;
                }

                if (_pendingApplicantId.HasValue && _client.IsMayor)
                {
                    InGameChatService.AppendLocalOutgoing(
                        _chatLog,
                        GetLocalChatDisplayName(),
                        command.Message,
                        isDead: false);
                    _client.SendComms(command.Message);
                    break;
                }

                var isDead = IsPlayerDeadForChat(_client.PlayerId);
                InGameChatService.AppendLocalOutgoing(_chatLog, GetLocalChatDisplayName(), command.Message, isDead);
                _client.SendWalkie(command.Message);
                break;
        }
    }

    private void SetMayorHeir(string argument)
    {
        if (!_client.IsMayor)
        {
            InGameChatService.AppendSystem(_chatLog, "Only the mayor can set a successor.");
            return;
        }

        if (string.IsNullOrWhiteSpace(argument)
            || argument.Equals("clear", StringComparison.OrdinalIgnoreCase)
            || argument.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            _client.SendSuccessor(0);
            InGameChatService.AppendSystem(_chatLog, "Mayor successor cleared.");
            return;
        }

        if (!WhisperRecipientMatcher.TryMatch(
                argument,
                _client.PlayerId,
                _remotePlayers.EnumerateDisplayNames(),
                out var heirId,
                out var heirName))
        {
            InGameChatService.AppendSystem(_chatLog, $"Player not found: {argument}");
            return;
        }

        _client.SendSuccessor(heirId);
        InGameChatService.AppendSystem(_chatLog, $"Mayor successor set to {heirName}.");
    }

    private void SendAdminModeration(string namePrefix, byte command, string verb)
    {
        if (!IsLocalAdmin())
        {
            InGameChatService.AppendSystem(_chatLog, $"Only admins can {verb} players.");
            return;
        }

        if (string.IsNullOrWhiteSpace(namePrefix))
        {
            InGameChatService.AppendSystem(_chatLog, $"Usage: /{verb} Name");
            return;
        }

        if (!WhisperRecipientMatcher.TryMatch(
                namePrefix,
                _client.PlayerId,
                _remotePlayers.EnumerateDisplayNames(),
                out var targetId,
                out var targetName))
        {
            InGameChatService.AppendSystem(_chatLog, $"Player not found: {namePrefix}");
            return;
        }

        _client.SendAdmin(targetId, command);
        var confirm = command switch
        {
            AdminCommands.Warp => $"Warping to {targetName}...",
            AdminCommands.Summon => $"Summoning {targetName}...",
            _ => $"Sent {verb} for {targetName}.",
        };
        InGameChatService.AppendSystem(_chatLog, confirm);
    }

    private void SendAdminJoinCity(string argument)
    {
        if (!IsLocalAdmin())
        {
            InGameChatService.AppendSystem(_chatLog, "Only admins can join another city.");
            return;
        }

        if (string.IsNullOrWhiteSpace(argument))
        {
            InGameChatService.AppendSystem(_chatLog, "Usage: /city <id|name>");
            return;
        }

        byte cityId;
        if (byte.TryParse(argument, out var parsedId) && CityCatalog.IsValidCityId(parsedId))
        {
            cityId = parsedId;
        }
        else if (CityCatalog.TryGetId(argument, out var namedId) && CityCatalog.IsValidCityId(namedId))
        {
            cityId = (byte)namedId;
        }
        else
        {
            InGameChatService.AppendSystem(_chatLog, $"City not found: {argument}");
            return;
        }

        _client.SendAdmin(cityId, AdminCommands.JoinCity);
        InGameChatService.AppendSystem(_chatLog, $"Joining {CityCatalog.GetName(cityId)}...");
    }

    private void SendAdminSpawn(string argument)
    {
        if (!IsLocalAdmin())
        {
            InGameChatService.AppendSystem(_chatLog, "Only admins can spawn items.");
            return;
        }

        if (!ItemCatalog.TryParse(argument, out var itemType))
        {
            InGameChatService.AppendSystem(_chatLog, "Usage: /spawn <id|name>  (0–11, e.g. wall, medkit, 8)");
            return;
        }

        _client.SendAdmin((byte)itemType, AdminCommands.SpawnItem);
        InGameChatService.AppendSystem(_chatLog, $"Spawn requested: {ItemCatalog.GetName(itemType)}");
    }

    private void SendAdminShutdown()
    {
        if (!IsLocalAdmin())
        {
            InGameChatService.AppendSystem(_chatLog, "Only admins can shut down the server.");
            return;
        }

        _client.SendAdmin(0, AdminCommands.Shutdown);
        InGameChatService.AppendSystem(_chatLog, "Shutting down server...");
    }

    private void SendAdminRequestBans()
    {
        if (!IsLocalAdmin())
        {
            InGameChatService.AppendSystem(_chatLog, "Only admins can list bans.");
            return;
        }

        InGameChatService.AppendSystem(_chatLog, "Banned accounts:");
        _client.SendAdmin(0, AdminCommands.RequestBans);
    }

    private void SendAdminUnban(string argument)
    {
        if (!IsLocalAdmin())
        {
            InGameChatService.AppendSystem(_chatLog, "Only admins can unban.");
            return;
        }

        if (string.IsNullOrWhiteSpace(argument))
        {
            InGameChatService.AppendSystem(_chatLog, "Usage: /unban <username>");
            return;
        }

        _client.SendAdminUnban(argument);
        InGameChatService.AppendSystem(_chatLog, $"Unban requested: {argument.Trim()}");
    }

    private void SendAdminRequestNews()
    {
        if (!IsLocalAdmin())
        {
            InGameChatService.AppendSystem(_chatLog, "Only admins can request news.");
            return;
        }

        _client.SendAdmin(0, AdminCommands.RequestNews);
    }

    private void SendAdminSetNews(string news)
    {
        if (!IsLocalAdmin())
        {
            InGameChatService.AppendSystem(_chatLog, "Only admins can set news.");
            return;
        }

        _client.SendChangeNews(news);
        InGameChatService.AppendSystem(
            _chatLog,
            string.IsNullOrWhiteSpace(news) ? "News cleared." : "News updated.");
    }

    private void AppendBanEntryChat(in ServerBanPacket ban)
    {
        var by = string.IsNullOrWhiteSpace(ban.IpAddress) ? "?" : ban.IpAddress;
        var reason = string.IsNullOrWhiteSpace(ban.Reason) ? "banned" : ban.Reason;
        InGameChatService.AppendSystem(_chatLog, $"  {ban.Account} — {reason} (by {by})");
    }

    private void AppendNewsChat(string news)
    {
        if (string.IsNullOrWhiteSpace(news))
        {
            return;
        }

        foreach (var line in news.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (trimmed.Length == 0)
            {
                continue;
            }

            InGameChatService.AppendSystem(_chatLog, trimmed);
        }
    }

    private void ApplyLocalWarp(in ServerStateGamePacket warp)
    {
        _simulation.ApplyNetworkWarp(warp);
        ApplyLocalWarpCamera(warp);
    }

    private void ApplyLocalWarpCamera(in ServerStateGamePacket warp)
    {
        _cameraPanOffset = Vector2.Zero;
        _cameraFocus = new Vector2(
            warp.X + GameConstants.TileSize / 2f,
            warp.Y + GameConstants.TileSize / 2f);
    }

    private void AppendAdminActionChat(in ServerAdminPacket admin)
    {
        var adminName = admin.AdminPlayerId == _client.PlayerId
            ? _context.PlayerName
            : _remotePlayers.GetDisplayName((byte)admin.AdminPlayerId) ?? $"Player{admin.AdminPlayerId}";
        var targetName = _remotePlayers.GetDisplayName((byte)admin.TargetPlayerId)
            ?? $"Player{admin.TargetPlayerId}";
        var verb = admin.Command == AdminCommands.Ban ? "banned" : "kicked";
        InGameChatService.AppendSystem(_chatLog, $"{targetName} has been {verb} by {adminName}");
        if (admin.TargetPlayerId != _client.PlayerId)
        {
            _remotePlayers.Remove((byte)admin.TargetPlayerId);
        }
    }

    private void SendGlobalChat(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!IsLocalAdmin())
        {
            InGameChatService.AppendSystem(_chatLog, "Only admins can send global messages.");
            return;
        }

        InGameChatService.AppendLocalGlobal(_chatLog, GetLocalChatDisplayName(), message);
        _client.SendGlobal(message);
    }

    private void SendWhisperChat(string recipientPrefix, string message)
    {
        if (string.IsNullOrWhiteSpace(recipientPrefix))
        {
            InGameChatService.AppendSystem(_chatLog, "Player not found: try adding more letters to the name!");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            InGameChatService.AppendSystem(_chatLog, $"Player not found: {recipientPrefix}");
            return;
        }

        if (recipientPrefix.Length > 20)
        {
            InGameChatService.AppendSystem(_chatLog, "Player not found: try adding more letters to the name!");
            return;
        }

        if (!WhisperRecipientMatcher.TryMatch(
                recipientPrefix,
                _client.PlayerId,
                _remotePlayers.EnumerateDisplayNames(),
                out var recipientId,
                out var recipientName))
        {
            InGameChatService.AppendSystem(_chatLog, $"Player not found: {recipientPrefix}");
            return;
        }

        InGameChatService.AppendLocalWhisper(_chatLog, GetLocalChatDisplayName(), recipientName, message);
        _client.SendWhisper(recipientId, message);
    }

    private Entity GetLocalPlayerEntity()
    {
        var query = new Arch.Core.QueryDescription().WithAll<InputControlled>();
        Entity player = default;
        _simulation.World.Query(in query, (Entity entity) => player = entity);
        return player;
    }

    private void ApplyUiInput(UiInputState ui, GameTime gameTime)
    {
        if (ui.ToggleMiniMapPressed)
        {
            _showMiniMap = !_showMiniMap;
        }

        if (ui.ToggleStatusPanelPressed)
        {
            _showStatusPanel = !_showStatusPanel;
        }

        if (ui.ZoomSteps != 0)
        {
            _camera.AdjustZoom(ui.ZoomSteps * RenderConstants.ZoomStep);
        }

        if (!ui.CameraPanLeft && !ui.CameraPanRight && !ui.CameraPanUp && !ui.CameraPanDown)
        {
            return;
        }

        var panSpeed = 400f * (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (ui.CameraPanLeft)
        {
            _cameraPanOffset.X -= panSpeed;
        }

        if (ui.CameraPanRight)
        {
            _cameraPanOffset.X += panSpeed;
        }

        if (ui.CameraPanUp)
        {
            _cameraPanOffset.Y -= panSpeed;
        }

        if (ui.CameraPanDown)
        {
            _cameraPanOffset.Y += panSpeed;
        }

        _cameraPanOffset.X = Math.Clamp(_cameraPanOffset.X, -UiLayout.WorldViewportWidth * 0.5f, UiLayout.WorldViewportWidth * 0.5f);
        _cameraPanOffset.Y = Math.Clamp(_cameraPanOffset.Y, -UiLayout.WorldViewportHeight * 0.5f, UiLayout.WorldViewportHeight * 0.5f);
    }

    private UiRenderer CreateUiRenderer()
    {
        var ui = new UiRenderer(_context.Assets);
        ui.LoadContent();
        return ui;
    }

    private DeathOverlayRenderer CreateDeathOverlayRenderer()
    {
        var overlay = new DeathOverlayRenderer(_context.Assets);
        overlay.LoadContent();
        return overlay;
    }

    private OrbedOverlayRenderer CreateOrbedOverlayRenderer()
    {
        var overlay = new OrbedOverlayRenderer(_context.Assets);
        overlay.LoadContent();
        return overlay;
    }

    private ResearchCompleteOverlayRenderer CreateResearchCompleteOverlayRenderer()
    {
        var overlay = new ResearchCompleteOverlayRenderer(_context.Assets);
        overlay.LoadContent();
        return overlay;
    }

    private ChatOverlayRenderer CreateChatOverlayRenderer()
    {
        var overlay = new ChatOverlayRenderer(_context.Assets);
        overlay.LoadContent();
        return overlay;
    }
}
