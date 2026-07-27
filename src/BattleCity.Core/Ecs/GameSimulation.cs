using System.Numerics;

using Arch.Core;

using BattleCity.Core.Audio;
using BattleCity.Core.City;
using BattleCity.Core.Collision;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Levels;
using BattleCity.Core.Maps;
using BattleCity.Core.Network;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Gameplay;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Core.Ecs;

public sealed class GameSimulation : IDisposable
{
    public const float FixedDeltaSeconds = 1f / 60f;

    private readonly World _world;
    private double _accumulatorSeconds;
    private TileMap _tileMap = TileMap.CreateEmpty();
    private CityLayout? _loadedCity;
    private CityBuildState? _cityBuild;
    private readonly Dictionary<int, CityBuildState> _cityBuilds = new();
    private readonly SimulationAudioBuffer _audioBuffer = new();
    private OrbEvent? _pendingOrbEvent;
    private readonly List<ServerDeathPacket> _pendingDeathEvents = [];
    private ushort _nextNetworkItemId = 1;
    private ushort _nextNetworkBuildingId = 1;
    private readonly List<ServerBuildingPacket> _removedBuildingSnapshots = [];

    public GameSimulation()
    {
        _world = World.Create();
    }

    public World World => _world;

    public TileMap TileMap
    {
        get => _tileMap;
        set => _tileMap = value;
    }

    public CityLayout? LoadedCity => _loadedCity;

    /// <summary>When true, orb detection runs but local apply/notification is deferred to network packet.</summary>
    public bool SuppressLocalOrbEffects { get; set; }

    /// <summary>When true, item drop/pickup/medkit are deferred to server packets.</summary>
    public bool SuppressLocalItemDrops { get; set; }

    /// <summary>When true, bomb fuse/detonation is deferred to server <c>smExplosion</c>.</summary>
    public bool SuppressLocalBombDetonation { get; set; }

    /// <summary>When true, factory bay spawns are deferred to server AddItem (online clients).</summary>
    public bool SuppressLocalFactoryProduction { get; set; }

    /// <summary>When true, bomb detonations queue network explosion/removal events (server).</summary>
    public bool ReportBombEventsToNetwork { get; set; }

    /// <summary>When true, factory bay spawns enqueue AddItem packets for broadcast.</summary>
    public bool ReportFactoryItemSpawnsToNetwork { get; set; }

    /// <summary>When true, dying tanks return placeables to factory bays.</summary>
    public bool ReturnInventoryPlaceablesOnDeath { get; set; } = true;

    /// <summary>When true, local player respawn waits for authoritative <c>smWarp</c>.</summary>
    public bool SuppressLocalPlayerRespawn { get; set; }

    /// <summary>When true, network-player respawn is broadcast via <c>smWarp</c>/<c>smRespawn</c> (server).</summary>
    public bool ReportRespawnEventsToNetwork { get; set; }

    /// <summary>When true, remote tanks stay dead until authoritative <c>smRespawn</c> (online clients).</summary>
    public bool DeferRemotePlayerRespawn { get; set; }

    /// <summary>Invoked when the local player fires so online clients can send <c>cmShoot</c>.</summary>
    public Action<ClientShotPacket>? ReportLocalShot { get; set; }

    /// <summary>When false, remote network tanks only die from <c>smDeath</c> (online clients).</summary>
    public bool NetworkPlayersUseLocalHealthDeath { get; set; }

    public bool NetworkPlayersUseLocalBulletDamage { get; set; } = true;

    private readonly List<ServerHpPacket> _pendingHpEvents = new();
    private readonly List<PendingExplosionEvent> _pendingExplosionEvents = [];
    private readonly List<PendingRespawnEvent> _pendingRespawnEvents = [];
    private readonly List<ServerAddItemPacket> _pendingFactoryAddItems = [];
    private readonly List<ServerBuildingPacket> _pendingBombBuildingRemovals = [];

    public GameSoundEvent[] ConsumeSoundEvents() => _audioBuffer.Drain();

    public void LoadCityLayout(CityLayout layout)
    {
        _loadedCity = layout;
        _cityBuilds.Clear();

        var cityId = CityCatalog.TryGetId(layout.CityName, out var resolvedCityId) ? resolvedCityId : 0;
        var build = new CityBuildState { CityId = cityId };
        CityBuildInitializer.InitializeFromLayout(build, layout, _tileMap);
        LevelLoader.SpawnCommandCenter(_world, build.CommandCenterGridX, build.CommandCenterGridY, cityId);
        LevelLoader.SpawnRemoteCommandCenters(
            _world,
            _tileMap,
            build.CommandCenterGridX,
            build.CommandCenterGridY);

        var spawnedFromLayout = 0;
        foreach (var building in layout.Buildings)
        {
            if (OverlapsBuildingFootprint(
                    building.GridX,
                    building.GridY,
                    build.CommandCenterGridX,
                    build.CommandCenterGridY))
            {
                continue;
            }

            LevelLoader.SpawnBuilding(_world, building, cityId);
            spawnedFromLayout++;
        }

        build.CurrentBuildingCount = Math.Max(1, spawnedFromLayout + 1);
        build.MaxBuildingCount = build.CurrentBuildingCount;
        _cityBuilds[cityId] = build;
        _cityBuild = build;
        AssignNetworkBuildingIds();
    }

    private static bool OverlapsBuildingFootprint(int gridAnchorX, int gridAnchorY, int otherGridX, int otherGridY) =>
        gridAnchorX >= otherGridX - 2
        && gridAnchorX <= otherGridX + 2
        && gridAnchorY >= otherGridY - 2
        && gridAnchorY <= otherGridY + 2;

    public void SpawnDemoItems(int? cityId = null)
    {
        SpawnDemoItemsCore(cityId ?? _cityBuild?.CityId ?? 0);
    }

    private void SpawnDemoItemsCore(int cityId)
    {
        if (_loadedCity is null)
        {
            return;
        }

        var spawn = _loadedCity.GetSpawnPosition();
        var originX = (int)(spawn.X / GameConstants.TileSize);
        var originY = (int)(spawn.Y / GameConstants.TileSize);

        GameplayEntityFactory.CreatePlacedItem(_world, ItemType.Wall, originX + 4, originY - 2, cityId: cityId);
        GameplayEntityFactory.CreatePlacedItem(_world, ItemType.Mine, originX + 5, originY - 2, cityId: cityId);
        GameplayEntityFactory.CreatePlacedItem(_world, ItemType.Bomb, originX + 3, originY - 1, active: false, cityId: cityId);
        GameplayEntityFactory.CreatePlacedItem(_world, ItemType.Turret, originX + 6, originY - 3, cityId: cityId);
        GameplayEntityFactory.CreatePlacedItem(_world, ItemType.Sleeper, originX + 7, originY - 3, cityId: cityId);
        GameplayEntityFactory.CreatePlacedItem(_world, ItemType.Plasma, originX + 8, originY - 3, cityId: cityId);
        GameplayEntityFactory.CreatePlacedItem(_world, ItemType.Dfg, originX + 2, originY + 1, cityId: cityId);
    }

    public void SpawnDemoTurret(int gridX, int gridY, int cityId = 0) =>
        GameplayEntityFactory.CreatePlacedItem(_world, ItemType.Turret, gridX, gridY, cityId: cityId);

    public Entity CreatePlayerEntity(
        Vector2 position,
        int spriteSourceX = 0,
        bool isMayor = true,
        bool isAdmin = false,
        int? cityId = null)
    {
        var resolvedCityId = cityId ?? _cityBuild?.CityId ?? 0;
        TryGetCityBuild(resolvedCityId, out var cityBuild);
        var inventory = PlayerInventory.CreateLoadoutForCity(cityBuild);
        // Online joins pass an explicit city id — start without an orb (factory / pickups supply it).
        // Also refuse a second orb when this city already has one.
        if (cityId.HasValue || OrbCityRules.CityAlreadyHasOrb(_world, resolvedCityId))
        {
            inventory.Orb = 0;
        }

        return _world.Create(
            new Transform2D { Position = position, PreviousPosition = position },
            new Velocity { Value = Vector2.Zero },
            new SpriteRef
            {
                TextureKey = "Sprites/Tanks",
                SourceX = spriteSourceX,
                SourceY = TankSpriteSelector.GetSourceY(resolvedCityId, resolvedCityId, isMayor, isAdmin) * GameConstants.TileSize,
                Width = GameConstants.TileSize,
                Height = GameConstants.TileSize,
            },
            new Collider
            {
                OffsetX = GameConstants.PlayerCollisionInset,
                OffsetY = GameConstants.PlayerCollisionInset,
                Width = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Height = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Layer = CollisionLayer.Player,
            },
            new InputControlled(),
            new InputCommand(),
            new TankFacing { Direction = 0, TurnCooldownSeconds = 0f },
            new Health { Current = GameConstants.MaxHealth, Max = GameConstants.MaxHealth },
            new TankLifeState { SpawnPosition = position, KillerCityId = EntityCityLookup.UnknownCity },
            new CityAffiliation { CityId = resolvedCityId },
            new MayorStatus { IsMayor = isMayor },
            inventory,
            new WeaponState(),
            new TankStatus(),
            new CityAlertState(),
            new CityOrbedState(),
            new CityResearchCompleteState());
    }

    public Entity CreateNetworkPlayerEntity(Vector2 position, byte playerId, int cityId = 0, int spriteSourceX = 0)
    {
        TryGetCityBuild(cityId, out var cityBuild);
        var inventory = PlayerInventory.CreateLoadoutForCity(cityBuild);
        inventory.Orb = 0;

        return _world.Create(
            new Transform2D { Position = position, PreviousPosition = position },
            new Velocity { Value = Vector2.Zero },
            new SpriteRef
            {
                TextureKey = "Sprites/Tanks",
                SourceX = spriteSourceX,
                SourceY = 0,
                Width = GameConstants.TileSize,
                Height = GameConstants.TileSize,
            },
            new Collider
            {
                OffsetX = GameConstants.PlayerCollisionInset,
                OffsetY = GameConstants.PlayerCollisionInset,
                Width = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Height = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Layer = CollisionLayer.Player,
            },
            new TankFacing { Direction = 0, TurnCooldownSeconds = 0f },
            new Health { Current = GameConstants.MaxHealth, Max = GameConstants.MaxHealth },
            new TankLifeState { SpawnPosition = position, KillerCityId = EntityCityLookup.UnknownCity },
            new CityAffiliation { CityId = cityId },
            new MayorStatus { IsMayor = false },
            new NetworkIdentity { PlayerId = playerId },
            inventory,
            new WeaponState(),
            new TankStatus(),
            new CityAlertState(),
            new CityOrbedState(),
            new CityResearchCompleteState());
    }

    public bool TryGetNetworkPlayerEntity(byte playerId, out Entity entity)
    {
        var query = new QueryDescription().WithAll<NetworkIdentity>();
        var foundEntity = Entity.Null;
        var found = false;

        World.Query(
            in query,
            (Entity candidate, ref NetworkIdentity identity) =>
            {
                if (identity.PlayerId != playerId)
                {
                    return;
                }

                foundEntity = candidate;
                found = true;
            });

        entity = foundEntity;
        return found;
    }

    public bool IsNetworkPlayerDead(byte playerId) =>
        TryGetNetworkPlayerEntity(playerId, out var entity)
        && _world.Has<TankLifeState>(entity)
        && _world.Get<TankLifeState>(entity).IsDead;

    public bool TryApplyNetworkUpdate(byte playerId, ushort x, ushort y, int turn, int move, byte direction)
    {
        if (!TryGetNetworkPlayerEntity(playerId, out var entity))
        {
            return false;
        }

        ref var transform = ref _world.Get<Transform2D>(entity);
        var currentX = (int)transform.Position.X;
        var currentY = (int)transform.Position.Y;
        if (Math.Abs(x - currentX) >= 300 || Math.Abs(y - currentY) >= 300)
        {
            return false;
        }

        transform.Position = new Vector2(x, y);
        transform.PreviousPosition = transform.Position;

        ref var facing = ref _world.Get<TankFacing>(entity);
        facing.Direction = direction;

        ref var sprite = ref _world.Get<SpriteRef>(entity);
        sprite.SourceX = direction / 2 * GameConstants.TileSize;

        return true;
    }

    public bool TryRemoveNetworkPlayer(byte playerId)
    {
        if (!TryGetNetworkPlayerEntity(playerId, out var entity))
        {
            return false;
        }

        _world.Destroy(entity);
        return true;
    }

    public bool TryGetNetworkPlayerSnapshot(byte playerId, out ServerPlayerSnapshot snapshot)
    {
        if (!TryGetNetworkPlayerEntity(playerId, out var entity))
        {
            snapshot = default;
            return false;
        }

        ref var transform = ref _world.Get<Transform2D>(entity);
        ref var facing = ref _world.Get<TankFacing>(entity);

        snapshot = new ServerPlayerSnapshot(
            playerId,
            (ushort)Math.Clamp((int)transform.Position.X, 0, ushort.MaxValue),
            (ushort)Math.Clamp((int)transform.Position.Y, 0, ushort.MaxValue),
            ClientUpdatePacket.EncodeAxisInput(0),
            ClientUpdatePacket.EncodeAxisInput(0),
            (byte)facing.Direction);

        return true;
    }

    public bool TryGetNetworkPlayerPosition(byte playerId, out Vector2 position)
    {
        if (!TryGetNetworkPlayerEntity(playerId, out var entity))
        {
            position = default;
            return false;
        }

        position = _world.Get<Transform2D>(entity).Position;
        return true;
    }

    public void SetNetworkPlayerMayor(byte playerId, bool isMayor)
    {
        if (!TryGetNetworkPlayerEntity(playerId, out var entity))
        {
            return;
        }

        ref var mayor = ref _world.Get<MayorStatus>(entity);
        mayor.IsMayor = isMayor;
    }

    public Entity CreateBotEntity(
        Vector2 position,
        int cityId,
        int spriteSourceX = 48,
        float aggroRangePixels = 2000f)
    {
        return _world.Create(
            new Transform2D { Position = position, PreviousPosition = position },
            new Velocity { Value = Vector2.Zero },
            new SpriteRef
            {
                TextureKey = "Sprites/Tanks",
                SourceX = spriteSourceX,
                SourceY = TankSpriteSelector.EnemyRegularRow * GameConstants.TileSize,
                Width = GameConstants.TileSize,
                Height = GameConstants.TileSize,
            },
            new Collider
            {
                OffsetX = GameConstants.PlayerCollisionInset,
                OffsetY = GameConstants.PlayerCollisionInset,
                Width = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Height = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Layer = CollisionLayer.Player,
            },
            new BotController { AggroRangePixels = aggroRangePixels },
            new PatrolBehavior(),
            new TankFacing { Direction = 16, TurnCooldownSeconds = 0f },
            new Health { Current = GameConstants.MaxHealth, Max = GameConstants.MaxHealth },
            new TankLifeState { SpawnPosition = position },
            new CityAffiliation { CityId = cityId },
            new TankStatus());
    }

    public Entity CreatePatrolEntity(Vector2 position, Vector2 velocity, int spriteSourceX = 0)
    {
        return _world.Create(
            new Transform2D { Position = position, PreviousPosition = position },
            new Velocity { Value = velocity },
            new SpriteRef
            {
                TextureKey = "Sprites/Tanks",
                SourceX = spriteSourceX,
                SourceY = 0,
                Width = GameConstants.TileSize,
                Height = GameConstants.TileSize,
            },
            new Collider
            {
                OffsetX = GameConstants.PlayerCollisionInset,
                OffsetY = GameConstants.PlayerCollisionInset,
                Width = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Height = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
                Layer = CollisionLayer.Player,
            },
            new PatrolBehavior(),
            new Health { Current = GameConstants.MaxHealth, Max = GameConstants.MaxHealth },
            new TankLifeState { SpawnPosition = position },
            new CityAffiliation { CityId = 1 },
            new TankStatus());
    }

    public Entity CreateBuildingObstacle(Vector2 position, int width, int height)
    {
        return _world.Create(
            new Transform2D { Position = position, PreviousPosition = position },
            new Collider
            {
                OffsetX = 0,
                OffsetY = 0,
                Width = width,
                Height = height,
                Layer = CollisionLayer.Building,
            });
    }

    public Entity CreateDemoEntity(Vector2 position, Vector2 velocity, int spriteSourceX = 0) =>
        CreatePatrolEntity(position, velocity, spriteSourceX);

    public void Update(float elapsedSeconds)
    {
        _accumulatorSeconds += elapsedSeconds;

        while (_accumulatorSeconds >= FixedDeltaSeconds)
        {
            Tick(FixedDeltaSeconds);
            _accumulatorSeconds -= FixedDeltaSeconds;
        }
    }

    public void SpawnPracticeBots(Vector2 playerSpawn)
    {
        const float practiceAggroPixels = 720f;
        var spawns = FindPracticeBotSpawns(playerSpawn, count: 2);
        CreateBotEntity(spawns[0], cityId: EntityCityLookup.UnknownCity, spriteSourceX: 48, aggroRangePixels: practiceAggroPixels);
        CreateBotEntity(spawns[1], cityId: EntityCityLookup.UnknownCity, spriteSourceX: 96, aggroRangePixels: practiceAggroPixels);
    }

    private Vector2[] FindPracticeBotSpawns(Vector2 playerSpawn, int count)
    {
        var results = new Vector2[count];
        var cityBounds = GetLoadedCityWorldBounds();
        var tile = GameConstants.TileSize;

        // Prefer open ground south/east of the city, well outside the building footprint.
        var candidates = new List<Vector2>(capacity: 64);
        if (cityBounds is { } bounds)
        {
            var centerX = (bounds.MinX + bounds.MaxX) / 2f;
            var southY = bounds.MaxY + tile * 12;
            var eastX = bounds.MaxX + tile * 12;
            var westX = bounds.MinX - tile * 12;
            var northY = bounds.MinY - tile * 12;

            for (var i = 0; i < 16; i++)
            {
                var spread = (i - 7.5f) * tile * 2f;
                candidates.Add(new Vector2(centerX + spread, southY + (i % 4) * tile * 2));
                candidates.Add(new Vector2(eastX + (i % 4) * tile * 2, bounds.MinY + (bounds.MaxY - bounds.MinY) * 0.5f + spread));
                candidates.Add(new Vector2(westX - (i % 4) * tile * 2, bounds.MinY + (bounds.MaxY - bounds.MinY) * 0.5f + spread));
                candidates.Add(new Vector2(centerX + spread, northY - (i % 4) * tile * 2));
            }
        }
        else
        {
            candidates.Add(playerSpawn + new Vector2(0f, tile * 20));
            candidates.Add(playerSpawn + new Vector2(tile * 20, 0f));
            candidates.Add(playerSpawn + new Vector2(-tile * 20, tile * 8));
            candidates.Add(playerSpawn + new Vector2(tile * 12, tile * 16));
        }

        var filled = 0;
        foreach (var candidate in candidates)
        {
            if (filled >= count)
            {
                break;
            }

            var snapped = SnapTankToTile(candidate);
            if (!IsOpenTankSpawn(snapped))
            {
                continue;
            }

            var tooCloseToPlayer = Vector2.DistanceSquared(snapped, playerSpawn) < (tile * 10f) * (tile * 10f);
            var tooCloseToOther = false;
            for (var i = 0; i < filled; i++)
            {
                if (Vector2.DistanceSquared(snapped, results[i]) < (tile * 6f) * (tile * 6f))
                {
                    tooCloseToOther = true;
                    break;
                }
            }

            if (tooCloseToPlayer || tooCloseToOther)
            {
                continue;
            }

            results[filled++] = snapped;
        }

        // Fallback: far offsets from the player if city search failed.
        var fallbackOffsets = new[]
        {
            new Vector2(0f, tile * 24),
            new Vector2(tile * 24, tile * 8),
            new Vector2(-tile * 24, tile * 8),
            new Vector2(tile * 16, -tile * 20),
        };
        for (var i = filled; i < count; i++)
        {
            var fallback = SnapTankToTile(playerSpawn + fallbackOffsets[i % fallbackOffsets.Length]);
            if (!IsOpenTankSpawn(fallback))
            {
                // Walk outward until clear.
                for (var step = 1; step <= 40; step++)
                {
                    var probe = SnapTankToTile(fallback + new Vector2(0f, step * tile));
                    if (IsOpenTankSpawn(probe))
                    {
                        fallback = probe;
                        break;
                    }
                }
            }

            results[i] = fallback;
        }

        return results;
    }

    private (float MinX, float MinY, float MaxX, float MaxY)? GetLoadedCityWorldBounds()
    {
        if (_loadedCity is null || _loadedCity.Buildings.Count == 0)
        {
            return null;
        }

        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;

        foreach (var building in _loadedCity.Buildings)
        {
            var topLeft = BuildingPlacement.GridAnchorToWorldPosition(building.GridX, building.GridY);
            minX = Math.Min(minX, topLeft.X);
            minY = Math.Min(minY, topLeft.Y);
            maxX = Math.Max(maxX, topLeft.X + GameConstants.BuildingCollisionSize);
            maxY = Math.Max(maxY, topLeft.Y + GameConstants.BuildingCollisionSize);
        }

        return (minX, minY, maxX, maxY);
    }

    private static Vector2 SnapTankToTile(Vector2 position) =>
        new(
            MathF.Floor(position.X / GameConstants.TileSize) * GameConstants.TileSize,
            MathF.Floor(position.Y / GameConstants.TileSize) * GameConstants.TileSize);

    private bool IsOpenTankSpawn(Vector2 position)
    {
        var max = GameConstants.WorldSizePixels - GameConstants.TileSize;
        if (position.X < 0 || position.Y < 0 || position.X > max || position.Y > max)
        {
            return false;
        }

        var collider = new Collider
        {
            OffsetX = GameConstants.PlayerCollisionInset,
            OffsetY = GameConstants.PlayerCollisionInset,
            Width = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
            Height = GameConstants.TileSize - GameConstants.PlayerCollisionInset * 2,
            Layer = CollisionLayer.Player,
        };

        return CollisionQueries.CheckPlayerCollision(
                _world,
                _tileMap,
                Entity.Null,
                position,
                in collider)
            == PlayerCollisionResult.None;
    }

    public void Tick(float deltaSeconds)
    {
        // Do not clear the audio buffer here — network handlers may queue sounds before Tick,
        // and multi-tick Updates should keep every event until ConsumeSoundEvents drains them.
        TankStatusSystem.Update(_world, deltaSeconds);
        CityAlertSystem.Update(_world, deltaSeconds);
        CityOrbedNotificationSystem.Update(_world, deltaSeconds);
        ResearchCompleteNotificationSystem.Update(_world, deltaSeconds);
        InputSystem.Update(_world, deltaSeconds);
        BotAiSystem.UpdateMovement(_world, deltaSeconds);
        MovementSystem.UpdateNonBullets(_world, deltaSeconds);
        AdvanceAllWeaponTimers(deltaSeconds);
        WeaponSystem.Update(_world, deltaSeconds, TryResolveCityBuildForPlayer, _audioBuffer, ReportLocalShot);
        BotAiSystem.UpdateFiring(_world, deltaSeconds, _audioBuffer);
        ItemDropSystem.Update(_world, _tileMap, _audioBuffer, SuppressLocalItemDrops, TryResolveCityBuildForPlayer);
        BombSystem.Update(_world, deltaSeconds, _audioBuffer, CreateBombSimulationHooks());
        ItemAnimationSystem.Update(_world, deltaSeconds);
        BuildingPopulationSystem.Update(_world, deltaSeconds);
        BuildingAnimationSystem.Update(_world, deltaSeconds);
        ResearchSystem.UpdateAll(_world, _cityBuilds.Values, deltaSeconds, _audioBuffer);
        if (!SuppressLocalFactoryProduction)
        {
            FactoryProductionSystem.UpdateAll(
                _world,
                _cityBuilds.Values,
                deltaSeconds,
                allocateNetworkItemId: ReportFactoryItemSpawnsToNetwork ? AllocateNetworkItemId : null,
                reportSpawn: ReportFactoryItemSpawnsToNetwork
                    ? packet => _pendingFactoryAddItems.Add(packet)
                    : null);
        }
        TurretAiSystem.Update(_world, deltaSeconds, _audioBuffer);
        BulletSystem.PrepareMovement(_world, deltaSeconds);
        MovementSystem.UpdateBullets(_world, deltaSeconds);
        BulletSystem.UpdateAfterMovement(_world, deltaSeconds);
        BulletCollisionSystem.Resolve(
            _world,
            _tileMap,
            _audioBuffer,
            QueueNetworkPlayerHpIfChanged,
            applyDamageToNetworkPlayers: !NetworkPlayersUseLocalBulletDamage,
            defendedCityId: _cityBuild?.CityId ?? 0);
        CombatLifeSystem.Update(
            _world,
            deltaSeconds,
            _audioBuffer,
            NetworkPlayersUseLocalHealthDeath,
            QueueNetworkPlayerDeath,
            CreateCombatLifeSimulationHooks());
        ProcessNetworkPlayerRespawns();
        CollisionSystem.Resolve(_world, _tileMap);
        MineSystem.Update(_world, _audioBuffer);
        DfgSystem.Update(_world, _audioBuffer);
        ExplosionSystem.Update(_world, deltaSeconds);

        if (!SuppressLocalOrbEffects
            && _cityBuilds.Count > 0
            && OrbSystem.TryTrigger(_world, _cityBuilds.Values, out var victimCityId, out var attackerCityId))
        {
            EnsureCityBuild(attackerCityId);
            var victimBuild = EnsureCityBuild(victimCityId);
            var attackerBuild = EnsureCityBuild(attackerCityId);

            // Legacy didOrb: increment orber Orbs first, then read both city values.
            attackerBuild.Orbs++;
            var victimPoints = (uint)Math.Max(0, victimBuild.GetOrbValue());
            var attackerPoints = (uint)Math.Max(0, attackerBuild.GetOrbValue());

            _pendingOrbEvent = new OrbEvent(
                victimCityId,
                attackerCityId,
                victimPoints,
                attackerPoints);

            ApplyOrbStrike(victimCityId, attackerCityId);
        }
    }

    public bool TryConsumeOrbEvent(out OrbEvent orbEvent)
    {
        if (_pendingOrbEvent is not { } pending)
        {
            orbEvent = default;
            return false;
        }

        orbEvent = pending;
        _pendingOrbEvent = null;
        return true;
    }

    public bool TryConsumeNetworkDeathEvent(out ServerDeathPacket deathEvent)
    {
        if (_pendingDeathEvents.Count == 0)
        {
            deathEvent = default;
            return false;
        }

        deathEvent = _pendingDeathEvents[0];
        _pendingDeathEvents.RemoveAt(0);
        return true;
    }

    public bool TryConsumeNetworkHpEvent(out ServerHpPacket hpEvent)
    {
        if (_pendingHpEvents.Count == 0)
        {
            hpEvent = default;
            return false;
        }

        hpEvent = _pendingHpEvents[0];
        _pendingHpEvents.RemoveAt(0);
        return true;
    }

    public bool TryConsumeNetworkExplosionEvent(out PendingExplosionEvent explosionEvent)
    {
        if (_pendingExplosionEvents.Count == 0)
        {
            explosionEvent = default;
            return false;
        }

        explosionEvent = _pendingExplosionEvents[0];
        _pendingExplosionEvents.RemoveAt(0);
        return true;
    }

    public bool TryConsumeNetworkRespawnEvent(out PendingRespawnEvent respawnEvent)
    {
        if (_pendingRespawnEvents.Count == 0)
        {
            respawnEvent = default;
            return false;
        }

        respawnEvent = _pendingRespawnEvents[0];
        _pendingRespawnEvents.RemoveAt(0);
        return true;
    }

    public bool TryConsumeFactoryAddItem(out ServerAddItemPacket addItem)
    {
        if (_pendingFactoryAddItems.Count == 0)
        {
            addItem = default;
            return false;
        }

        addItem = _pendingFactoryAddItems[0];
        _pendingFactoryAddItems.RemoveAt(0);
        return true;
    }

    public void ApplyNetworkExplosion(in ServerExplosionPacket packet)
    {
        var center = new Vector2(
            (packet.GridX - 1) * GameConstants.TileSize + GameConstants.TileSize / 2f,
            (packet.GridY - 1) * GameConstants.TileSize + GameConstants.TileSize / 2f);
        GameplayEntityFactory.CreateExplosion(_world, ExplosionKind.Large, center);
        _audioBuffer.Play(SoundId.Explode, center);
    }

    public void ApplyPlayerDeath(in ServerDeathPacket packet, byte localPlayerId)
    {
        if (packet.PlayerId == localPlayerId)
        {
            ApplyDeathToLocalPlayer(packet.KillerCity, syncRespawnTimerWithServer: true);
            return;
        }

        ApplyNetworkDeath(packet);
    }

    public void ApplyNetworkWarp(in ServerStateGamePacket packet)
    {
        if (!TryGetLocalPlayerEntity(out var entity))
        {
            return;
        }

        ApplyRespawnState(
            entity,
            new Vector2(packet.X, packet.Y),
            packet.City,
            playWarpAudio: true);
    }

    public void ApplyNetworkRespawn(byte playerId)
    {
        if (!TryGetNetworkPlayerEntity(playerId, out var entity))
        {
            return;
        }

        ApplyRespawnState(
            entity,
            Vector2.Zero,
            (byte)Math.Clamp(_world.Get<CityAffiliation>(entity).CityId, 0, byte.MaxValue),
            playWarpAudio: false);
    }

    public bool TryGetCityRespawnPosition(int cityId, out Vector2 position, out byte resolvedCityId)
    {
        resolvedCityId = (byte)Math.Clamp(cityId, 0, byte.MaxValue);

        // Prefer the CC for the player's city on the shared world map (legacy 63→0 scan).
        if (_tileMap is not null
            && CityBuildInitializer.TryGetCommandCenterGridForCity(
                cityId,
                _tileMap,
                out var gridX,
                out var gridY))
        {
            if (!CommandCenterLookup.TryGetRespawnPosition(_world, gridX, gridY, out position))
            {
                position = CommandCenterLookup.GetRespawnPositionFromGridAnchor(gridX, gridY);
            }

            position = FindOpenTankSpawnNear(position);
            return true;
        }

        if (_cityBuild is not null)
        {
            if (!TryGetCityRespawnPositionFromWorld(out position))
            {
                position = CommandCenterLookup.GetRespawnPositionFromGridAnchor(
                    _cityBuild.CommandCenterGridX,
                    _cityBuild.CommandCenterGridY);
            }

            position = FindOpenTankSpawnNear(position);
            return true;
        }

        position = _loadedCity?.GetSpawnPosition() ?? Vector2.Zero;
        if (position != Vector2.Zero)
        {
            position = FindOpenTankSpawnNear(position);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Prefer <paramref name="preferred"/> when clear; otherwise spiral outward on the tile grid
    /// so joins/respawns are not trapped inside walls or other blockers on the CC pad.
    /// </summary>
    public Vector2 FindOpenTankSpawnNear(Vector2 preferred)
    {
        var snapped = SnapTankToTile(preferred);
        if (IsOpenTankSpawn(snapped))
        {
            return snapped;
        }

        var tile = GameConstants.TileSize;
        for (var radius = 1; radius <= 32; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                    {
                        continue;
                    }

                    var probe = SnapTankToTile(preferred + new Vector2(dx * tile, dy * tile));
                    if (IsOpenTankSpawn(probe))
                    {
                        return probe;
                    }
                }
            }
        }

        return snapped;
    }

    private bool TryGetCityRespawnPositionFromWorld(out Vector2 position) =>
        _cityBuild is not null
            ? CommandCenterLookup.TryGetRespawnPosition(
                _world,
                _cityBuild.CommandCenterGridX,
                _cityBuild.CommandCenterGridY,
                out position)
            : CommandCenterLookup.TryGetRespawnPosition(_world, out position);

    public void ApplyNetworkHp(in ServerHpPacket packet, byte localPlayerId = byte.MaxValue)
    {
        // Local online tanks use InputControlled without NetworkIdentity; resolve like cloak.
        if (!TryResolvePlayerEntityForNetworkEvent(packet.PlayerId, localPlayerId, out var entity)
            || !_world.Has<Health>(entity))
        {
            return;
        }

        ref var health = ref _world.Get<Health>(entity);
        health.Current = Math.Clamp(packet.Health, 0, health.Max);
    }

    public bool TryApplyDeathForNetworkPlayer(byte playerId, byte killerCity, out ServerDeathPacket broadcast)
    {
        broadcast = default;
        if (!TryGetNetworkPlayerEntity(playerId, out var entity))
        {
            return false;
        }

        if (!ApplyDeathState(entity, killerCity, playEffects: true))
        {
            return false;
        }

        broadcast = new ServerDeathPacket(playerId, deathType: 0, killerCity);
        return true;
    }

    public void ApplyNetworkDeath(in ServerDeathPacket packet)
    {
        if (!TryGetNetworkPlayerEntity(packet.PlayerId, out var entity))
        {
            return;
        }

        ApplyDeathState(entity, packet.KillerCity, playEffects: true);
    }

    public void ApplyNetworkOrb(byte victimCityId, byte attackerCityId)
    {
        if (!TryGetCityBuild(victimCityId, out _) && _cityBuilds.Count == 0)
        {
            return;
        }

        var attackerBuild = EnsureCityBuild(attackerCityId);
        attackerBuild.Orbs++;
        ApplyOrbStrike(victimCityId, attackerCityId);
    }

    public bool TryDropItemForNetworkPlayer(byte playerId, ItemType type, bool active, out ServerAddItemPacket packet)
    {
        packet = default;

        if (!TryGetNetworkPlayerEntity(playerId, out var entity)
            || !_world.Has<PlayerInventory>(entity))
        {
            return false;
        }

        ref var inventory = ref _world.Get<PlayerInventory>(entity);
        if (!ItemCatalog.IsPlaceable(type) || inventory.GetCount(type) <= 0)
        {
            return false;
        }

        ref var transform = ref _world.Get<Transform2D>(entity);
        var itemId = AllocateNetworkItemId();

        if (!ItemDropActions.TryDropForEntity(
                _world,
                entity,
                transform.Position,
                type,
                active,
                out var gridX,
                out var gridY,
                itemId,
                _cityBuild,
                _tileMap))
        {
            return false;
        }

        if (!inventory.TryConsume(type))
        {
            return false;
        }

        inventory.SelectNextAvailablePlaceable();

        var cityId = _world.Has<CityAffiliation>(entity)
            ? _world.Get<CityAffiliation>(entity).CityId
            : 0;

        packet = new ServerAddItemPacket(
            (ushort)gridX,
            (ushort)gridY,
            (byte)cityId,
            (byte)type,
            (byte)(active ? 1 : 0),
            itemId);

        _audioBuffer.Play(SoundId.Click, transform.Position);
        return true;
    }

    public bool TryUseMedKitForNetworkPlayer(byte playerId, out ServerHpPacket hpPacket)
    {
        hpPacket = default;

        if (!TryGetNetworkPlayerEntity(playerId, out var entity)
            || !_world.Has<PlayerInventory>(entity)
            || !_world.Has<Health>(entity)
            || !_world.Has<TankLifeState>(entity)
            || !_world.Has<TankStatus>(entity))
        {
            return false;
        }

        ref var life = ref _world.Get<TankLifeState>(entity);
        if (life.IsDead)
        {
            return false;
        }

        ref var status = ref _world.Get<TankStatus>(entity);
        if (status.IsFrozen)
        {
            return false;
        }

        ref var inventory = ref _world.Get<PlayerInventory>(entity);
        if (inventory.GetCount(ItemType.MedKit) <= 0 || !inventory.TryConsume(ItemType.MedKit))
        {
            return false;
        }

        ref var health = ref _world.Get<Health>(entity);
        health.Current = health.Max;
        hpPacket = new ServerHpPacket(playerId, (byte)health.Max);

        ref var transform = ref _world.Get<Transform2D>(entity);
        _audioBuffer.Play(SoundId.Click, transform.Position);
        return true;
    }

    public bool TryUseCloakForNetworkPlayer(byte playerId)
    {
        if (!TryGetNetworkPlayerEntity(playerId, out var entity)
            || !_world.Has<PlayerInventory>(entity)
            || !_world.Has<TankLifeState>(entity)
            || !_world.Has<TankStatus>(entity))
        {
            return false;
        }

        ref var life = ref _world.Get<TankLifeState>(entity);
        if (life.IsDead)
        {
            return false;
        }

        ref var status = ref _world.Get<TankStatus>(entity);
        if (status.IsFrozen)
        {
            return false;
        }

        ref var inventory = ref _world.Get<PlayerInventory>(entity);
        if (!_world.Has<WeaponState>(entity))
        {
            return false;
        }

        ref var weapons = ref _world.Get<WeaponState>(entity);
        var cityBuild = TryResolveCityBuildForPlayer(entity);
        if (!WeaponActions.TryConsumeCloak(ref weapons, ref inventory, cityBuild))
        {
            return false;
        }

        TankStatusSystem.ActivateCloak(ref status);

        ref var transform = ref _world.Get<Transform2D>(entity);
        _audioBuffer.Play(SoundId.Cloak, transform.Position);
        return true;
    }

    public bool ApplyNetworkCloak(byte playerId, byte localPlayerId)
    {
        if (!TryResolvePlayerEntityForNetworkEvent(playerId, localPlayerId, out var entity)
            || !_world.Has<TankStatus>(entity))
        {
            return false;
        }

        ref var status = ref _world.Get<TankStatus>(entity);
        TankStatusSystem.ActivateCloak(ref status);

        // Mirror server recharge when the local player cloaks with city-unlocked equipment.
        if (playerId == localPlayerId
            && _world.Has<WeaponState>(entity)
            && _world.Has<PlayerInventory>(entity))
        {
            ref var weapons = ref _world.Get<WeaponState>(entity);
            ref var inventory = ref _world.Get<PlayerInventory>(entity);
            var cityBuild = TryResolveCityBuildForPlayer(entity);
            if (CityEquipmentRules.HasRechargeableCloak(cityBuild))
            {
                weapons.CloakRechargeSeconds = EconomyConstants.AbilityRechargeSeconds;
            }
            else
            {
                inventory.TryConsume(ItemType.Cloak);
            }
        }

        ref var transform = ref _world.Get<Transform2D>(entity);
        _audioBuffer.Play(SoundId.Cloak, transform.Position);
        return true;
    }

    /// <summary>True when local player may request cloak (inventory or recharge-ready).</summary>
    public bool CanLocalPlayerCloak()
    {
        var query = new QueryDescription().WithAll<InputControlled, PlayerInventory, WeaponState, TankLifeState>();
        var canCloak = false;
        _world.Query(
            in query,
            (Entity entity, ref PlayerInventory inventory, ref WeaponState weapons, ref TankLifeState life) =>
            {
                if (life.IsDead)
                {
                    return;
                }

                var cityBuild = TryResolveCityBuildForPlayer(entity);
                if (CityEquipmentRules.HasRechargeableCloak(cityBuild))
                {
                    canCloak = weapons.CloakRechargeSeconds <= 0f;
                    return;
                }

                canCloak = inventory.GetCount(ItemType.Cloak) > 0;
            });

        return canCloak;
    }

    private bool TryResolvePlayerEntityForNetworkEvent(byte playerId, byte localPlayerId, out Entity entity)
    {
        entity = default;

        if (playerId == localPlayerId)
        {
            var query = new QueryDescription().WithAll<InputControlled>();
            Entity resolved = default;
            var found = false;
            _world.Query(
                in query,
                (Entity candidate) =>
                {
                    if (!found)
                    {
                        resolved = candidate;
                        found = true;
                    }
                });
            entity = resolved;
            return found;
        }

        return TryGetNetworkPlayerEntity(playerId, out entity);
    }

    public void ApplyNetworkAddItem(in ServerAddItemPacket packet)
    {
        if (TryFindNetworkItem(packet.Id, out _))
        {
            return;
        }

        if (!Enum.IsDefined(typeof(ItemType), (int)packet.Type))
        {
            return;
        }

        var itemType = (ItemType)packet.Type;
        if (TryFindPredictedItemAtGrid(packet.X, packet.Y, itemType, out var predictedEntity)
            || TryFindPredictedItemNear(packet.X, packet.Y, itemType, maxChebyshev: 2, out predictedEntity))
        {
            AttachNetworkIdToPredictedItem(predictedEntity, packet);
            return;
        }

        GameplayEntityFactory.CreatePlacedItem(
            _world,
            itemType,
            packet.X,
            packet.Y,
            packet.Active != 0,
            cityId: packet.City,
            networkItemId: packet.Id);
    }

    private void AttachNetworkIdToPredictedItem(Entity predictedEntity, in ServerAddItemPacket packet)
    {
        if (!_world.Has<NetworkItemRef>(predictedEntity))
        {
            _world.Add(predictedEntity, new NetworkItemRef { ItemId = packet.Id });
        }

        ref var predictedItem = ref _world.Get<PlacedItemRef>(predictedEntity);
        predictedItem.GridX = packet.X;
        predictedItem.GridY = packet.Y;
        predictedItem.Active = packet.Active != 0;
        predictedItem.CityId = packet.City;

        var position = PlacedItemPlacement.GridToWorldPosition(packet.X, packet.Y);
        ref var transform = ref _world.Get<Transform2D>(predictedEntity);
        transform.Position = position;
        transform.PreviousPosition = position;
    }

    public bool TryFireShotForNetworkPlayer(byte playerId, in ClientShotPacket request, out ServerShotPacket broadcast)
    {
        broadcast = default;

        if (!TryGetNetworkPlayerEntity(playerId, out var entity))
        {
            return false;
        }

        ref var weapons = ref _world.Get<WeaponState>(entity);
        ref var inventory = ref _world.Get<PlayerInventory>(entity);
        ref var life = ref _world.Get<TankLifeState>(entity);
        ref var status = ref _world.Get<TankStatus>(entity);

        if (!WeaponActions.TryFireFromNetworkRequest(
                _world,
                entity,
                ref weapons,
                ref inventory,
                ref life,
                ref status,
                request,
                _cityBuild,
                _audioBuffer))
        {
            return false;
        }

        broadcast = new ServerShotPacket(
            playerId,
            request.X,
            request.Y,
            request.Direction,
            request.Type);
        return true;
    }

    public void ApplyNetworkShot(in ServerShotPacket packet)
    {
        if (!TryGetNetworkPlayerEntity((byte)packet.PlayerId, out var entity))
        {
            return;
        }

        WeaponActions.ApplyNetworkShot(_world, entity, packet, _audioBuffer);
    }

    public bool TryPickupItemForNetworkPlayer(
        byte playerId,
        in ClientItemPickupPacket request,
        out ServerRemoveItemPacket removePacket,
        out ServerPickedUpPacket pickedUpPacket)
    {
        removePacket = default;
        pickedUpPacket = default;

        if (!TryGetNetworkPlayerEntity(playerId, out var entity)
            || !_world.Has<PlayerInventory>(entity))
        {
            return false;
        }

        ref var inventory = ref _world.Get<PlayerInventory>(entity);
        ref var life = ref _world.Get<TankLifeState>(entity);
        if (life.IsDead)
        {
            return false;
        }

        Entity itemEntity;
        ItemType itemType;
        if (request.ItemId != 0)
        {
            if (!ItemPickupActions.TryFindNetworkItem(_world, request.ItemId, out itemEntity, out itemType))
            {
                return false;
            }
        }
        else if (!ItemPickupActions.TryFindItemAtTank(
                     _world,
                     entity,
                     out itemEntity,
                     out itemType,
                     out _))
        {
            return false;
        }

        var playerCityId = _world.Has<CityAffiliation>(entity)
            ? _world.Get<CityAffiliation>(entity).CityId
            : 0;
        if (!_world.Has<PlacedItemRef>(itemEntity)
            || _world.Get<PlacedItemRef>(itemEntity).CityId != playerCityId)
        {
            return false;
        }

        var networkItemId = request.ItemId;
        if (networkItemId == 0 && _world.Has<NetworkItemRef>(itemEntity))
        {
            networkItemId = _world.Get<NetworkItemRef>(itemEntity).ItemId;
        }

        if (!ItemPickupActions.TryPickUp(_world, entity, ref inventory, itemEntity, itemType))
        {
            return false;
        }

        if (networkItemId == 0)
        {
            networkItemId = AllocateNetworkItemId();
        }

        removePacket = new ServerRemoveItemPacket(networkItemId);
        pickedUpPacket = new ServerPickedUpPacket(
            networkItemId,
            request.Active,
            (byte)itemType);
        _audioBuffer.Play(SoundId.Click, _world.Get<Transform2D>(entity).Position);
        return true;
    }

    public void ApplyNetworkRemoveItem(in ServerRemoveItemPacket packet)
    {
        if (TryFindNetworkItem(packet.ItemId, out var entity))
        {
            _world.Destroy(entity);
        }
    }

    public void ApplyNetworkPickedUp(in ServerPickedUpPacket packet)
    {
        if (!Enum.IsDefined(typeof(ItemType), (int)packet.ItemType))
        {
            return;
        }

        var itemType = (ItemType)packet.ItemType;
        var query = new QueryDescription().WithAll<InputControlled, PlayerInventory>();
        _world.Query(
            in query,
            (ref PlayerInventory inventory) => inventory.TryAdd(itemType));
    }

    public bool TryBuildForNetworkPlayer(byte playerId, in ClientBuildPacket request, out ServerBuildingPacket broadcast)
    {
        broadcast = default;

        if (!TryGetNetworkPlayerEntity(playerId, out var entity)
            || _world.Get<TankLifeState>(entity).IsDead)
        {
            return false;
        }

        var cityId = _world.Has<CityAffiliation>(entity)
            ? _world.Get<CityAffiliation>(entity).CityId
            : 0;

        ref var transform = ref _world.Get<Transform2D>(entity);
        var playerCenter = transform.Position + new Vector2(GameConstants.TileSize / 2f, GameConstants.TileSize / 2f);

        if (!TryPlaceBuilding(cityId, request.BuildSlot, request.X, request.Y, playerCenter))
        {
            return false;
        }

        if (!BuildingPlacementValidator.TryFindBuildingAt(_world, request.X, request.Y, out var buildingEntity))
        {
            return false;
        }

        ref var building = ref _world.Get<BuildingRef>(buildingEntity);
        building.CityId = cityId;
        var population = _world.Has<BuildingState>(buildingEntity)
            ? (byte)Math.Clamp(_world.Get<BuildingState>(buildingEntity).Population, 0, byte.MaxValue)
            : (byte)0;

        broadcast = new ServerBuildingPacket(
            (byte)cityId,
            request.X,
            request.Y,
            request.BuildSlot,
            count: 0,
            building.NetworkId,
            population);

        _audioBuffer.Play(SoundId.Build, transform.Position);
        return true;
    }

    public bool TryDemolishForNetworkPlayer(byte playerId, in ClientDemolishPacket request, out ServerBuildingPacket broadcast)
    {
        broadcast = default;

        if (!TryGetNetworkPlayerEntity(playerId, out var entity)
            || _world.Get<TankLifeState>(entity).IsDead
            || request.BuildingId == 0)
        {
            return false;
        }

        if (!BuildingCommandService.TryFindBuildingByNetworkId(_world, request.BuildingId, out var buildingEntity))
        {
            return false;
        }

        ref var building = ref _world.Get<BuildingRef>(buildingEntity);
        if (BuildingCatalog.IsCommandCenter(building.TypeCode))
        {
            return false;
        }

        var playerCityId = _world.Has<CityAffiliation>(entity)
            ? _world.Get<CityAffiliation>(entity).CityId
            : 0;
        // CityId 0 on legacy buildings: only city 0 may demolish them.
        if (building.CityId != playerCityId)
        {
            return false;
        }

        var build = EnsureCityBuild(playerCityId);
        var population = _world.Has<BuildingState>(buildingEntity)
            ? (byte)Math.Clamp(_world.Get<BuildingState>(buildingEntity).Population, 0, byte.MaxValue)
            : (byte)0;

        broadcast = new ServerBuildingPacket(
            (byte)playerCityId,
            (ushort)building.GridAnchorX,
            (ushort)building.GridAnchorY,
            (byte)(building.MenuIndex + 1),
            count: 0,
            building.NetworkId,
            population);

        if (!BuildingCommandService.TryDemolishByNetworkId(_world, build, request.BuildingId))
        {
            return false;
        }

        _removedBuildingSnapshots.Add(broadcast);
        _audioBuffer.Play(SoundId.Click, _world.Get<Transform2D>(entity).Position);
        return true;
    }

    public void CollectJoinSnapshot(JoinWorldSnapshot snapshot)
    {
        snapshot.Items.Clear();
        snapshot.Buildings.Clear();
        snapshot.RemovedBuildings.Clear();
        snapshot.RemovedBuildings.AddRange(_removedBuildingSnapshots);

        var itemQuery = new QueryDescription().WithAll<PlacedItemRef, NetworkItemRef>();
        _world.Query(
            in itemQuery,
            (Entity entity, ref PlacedItemRef item, ref NetworkItemRef networkItem) =>
            {
                snapshot.Items.Add(new ServerAddItemPacket(
                    (ushort)item.GridX,
                    (ushort)item.GridY,
                    (byte)item.CityId,
                    (byte)item.Type,
                    (byte)(item.Active ? 1 : 0),
                    networkItem.ItemId));
            });

        var cityId = _cityBuild?.CityId ?? 0;
        var buildingQuery = new QueryDescription().WithAll<BuildingRef>();
        _world.Query(
            in buildingQuery,
            (Entity entity, ref BuildingRef building) =>
            {
                if (building.NetworkId == 0)
                {
                    return;
                }

                var population = _world.Has<BuildingState>(entity)
                    ? (byte)Math.Clamp(_world.Get<BuildingState>(entity).Population, 0, byte.MaxValue)
                    : (byte)0;

                snapshot.Buildings.Add(new ServerBuildingPacket(
                    (byte)Math.Clamp(building.CityId, 0, byte.MaxValue),
                    (ushort)building.GridAnchorX,
                    (ushort)building.GridAnchorY,
                    (byte)Math.Max(0, building.MenuIndex + 1),
                    count: 0,
                    building.NetworkId,
                    population));
            });
    }

    public void ApplyNetworkNewBuilding(in ServerBuildingPacket packet)
    {
        if (BuildingCommandService.TryFindBuildingByNetworkId(_world, packet.Id, out _))
        {
            return;
        }

        if (BuildingPlacementValidator.TryFindBuildingAt(_world, packet.X, packet.Y, out var existing))
        {
            ref var existingBuilding = ref _world.Get<BuildingRef>(existing);
            existingBuilding.NetworkId = packet.Id;
            existingBuilding.CityId = packet.City;
            return;
        }

        var cityId = packet.City;
        if (!TryPlaceBuilding(cityId, packet.BuildSlot, packet.X, packet.Y))
        {
            var menuIndex = packet.BuildSlot - 1;
            if (menuIndex < 0 || menuIndex >= BuildingCatalog.MenuTypeCodes.Count)
            {
                return;
            }

            var typeCode = BuildingCatalog.MenuTypeCodes[menuIndex];
            var placement = new CityBuildingPlacement(menuIndex, packet.X, packet.Y, typeCode);
            LevelLoader.SpawnBuilding(_world, placement, cityId);
            var build = EnsureCityBuild(cityId);
            build.RegisterBuildingPlaced(menuIndex, typeCode);
        }

        if (BuildingPlacementValidator.TryFindBuildingAt(_world, packet.X, packet.Y, out var buildingEntity))
        {
            ref var building = ref _world.Get<BuildingRef>(buildingEntity);
            building.NetworkId = packet.Id;
            building.CityId = cityId;
            if (_world.Has<BuildingState>(buildingEntity))
            {
                ref var state = ref _world.Get<BuildingState>(buildingEntity);
                state.Population = packet.Population;
            }
        }
    }

    public void ApplyNetworkRemoveBuilding(in ServerBuildingPacket packet)
    {
        var cityId = packet.City;
        if (!TryGetCityBuild(cityId, out var build))
        {
            build = EnsureCityBuild(cityId);
        }

        if (packet.Id != 0)
        {
            BuildingCommandService.TryDemolishByNetworkId(_world, build, packet.Id);
        }
        else
        {
            BuildingCommandService.TryDemolishAt(_world, build, packet.X, packet.Y);
        }

        // Legacy ProcessRemBuilding: own-city removals raise the under-attack alert.
        if (TryGetPlayerCityId(out var localCityId) && packet.City == localCityId)
        {
            ApplyNetworkUnderAttack();
        }
    }

    /// <summary>Legacy <c>smUnderAttack</c> — flash the home-city under-attack HUD for ~3s.</summary>
    public void ApplyNetworkUnderAttack()
    {
        var cityId = TryGetPlayerCityId(out var playerCityId)
            ? playerCityId
            : _cityBuild?.CityId ?? 0;
        CityAlertSystem.TriggerForCity(_world, cityId);
    }

    public void ApplyNetworkCanBuild(in ServerCanBuildPacket packet)
    {
        CityBuildState build;
        if (TryGetPlayerCityId(out var playerCityId))
        {
            build = EnsureCityBuild(playerCityId);
        }
        else if (_cityBuild is not null)
        {
            build = _cityBuild;
        }
        else
        {
            return;
        }

        var menuIndex = packet.BuildSlot - 1;
        if (menuIndex < 0 || menuIndex >= build.CanBuild.Length)
        {
            return;
        }

        build.CanBuild[menuIndex] = packet.CanBuildState;
    }

    public void ApplyNetworkUpdatePop(in ServerUpdatePopPacket packet)
    {
        if (!BuildingCommandService.TryFindBuildingByNetworkId(_world, packet.BuildingId, out var entity)
            || !_world.Has<BuildingState>(entity))
        {
            return;
        }

        ref var state = ref _world.Get<BuildingState>(entity);
        state.Population = packet.Population;
    }

    public IEnumerable<(ushort BuildingId, byte Population)> CollectBuildingPopulations()
    {
        var query = new QueryDescription().WithAll<BuildingRef, BuildingState>();
        var results = new List<(ushort, byte)>();

        _world.Query(
            in query,
            (ref BuildingRef building, ref BuildingState state) =>
            {
                if (building.NetworkId == 0)
                {
                    return;
                }

                results.Add((
                    building.NetworkId,
                    (byte)Math.Clamp(state.Population, 0, byte.MaxValue)));
            });

        return results;
    }

    public IEnumerable<(ushort BuildingId, byte ItemCount)> CollectFactoryItemCounts()
    {
        var query = new QueryDescription().WithAll<BuildingRef, BuildingState>();
        var results = new List<(ushort, byte)>();

        _world.Query(
            in query,
            (ref BuildingRef building, ref BuildingState state) =>
            {
                if (building.NetworkId == 0 || !BuildingCatalog.IsFactory(building.TypeCode))
                {
                    return;
                }

                results.Add((
                    building.NetworkId,
                    (byte)Math.Clamp(state.ItemsLeft, 0, byte.MaxValue)));
            });

        return results;
    }

    public void ApplyNetworkItemCount(in ServerItemCountPacket packet)
    {
        if (!BuildingCommandService.TryFindBuildingByNetworkId(_world, packet.BuildingId, out var entity)
            || !_world.Has<BuildingState>(entity))
        {
            return;
        }

        ref var state = ref _world.Get<BuildingState>(entity);
        state.ItemsLeft = packet.ItemCount;
    }

    public bool TryGetBuildingNetworkIdAt(int gridAnchorX, int gridAnchorY, out ushort networkId) =>
        BuildingCommandService.TryFindBuildingNetworkIdAt(_world, gridAnchorX, gridAnchorY, out networkId);

    public bool TryFindPickupItemAtLocalPlayer(out ushort networkItemId, out byte itemType, out byte active)
    {
        networkItemId = 0;
        itemType = 0;
        active = 0;

        var query = new QueryDescription().WithAll<InputControlled>();
        var found = false;
        Entity player = Entity.Null;

        _world.Query(
            in query,
            (Entity entity) =>
            {
                if (!found)
                {
                    player = entity;
                    found = true;
                }
            });

        if (!found
            || !ItemPickupActions.TryFindItemAtTank(
                _world,
                player,
                out _,
                out var itemTypeValue,
                out networkItemId))
        {
            return false;
        }

        itemType = (byte)itemTypeValue;
        active = 1;
        return true;
    }

    public void AssignNetworkItemIds()
    {
        var query = new QueryDescription().WithAll<PlacedItemRef>();
        _world.Query(
            in query,
            (Entity entity, ref PlacedItemRef _) =>
            {
                if (_world.Has<NetworkItemRef>(entity))
                {
                    return;
                }

                _world.Add(entity, new NetworkItemRef { ItemId = AllocateNetworkItemId() });
            });
    }

    public bool TryGetPlayerInventory(out PlayerInventory inventory)
    {
        var query = new QueryDescription().WithAll<InputControlled, PlayerInventory>();
        var found = false;
        var value = default(PlayerInventory);

        _world.Query(
            in query,
            (ref PlayerInventory playerInventory) =>
            {
                value = playerInventory;
                found = true;
            });

        inventory = value;
        return found;
    }

    public bool TryConsumeLocalPlayerItem(ItemType type)
    {
        var query = new QueryDescription().WithAll<InputControlled, PlayerInventory>();
        var consumed = false;

        _world.Query(
            in query,
            (ref PlayerInventory inventory) =>
            {
                if (!consumed)
                {
                    consumed = inventory.TryConsume(type);
                    if (consumed)
                    {
                        inventory.SelectNextAvailablePlaceable();
                    }
                }
            });

        return consumed;
    }

    public bool TryPredictLocalItemDrop(ItemType type, bool active)
    {
        Entity player = default;
        var foundPlayer = false;

        var playerQuery = new QueryDescription().WithAll<InputControlled, Transform2D>();
        _world.Query(
            in playerQuery,
            (Entity entity, ref Transform2D _) =>
            {
                if (!foundPlayer)
                {
                    player = entity;
                    foundPlayer = true;
                }
            });

        if (!foundPlayer)
        {
            return false;
        }

        ref var transform = ref _world.Get<Transform2D>(player);
        return ItemDropActions.TryDropForEntity(
            _world,
            player,
            transform.Position,
            type,
            active,
            out _,
            out _,
            cityBuild: _cityBuild,
            tileMap: _tileMap);
    }

    private CombatLifeSimulationHooks CreateCombatLifeSimulationHooks() =>
        new()
        {
            SuppressLocalPlayerRespawn = SuppressLocalPlayerRespawn,
            DeferNetworkPlayerRespawn = ReportRespawnEventsToNetwork || DeferRemotePlayerRespawn,
            OnTankDied = ReturnPlaceablesToFactoriesOnDeath,
            ResolveRespawnPosition = ResolveEntityRespawnPosition,
        };

    private Vector2? ResolveEntityRespawnPosition(Entity entity)
    {
        var cityId = _world.Has<CityAffiliation>(entity)
            ? _world.Get<CityAffiliation>(entity).CityId
            : _cityBuild?.CityId ?? 0;

        if (!TryGetCityRespawnPosition(cityId, out var position, out _))
        {
            return null;
        }

        return position;
    }

    private void ProcessNetworkPlayerRespawns()
    {
        if (!ReportRespawnEventsToNetwork)
        {
            return;
        }

        var query = new QueryDescription()
            .WithAll<NetworkIdentity, TankLifeState, Health, Transform2D, CityAffiliation>();

        _world.Query(
            in query,
            (Entity entity, ref NetworkIdentity identity, ref TankLifeState life, ref CityAffiliation city) =>
            {
                if (!life.IsDead || life.RespawnTimerSeconds > 0f)
                {
                    return;
                }

                if (!TryGetCityRespawnPosition(city.CityId, out var position, out var resolvedCityId))
                {
                    return;
                }

                ApplyRespawnState(entity, position, resolvedCityId, playWarpAudio: false);
                _pendingRespawnEvents.Add(new PendingRespawnEvent(identity.PlayerId, position, resolvedCityId));
            });
    }

    private void QueueNetworkPlayerDeath(byte playerId, byte killerCity = EntityCityLookup.UnknownCity)
    {
        _pendingDeathEvents.Add(new ServerDeathPacket(playerId, deathType: 0, killerCity));
    }

    private bool ApplyDeathToLocalPlayer(byte killerCity, bool syncRespawnTimerWithServer = false)
    {
        _ = syncRespawnTimerWithServer;
        if (!TryGetLocalPlayerEntity(out var entity))
        {
            return false;
        }

        if (ApplyDeathState(entity, killerCity, playEffects: true))
        {
            return true;
        }

        // Already dead: do not re-anchor the countdown (was restarting the full 10s on smDeath).
        return false;
    }

    private bool TryGetLocalPlayerEntity(out Entity entity)
    {
        var query = new QueryDescription().WithAll<InputControlled>();
        var foundEntity = default(Entity);
        var found = false;

        _world.Query(
            in query,
            (Entity candidate) =>
            {
                if (!found)
                {
                    foundEntity = candidate;
                    found = true;
                }
            });

        entity = foundEntity;
        return found;
    }

    private void ApplyRespawnState(Entity entity, Vector2 position, byte cityId, bool playWarpAudio)
    {
        ref var life = ref _world.Get<TankLifeState>(entity);
        ref var health = ref _world.Get<Health>(entity);
        ref var transform = ref _world.Get<Transform2D>(entity);

        life.IsDead = false;
        life.KillerCityId = EntityCityLookup.UnknownCity;
        life.RespawnTimerSeconds = 0f;
        life.SpawnPosition = position;
        health.Current = health.Max;
        transform.Position = position;
        transform.PreviousPosition = position;

        if (_world.Has<Collider>(entity))
        {
            ref var collider = ref _world.Get<Collider>(entity);
            collider.Layer = CollisionLayer.Player;
        }

        if (_world.Has<Velocity>(entity))
        {
            ref var velocity = ref _world.Get<Velocity>(entity);
            velocity.Value = Vector2.Zero;
        }

        if (_world.Has<CityAffiliation>(entity))
        {
            ref var city = ref _world.Get<CityAffiliation>(entity);
            city.CityId = cityId;
        }

        if (playWarpAudio)
        {
            _audioBuffer.Play(
                SoundId.Click,
                position + new Vector2(GameConstants.TileSize / 2f, GameConstants.TileSize / 2f));
        }
    }

    private BombSimulationHooks CreateBombSimulationHooks()
    {
        if (!ReportBombEventsToNetwork && !SuppressLocalBombDetonation && _cityBuild is null)
        {
            return default;
        }

        return new BombSimulationHooks
        {
            SuppressDetonation = SuppressLocalBombDetonation,
            ReportExplosion = ReportBombEventsToNetwork ? QueueNetworkExplosion : null,
            ReportItemRemoved = ReportBombEventsToNetwork ? QueueNetworkBombItemRemoved : null,
            ReportHpChanged = ReportBombEventsToNetwork ? QueueNetworkPlayerHpIfChanged : null,
            ReportNetworkPlayerKilled = ReportBombEventsToNetwork ? ReportNetworkPlayerKilledByBomb : null,
            DestroyBuilding = _cityBuild is not null || ReportBombEventsToNetwork
                ? DestroyBuildingFromBomb
                : null,
        };
    }

    private void DestroyBuildingFromBomb(Entity entity)
    {
        if (!_world.IsAlive(entity) || !_world.Has<BuildingRef>(entity))
        {
            return;
        }

        ref var building = ref _world.Get<BuildingRef>(entity);
        var cityId = building.CityId;
        var population = _world.Has<BuildingState>(entity)
            ? (byte)Math.Clamp(_world.Get<BuildingState>(entity).Population, 0, byte.MaxValue)
            : (byte)0;
        var packet = new ServerBuildingPacket(
            (byte)Math.Clamp(cityId, 0, byte.MaxValue),
            (ushort)building.GridAnchorX,
            (ushort)building.GridAnchorY,
            (byte)Math.Max(0, building.MenuIndex + 1),
            count: 0,
            building.NetworkId,
            population);

        var build = EnsureCityBuild(cityId);
        if (building.NetworkId != 0)
        {
            BuildingCommandService.TryDemolishByNetworkId(_world, build, building.NetworkId);
        }
        else
        {
            BuildingCommandService.TryDemolishAt(
                _world,
                build,
                building.GridAnchorX,
                building.GridAnchorY);
        }

        if (!ReportBombEventsToNetwork)
        {
            return;
        }

        _pendingBombBuildingRemovals.Add(packet);
        _removedBuildingSnapshots.Add(packet);
    }

    public bool TryConsumeBombBuildingRemoval(out ServerBuildingPacket building)
    {
        if (_pendingBombBuildingRemovals.Count == 0)
        {
            building = default;
            return false;
        }

        building = _pendingBombBuildingRemovals[0];
        _pendingBombBuildingRemovals.RemoveAt(0);
        return true;
    }

    private void QueueNetworkExplosion(ServerExplosionPacket packet) =>
        _pendingExplosionEvents.Add(new PendingExplosionEvent(packet, RemovedItemId: 0));

    private void QueueNetworkBombItemRemoved(ushort itemId)
    {
        if (_pendingExplosionEvents.Count == 0)
        {
            return;
        }

        var last = _pendingExplosionEvents[^1];
        if (last.RemovedItemId != 0)
        {
            return;
        }

        _pendingExplosionEvents[^1] = last with { RemovedItemId = itemId };
    }

    private void ReportNetworkPlayerKilledByBomb(Entity entity, byte killerCity)
    {
        if (!ApplyDeathState(entity, killerCity, playEffects: true))
        {
            return;
        }

        var playerId = _world.Get<NetworkIdentity>(entity).PlayerId;
        QueueNetworkPlayerDeath(playerId, killerCity);
    }

    private void QueueNetworkPlayerHpIfChanged(Entity entity, int previousHealth, int currentHealth)
    {
        if (NetworkPlayersUseLocalBulletDamage
            || previousHealth == currentHealth
            || !_world.Has<NetworkIdentity>(entity)
            || _world.Has<InputControlled>(entity))
        {
            return;
        }

        var playerId = _world.Get<NetworkIdentity>(entity).PlayerId;
        _pendingHpEvents.Add(new ServerHpPacket(playerId, (byte)Math.Clamp(currentHealth, 0, byte.MaxValue)));
    }

    private bool ApplyDeathState(Entity entity, byte killerCity, bool playEffects)
    {
        ref var life = ref _world.Get<TankLifeState>(entity);
        if (life.IsDead)
        {
            return false;
        }

        ref var health = ref _world.Get<Health>(entity);
        ref var transform = ref _world.Get<Transform2D>(entity);
        if (_world.Has<Velocity>(entity))
        {
            ref var velocity = ref _world.Get<Velocity>(entity);
            velocity.Value = Vector2.Zero;
        }

        life.IsDead = true;
        life.RespawnTimerSeconds = GameConstants.TimerRespawn / 1000f;
        health.Current = 0;
        transform.PreviousPosition = transform.Position;

        if (_world.Has<Collider>(entity))
        {
            ref var collider = ref _world.Get<Collider>(entity);
            collider.Layer = CollisionLayer.None;
        }

        if (playEffects)
        {
            var center = transform.Position + new Vector2(GameConstants.TileSize / 2f, GameConstants.TileSize / 2f);
            GameplayEntityFactory.CreateExplosion(_world, ExplosionKind.Small, center);
            _audioBuffer.Play(SoundId.Die, center);
        }

        ReturnPlaceablesToFactoriesOnDeath(entity);
        return true;
    }

    private void ReturnPlaceablesToFactoriesOnDeath(Entity entity)
    {
        if (!_world.Has<PlayerInventory>(entity))
        {
            return;
        }

        ref var inventory = ref _world.Get<PlayerInventory>(entity);
        var cityId = _world.Has<CityAffiliation>(entity)
            ? _world.Get<CityAffiliation>(entity).CityId
            : _cityBuild?.CityId ?? 0;

        foreach (var type in PlayerInventory.SelectableItems)
        {
            if (!ItemCatalog.ReturnsToFactoryOnDeath(type))
            {
                continue;
            }

            while (inventory.GetCount(type) > 0)
            {
                // Online clients clear HUD stock without re-spawning — server already queues smAddItem.
                if (ReturnInventoryPlaceablesOnDeath)
                {
                    if (!FactoryProductionSystem.TryFindFactoryBayForProduct(_world, type, out var bayX, out var bayY))
                    {
                        // No matching factory — drop remaining stock as inactive map items near the tank.
                        if (!_world.Has<Transform2D>(entity))
                        {
                            break;
                        }

                        var topLeft = _world.Get<Transform2D>(entity).Position;
                        bayX = (int)(topLeft.X / GameConstants.TileSize);
                        bayY = (int)(topLeft.Y / GameConstants.TileSize);
                    }

                    var networkItemId = ReportFactoryItemSpawnsToNetwork ? AllocateNetworkItemId() : (ushort)0;
                    GameplayEntityFactory.CreatePlacedItem(
                        _world,
                        type,
                        bayX,
                        bayY,
                        active: false,
                        cityId: cityId,
                        networkItemId: networkItemId);

                    if (networkItemId != 0)
                    {
                        _pendingFactoryAddItems.Add(new ServerAddItemPacket(
                            (ushort)bayX,
                            (ushort)bayY,
                            (byte)Math.Clamp(cityId, 0, byte.MaxValue),
                            (byte)type,
                            active: 0,
                            networkItemId));
                    }
                }

                inventory.TryConsume(type);
            }
        }

        inventory.SelectNextAvailablePlaceable();
    }

    private void AdvanceAllWeaponTimers(float deltaSeconds)
    {
        var query = new QueryDescription().WithAll<WeaponState>();
        _world.Query(
            in query,
            (ref WeaponState weapons) => WeaponActions.AdvanceCooldowns(ref weapons, deltaSeconds));
    }

    private void AssignNetworkBuildingIds()
    {
        var query = new QueryDescription().WithAll<BuildingRef>();
        _world.Query(
            in query,
            (ref BuildingRef building) =>
            {
                if (building.NetworkId != 0)
                {
                    return;
                }

                building.NetworkId = AllocateNetworkBuildingId();
            });
    }

    private ushort AllocateNetworkBuildingId()
    {
        var id = _nextNetworkBuildingId++;
        if (_nextNetworkBuildingId == 0)
        {
            _nextNetworkBuildingId = 1;
        }

        return id;
    }

    private ushort AllocateNetworkItemId()
    {
        var id = _nextNetworkItemId++;
        if (_nextNetworkItemId == 0)
        {
            _nextNetworkItemId = 1;
        }

        return id;
    }

    private bool TryFindNetworkItem(ushort itemId, out Entity entity)
    {
        entity = Entity.Null;
        if (itemId == 0)
        {
            return false;
        }

        var found = false;
        var foundEntity = Entity.Null;
        var query = new QueryDescription().WithAll<NetworkItemRef>();
        _world.Query(
            in query,
            (Entity candidate, ref NetworkItemRef networkItem) =>
            {
                if (found || networkItem.ItemId != itemId)
                {
                    return;
                }

                foundEntity = candidate;
                found = true;
            });

        entity = foundEntity;
        return found;
    }

    private bool TryFindPredictedItemAtGrid(int gridX, int gridY, ItemType type, out Entity entity)
    {
        entity = Entity.Null;
        var found = false;
        var foundEntity = Entity.Null;
        var query = new QueryDescription().WithAll<PlacedItemRef>();

        _world.Query(
            in query,
            (Entity candidate, ref PlacedItemRef item) =>
            {
                if (found
                    || item.GridX != gridX
                    || item.GridY != gridY
                    || item.Type != type
                    || _world.Has<NetworkItemRef>(candidate))
                {
                    return;
                }

                foundEntity = candidate;
                found = true;
            });

        entity = foundEntity;
        return found;
    }

    /// <summary>
    /// Finds a locally predicted item (no network id) near the authoritative grid so lag
    /// between client prediction and server placement does not spawn a duplicate.
    /// </summary>
    private bool TryFindPredictedItemNear(int gridX, int gridY, ItemType type, int maxChebyshev, out Entity entity)
    {
        entity = Entity.Null;
        var bestDistance = int.MaxValue;
        var foundEntity = Entity.Null;
        var query = new QueryDescription().WithAll<PlacedItemRef>();

        _world.Query(
            in query,
            (Entity candidate, ref PlacedItemRef item) =>
            {
                if (item.Type != type || _world.Has<NetworkItemRef>(candidate))
                {
                    return;
                }

                var distance = Math.Max(Math.Abs(item.GridX - gridX), Math.Abs(item.GridY - gridY));
                if (distance > maxChebyshev || distance >= bestDistance)
                {
                    return;
                }

                bestDistance = distance;
                foundEntity = candidate;
            });

        entity = foundEntity;
        return bestDistance != int.MaxValue;
    }

    private void ApplyOrbStrike(int victimCityId, int attackerCityId)
    {
        var victimBuild = EnsureCityBuild(victimCityId);

        var ccCenter = CommandCenterLookup.TryGetWorldPosition(
                _world,
                victimBuild.CommandCenterGridX,
                victimBuild.CommandCenterGridY,
                out var ccPosition)
            ? ccPosition
            : CommandCenterLookup.GridAnchorToWorldCenter(
                victimBuild.CommandCenterGridX,
                victimBuild.CommandCenterGridY);

        CityOrbedService.ApplyOrbed(_world, victimBuild);

        var victimName = CityCatalog.IsValidCityId(victimCityId)
            ? CityCatalog.GetName(victimCityId)
            : _loadedCity?.CityName;
        var attackerName = CityCatalog.IsValidCityId(attackerCityId)
            ? CityCatalog.GetName(attackerCityId)
            : null;

        CityOrbedNotificationSystem.Trigger(
            _world,
            victimCityId,
            attackerCityId,
            victimName,
            attackerName);
        _audioBuffer.Play(SoundId.Die, ccCenter);

        if (attackerCityId != victimCityId)
        {
            _audioBuffer.Play(SoundId.Screech, ccCenter);
        }
    }

    public bool TryGetPlayerInputMove(out int move)
    {
        var query = new QueryDescription().WithAll<InputControlled, InputCommand>();
        var inputMove = 0;
        var found = false;

        World.Query(
            in query,
            (ref InputCommand input) =>
            {
                inputMove = input.Move;
                found = true;
            });

        move = inputMove;
        return found;
    }

    public bool TryGetPlayerPosition(out Vector2 position)
    {
        var query = new QueryDescription().WithAll<InputControlled, Transform2D>();
        var foundPosition = Vector2.Zero;
        var found = false;

        World.Query(
            in query,
            (ref Transform2D transform) =>
            {
                foundPosition = transform.Position;
                found = true;
            });

        position = foundPosition;
        return found;
    }

    public bool TryGetPlayerHealth(out Health health)
    {
        var query = new QueryDescription().WithAll<InputControlled, Health>();
        var foundHealth = default(Health);
        var found = false;

        World.Query(
            in query,
            (ref Health value) =>
            {
                foundHealth = value;
                found = true;
            });

        health = foundHealth;
        return found;
    }

    public bool TryGetPlayerLifeState(out TankLifeState life)
    {
        var query = new QueryDescription().WithAll<InputControlled, TankLifeState>();
        var foundLife = default(TankLifeState);
        var found = false;

        World.Query(
            in query,
            (ref TankLifeState value) =>
            {
                foundLife = value;
                found = true;
            });

        life = foundLife;
        return found;
    }

    public bool TryGetPlayerCityId(out int cityId)
    {
        var query = new QueryDescription().WithAll<InputControlled, CityAffiliation>();
        var found = false;
        var resolved = 0;
        _world.Query(
            in query,
            (ref CityAffiliation city) =>
            {
                if (found)
                {
                    return;
                }

                resolved = city.CityId;
                found = true;
            });

        cityId = resolved;
        return found;
    }

    public bool TryGetDemoEntityPosition(out Vector2 position) => TryGetPlayerPosition(out position);

    private CityBuildState? TryResolveCityBuildForPlayer(int cityId)
    {
        if (TryGetCityBuild(cityId, out var build))
        {
            return build;
        }

        return _cityBuilds.Count > 0 ? EnsureCityBuild(cityId) : _cityBuild;
    }

    private CityBuildState? TryResolveCityBuildForPlayer(Entity entity)
    {
        if (!_world.IsAlive(entity) || !_world.Has<CityAffiliation>(entity))
        {
            return _cityBuild;
        }

        return TryResolveCityBuildForPlayer(_world.Get<CityAffiliation>(entity).CityId);
    }

    public CityBuildState EnsureCityBuild(int cityId)
    {
        if (_cityBuilds.TryGetValue(cityId, out var existing))
        {
            return existing;
        }

        // Offline alias: city 0 may already be loaded under the layout's catalog id.
        if (cityId == 0 && _cityBuild is not null)
        {
            _cityBuilds[0] = _cityBuild;
            return _cityBuild;
        }

        var build = new CityBuildState { CityId = cityId };
        CityBuildInitializer.ApplyLegacyStartingPermissions(build);
        if (CityBuildInitializer.TryGetCommandCenterGridForCity(
                cityId,
                _tileMap,
                out var gridX,
                out var gridY))
        {
            build.CommandCenterGridX = gridX;
            build.CommandCenterGridY = gridY;
        }

        build.CurrentBuildingCount = 1;
        build.MaxBuildingCount = 1;
        _cityBuilds[cityId] = build;
        if (_cityBuild is null)
        {
            _cityBuild = build;
        }

        return build;
    }

    public IEnumerable<int> EnumerateCityBuildIds() => _cityBuilds.Keys;

    public bool TryGetCityBuild(int cityId, out CityBuildState build)
    {
        if (_cityBuilds.TryGetValue(cityId, out build!))
        {
            return true;
        }

        // Offline/local-home alias: cityId 0 accepts the primary layout build.
        if (cityId == 0)
        {
            if (_cityBuild is not null)
            {
                build = _cityBuild;
                return true;
            }

            foreach (var entry in _cityBuilds.Values)
            {
                build = entry;
                return true;
            }
        }

        build = null!;
        return false;
    }

    public bool TryPlaceBuilding(int buildSlot, int gridAnchorX, int gridAnchorY, Vector2? playerCenter = null)
    {
        if (_cityBuild is null)
        {
            return false;
        }

        return TryPlaceBuilding(_cityBuild.CityId, buildSlot, gridAnchorX, gridAnchorY, playerCenter);
    }

    public bool TryPlaceBuilding(
        int cityId,
        int buildSlot,
        int gridAnchorX,
        int gridAnchorY,
        Vector2? playerCenter = null)
    {
        var build = EnsureCityBuild(cityId);

        if (!BuildingCommandService.TryPlaceBuilding(
                _world,
                build,
                _tileMap,
                buildSlot,
                gridAnchorX,
                gridAnchorY,
                playerCenter))
        {
            return false;
        }

        if (BuildingPlacementValidator.TryFindBuildingAt(_world, gridAnchorX, gridAnchorY, out var entity))
        {
            ref var building = ref _world.Get<BuildingRef>(entity);
            building.CityId = cityId;
            if (building.NetworkId == 0)
            {
                building.NetworkId = AllocateNetworkBuildingId();
            }
        }

        return true;
    }

    public bool TryDemolishAt(int gridAnchorX, int gridAnchorY)
    {
        if (_cityBuild is null)
        {
            return false;
        }

        return BuildingCommandService.TryDemolishAt(_world, _cityBuild, gridAnchorX, gridAnchorY);
    }

    public void Dispose()
    {
        _world.Dispose();
    }
}

public readonly record struct PendingExplosionEvent(ServerExplosionPacket Explosion, ushort RemovedItemId = 0);

public readonly record struct PendingRespawnEvent(byte PlayerId, Vector2 Position, byte CityId);
