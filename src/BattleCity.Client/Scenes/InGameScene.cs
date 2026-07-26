using BattleCity.Client.Assets;
using BattleCity.Client.Audio;
using BattleCity.Client.Chat;
using BattleCity.Client.Input;
using BattleCity.Client.Rendering;
using Arch.Core;

using BattleCity.Core.City;
using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Gameplay;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using NumericsVector2 = System.Numerics.Vector2;

namespace BattleCity.Client.Scenes;

public sealed class InGameScene : IScene
{
    private readonly SceneContext _context;
    private readonly Camera2D _camera = new();
    private readonly GameSimulation _simulation = new();
    private readonly InputManager _input = new();
    private readonly MenuInputReader _menuInput = new();
    private GameplayAudioController _gameplayAudio = null!;

    private RenderPipeline _renderPipeline = null!;
    private TileMap _tileMap = null!;
    private CityLayout _cityLayout = null!;
    private Vector2 _cameraFocus;
    private Vector2 _cameraPanOffset;
    private Vector2 _buildMenuAnchor;
    private bool _showMiniMap;
    private bool _showStatusPanel = true;
    private bool _showSettingsMenu;
    private int _settingsSelectedIndex;
    private bool _showBuildMenu;
    private int _buildModeSlot;
    private float _animationTime;
    private bool _showBuildPreview;
    private int _buildPreviewGridX;
    private int _buildPreviewGridY;
    private int _buildPreviewTypeCode;
    private bool _buildPreviewIsValid;
    private bool _buildPreviewIsDemolish;
    private bool _loaded;
    private readonly InGameChatLog _chatLog = new();
    private readonly InGameChatInput _chatInput = new();

    public InGameScene(SceneContext context)
    {
        _context = context;
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

        _tileMap = LoadTileMap();
        _simulation.TileMap = _tileMap;

        _cityLayout = LevelLoader.LoadLegacyCity(_context.SelectedCity, _context.CityDesign);
        _simulation.LoadCityLayout(_cityLayout);
        _simulation.SpawnDemoItems();

        var spawn = _simulation.TryGetCityRespawnPosition(
                cityId: 0,
                out var openSpawn,
                out _)
            ? new NumericsVector2(openSpawn.X, openSpawn.Y)
            : new NumericsVector2(_cityLayout.GetSpawnPosition().X, _cityLayout.GetSpawnPosition().Y);

        _cameraFocus = new Vector2(spawn.X + GameConstants.TileSize / 2f, spawn.Y + GameConstants.TileSize / 2f);
        _simulation.CreatePlayerEntity(
            spawn,
            isAdmin: TankSpriteSelector.IsAdminAccount(_context.PlayerName));
        _simulation.SpawnPracticeBots(spawn);

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

        var keyboard = Keyboard.GetState();

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

        // Settings must win over chat so Enter confirms the settings item instead of opening chat.
        if (_showSettingsMenu)
        {
            var settingsFrame = _input.Poll(_camera, playerCenter, worldWidth, _context.Presentation);
            if (HandleSettingsInput(settingsFrame.Ui, out var leaveToMenu) && leaveToMenu)
            {
                _context.Audio.StopEngine();
                return SceneTransition.MainMenu;
            }
        }
        else
        {
            var chatUpdate = _chatInput.Update(keyboard);
            if (chatUpdate.Submitted)
            {
                InGameChatService.AppendLocalOutgoing(
                    _chatLog,
                    _context.PlayerName,
                    chatUpdate.Message,
                    isDead: _simulation.TryGetPlayerLifeState(out var life) && life.IsDead);
            }

            if (!_chatInput.IsActive)
            {
                var frameInput = _input.Poll(_camera, playerCenter, worldWidth, _context.Presentation);
                if (HandleSettingsInput(frameInput.Ui, out var leaveToMenu))
                {
                    if (leaveToMenu)
                    {
                        _context.Audio.StopEngine();
                        return SceneTransition.MainMenu;
                    }
                }
                else
                {
                    HandleBuildInput(frameInput.Ui, playerCenter, worldWidth);
                    UpdateBuildPreview(frameInput.Ui, playerCenter, worldWidth);
                    NotifyFailedItemDrops(frameInput.Gameplay);
                    InputCommandWriter.Apply(_simulation.World, frameInput.Gameplay);
                    ApplyUiInput(frameInput.Ui, gameTime);
                }
            }
        }

        if (!_showSettingsMenu)
        {
            _simulation.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        }
        _animationTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_simulation.TryGetPlayerPosition(out playerPosition))
        {
            _cameraFocus = new Vector2(
                playerPosition.X + GameConstants.TileSize / 2f,
                playerPosition.Y + GameConstants.TileSize / 2f);
        }

        _camera.CenterOn(_cameraFocus + _cameraPanOffset);

        _gameplayAudio.PlaySimulationEvents(
            _simulation.ConsumeSoundEvents(),
            playerCenter);

        _context.Audio.SetEngineRunning(
            !_chatInput.IsActive
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
        _simulation.Dispose();
        _loaded = false;
    }

    private void HandleBuildInput(UiInputState ui, Vector2 playerCenter, int worldWidth)
    {
        if (ui.MouseRightClicked && ui.PointerOverWorld)
        {
            _showBuildMenu = true;
            _buildModeSlot = 0;
            _buildMenuAnchor = ui.MouseLogicalPosition;
            return;
        }

        if (!ui.MouseLeftClicked || !_simulation.TryGetCityBuild(0, out var build))
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
            if (_simulation.TryDemolishAt(gridX, gridY))
            {
                _buildModeSlot = 0;
            }

            return;
        }

        if (_simulation.TryPlaceBuilding(
                _buildModeSlot,
                gridX,
                gridY,
                new NumericsVector2(playerCenter.X, playerCenter.Y)))
        {
            _buildModeSlot = 0;
        }

        // Keep build mode active on failure so the player can click another tile.
    }

    private void UpdateBuildPreview(UiInputState ui, Vector2 playerCenter, int worldWidth)
    {
        _showBuildPreview = false;

        if (_buildModeSlot == 0 || !ui.PointerOverWorld || !_simulation.TryGetCityBuild(0, out var build))
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

        CityBuildState? cityBuild = null;
        if (_simulation.TryGetCityBuild(0, out var build))
        {
            cityBuild = build;
        }

        var homeCcGridX = 0;
        var homeCcGridY = 0;
        var cityCenterWorldPosition = new Vector2(_cityLayout.GetCameraFocus().X, _cityLayout.GetCameraFocus().Y);
        Vector2? nearestOrbableCity = null;
        if (_simulation.TryGetCityBuild(0, out var homeCity))
        {
            homeCcGridX = homeCity.CommandCenterGridX;
            homeCcGridY = homeCity.CommandCenterGridY;
            if (CommandCenterLookup.TryGetWorldPosition(
                    _simulation.World,
                    homeCity.CommandCenterGridX,
                    homeCity.CommandCenterGridY,
                    out var commandCenterPosition))
            {
                cityCenterWorldPosition = new Vector2(commandCenterPosition.X, commandCenterPosition.Y);
            }

            if (CommandCenterLookup.TryFindNearestOtherWorldPosition(
                    _simulation.World,
                    homeCity.CommandCenterGridX,
                    homeCity.CommandCenterGridY,
                    new NumericsVector2(_cameraFocus.X, _cameraFocus.Y),
                    out var orbTarget))
            {
                nearestOrbableCity = new Vector2(orbTarget.X, orbTarget.Y);
            }
        }

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

        return new RenderContext
        {
            Camera = _camera,
            TileMap = _tileMap,
            World = _simulation.World,
            FocusWorldPosition = _cameraFocus,
            ScreenWidth = _camera.ViewportWidth,
            ScreenHeight = _camera.ViewportHeight,
            ShowMiniMap = _showMiniMap,
            ShowStatusPanel = _showStatusPanel,
            ShowSettingsMenu = _showSettingsMenu,
            SettingsSelectedIndex = _settingsSelectedIndex,
            LoadedCityName = _cityLayout.CityName,
            BuildingCount = _cityLayout.Buildings.Count,
            PlayerDisplayName = _context.PlayerName,
            PlayerHealth = playerHealth,
            PlayerMaxHealth = playerMaxHealth,
            PlayerRespawnSeconds = playerRespawnSeconds,
            PlayerInventory = inventory,
            CloakRechargeSeconds = cloakRecharge,
            FlareRechargeSeconds = flareRecharge,
            CloakRechargeUnlocked = CityEquipmentRules.HasRechargeableCloak(cityBuild),
            FlareRechargeUnlocked = CityEquipmentRules.HasRechargeableFlare(cityBuild),
            CityCenterWorldPosition = cityCenterWorldPosition,
            NearestOrbableCityWorldPosition = nearestOrbableCity,
            HomeCommandCenterGridX = homeCcGridX,
            HomeCommandCenterGridY = homeCcGridY,
            IsUnderAttack = isUnderAttack,
            UnderAttackFlashVisible = underAttackFlashVisible,
            ShowBuildMenu = _showBuildMenu,
            BuildMenuAnchor = _buildMenuAnchor,
            CityBuild = cityBuild,
            BuildModeSlot = _buildModeSlot,
            AnimationTime = _animationTime,
            ShowOrbedOverlay = showOrbedOverlay,
            OrbedOverlayIsVictim = orbedOverlayIsVictim,
            OrbedOverlayMessage = orbedOverlayMessage,
            ShowBuildPreview = _showBuildPreview,
            BuildPreviewGridX = _buildPreviewGridX,
            BuildPreviewGridY = _buildPreviewGridY,
            BuildPreviewTypeCode = _buildPreviewTypeCode,
            BuildPreviewIsValid = _buildPreviewIsValid,
            BuildPreviewIsDemolish = _buildPreviewIsDemolish,
            ShowResearchCompleteOverlay = showResearchCompleteOverlay,
            ResearchCompleteOverlayMessage = researchCompleteOverlayMessage,
            ChatLines = _chatLog.Lines,
            IsChatting = _chatInput.IsActive,
            ChatDraft = _chatInput.Draft,
            ObserverCityId = _simulation.TryGetPlayerCityId(out var observerCityId) ? observerCityId : 0,
        };
    }

    /// <returns>True when settings UI consumed the input.</returns>
    private bool HandleSettingsInput(UiInputState ui, out bool leaveToMenu)
    {
        leaveToMenu = false;
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
                    case 4:
                        leaveToMenu = true;
                        break;
                }
            }

        return true;
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

    private static TileMap LoadTileMap() => InGameWorldLoader.LoadTileMap();

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

    private void NotifyFailedItemDrops(GameplayInputState gameplay)
    {
        if (!_simulation.TryGetPlayerInventory(out var inventory)
            || !_simulation.TryGetPlayerPosition(out var position))
        {
            return;
        }

        _simulation.TryGetCityBuild(0, out var cityBuild);
        var player = GetLocalPlayerEntity();
        var tankTopLeft = new NumericsVector2(position.X, position.Y);

        if (gameplay.DropSelectedItemPressed
            && inventory.GetCount(inventory.SelectedItemType) > 0
            && ItemDropFeedback.GetFailureMessage(
                _simulation.World,
                player,
                tankTopLeft,
                inventory.SelectedItemType,
                cityBuild) is { } selectedFailure)
        {
            InGameChatService.AppendSystem(_chatLog, selectedFailure);
        }

        if (gameplay.DropBombPressed
            && inventory.GetCount(ItemType.Bomb) > 0
            && ItemDropFeedback.GetFailureMessage(
                _simulation.World,
                player,
                tankTopLeft,
                ItemType.Bomb,
                cityBuild) is { } bombFailure)
        {
            InGameChatService.AppendSystem(_chatLog, bombFailure);
        }
    }

    private Entity GetLocalPlayerEntity()
    {
        var query = new QueryDescription().WithAll<InputControlled>();
        Entity player = default;
        _simulation.World.Query(in query, (Entity entity) => player = entity);
        return player;
    }

    private ChatOverlayRenderer CreateChatOverlayRenderer()
    {
        var overlay = new ChatOverlayRenderer(_context.Assets);
        overlay.LoadContent();
        return overlay;
    }
}
