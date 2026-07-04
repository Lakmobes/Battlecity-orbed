using System.Numerics;



using Arch.Core;



using BattleCity.Core.Ecs;

using BattleCity.Core.Ecs.Components;

using BattleCity.Core.Gameplay;

using BattleCity.Shared.Constants;

using BattleCity.Shared.Gameplay;

using BattleCity.Shared.Network.Packets;



namespace BattleCity.Client.Network;



public sealed class RemotePlayerSync

{

    private readonly World _world;

    private readonly Dictionary<byte, Entity> _remotePlayers = new();

    private readonly Dictionary<byte, string> _displayNames = new();

    private readonly Dictionary<byte, PlayerNetworkStats> _stats = new();

    public record struct PlayerNetworkStats(uint Points, uint Deaths, uint Orbs, uint Assists, uint MonthlyPoints);



    public RemotePlayerSync(World world)

    {

        _world = world;

    }



    public int ObserverCityId { get; set; }



    public void SetDisplayName(byte playerId, string displayName) =>

        _displayNames[playerId] = displayName;



    public string? GetDisplayName(byte playerId) =>

        _displayNames.TryGetValue(playerId, out var name) ? name : null;



    public string GetChatDisplayName(byte playerId, bool isAdmin = false)

    {

        var baseName = GetDisplayName(playerId) ?? $"Player{playerId}";

        return _stats.TryGetValue(playerId, out var stats)

            ? PlayerRankCatalog.FormatChatName(baseName, stats.Points, isAdmin)

            : baseName;

    }



    public void ApplyPointsUpdate(in ServerPointsUpdatePacket packet) =>

        _stats[packet.PlayerId] = new PlayerNetworkStats(

            packet.Points,

            packet.Deaths,

            packet.Orbs,

            packet.Assists,

            packet.MonthlyPoints);



    public bool TryGetStats(byte playerId, out PlayerNetworkStats stats) =>

        _stats.TryGetValue(playerId, out stats);



    public IEnumerable<(byte PlayerId, string DisplayName)> EnumerateDisplayNames()

    {

        foreach (var (playerId, displayName) in _displayNames)

        {

            yield return (playerId, displayName);

        }

    }



    public bool TryGetCityId(byte playerId, out byte cityId)

    {

        if (!_remotePlayers.TryGetValue(playerId, out var entity))

        {

            cityId = 0;

            return false;

        }



        cityId = (byte)_world.Get<CityAffiliation>(entity).CityId;

        return true;

    }



    public void HandleJoin(ServerJoinDataPacket joinData, Vector2 fallbackPosition)

    {

        if (_remotePlayers.ContainsKey(joinData.PlayerId))

        {

            return;

        }



        var entity = CreateRemoteEntity(fallbackPosition, joinData.City, joinData.PlayerId, joinData.Mayor != 0);

        _remotePlayers[joinData.PlayerId] = entity;

    }



    public void ApplyUpdate(ServerUpdatePacket update)

    {

        if (!_remotePlayers.TryGetValue(update.PlayerId, out var entity))

        {

            var position = new Vector2(update.X, update.Y);

            entity = CreateRemoteEntity(position, cityId: ObserverCityId, update.PlayerId, isMayor: false);

            _remotePlayers[update.PlayerId] = entity;

        }

        if (_world.Has<TankLifeState>(entity) && _world.Get<TankLifeState>(entity).IsDead)

        {

            return;

        }



        ref var transform = ref _world.Get<Transform2D>(entity);

        transform.Position = new Vector2(update.X, update.Y);

        transform.PreviousPosition = transform.Position;



        ref var facing = ref _world.Get<TankFacing>(entity);

        facing.Direction = update.Direction;



        ref var sprite = ref _world.Get<SpriteRef>(entity);

        sprite.SourceX = update.Direction / 2 * GameConstants.TileSize;



        ref var mayor = ref _world.Get<MayorStatus>(entity);

        ref var city = ref _world.Get<CityAffiliation>(entity);

        ApplyTankSprite(ref sprite, city.CityId, mayor.IsMayor);

    }



    public void ApplyRespawn(byte playerId)

    {

        if (!_remotePlayers.TryGetValue(playerId, out var entity))

        {

            return;

        }



        ref var life = ref _world.Get<TankLifeState>(entity);

        ref var health = ref _world.Get<Health>(entity);

        ref var transform = ref _world.Get<Transform2D>(entity);



        life.IsDead = false;

        life.KillerCityId = EntityCityLookup.UnknownCity;

        health.Current = health.Max;

        transform.Position = Vector2.Zero;

        transform.PreviousPosition = Vector2.Zero;

        if (_world.Has<Collider>(entity))

        {

            ref var collider = ref _world.Get<Collider>(entity);

            collider.Layer = CollisionLayer.Player;

        }

    }



    public void SetMayor(byte playerId, bool isMayor)

    {

        if (!_remotePlayers.TryGetValue(playerId, out var entity))

        {

            return;

        }



        ref var mayor = ref _world.Get<MayorStatus>(entity);

        mayor.IsMayor = isMayor;



        ref var sprite = ref _world.Get<SpriteRef>(entity);

        ref var city = ref _world.Get<CityAffiliation>(entity);

        ApplyTankSprite(ref sprite, city.CityId, isMayor);

    }



    public void Remove(byte playerId)

    {

        if (!_remotePlayers.Remove(playerId, out var entity))

        {

            return;

        }



        _world.Destroy(entity);

        _displayNames.Remove(playerId);

        _stats.Remove(playerId);

    }



    public void Clear()

    {

        foreach (var entity in _remotePlayers.Values)

        {

            _world.Destroy(entity);

        }



        _remotePlayers.Clear();

        _displayNames.Clear();

        _stats.Clear();

    }



    private Entity CreateRemoteEntity(Vector2 position, int cityId, byte playerId, bool isMayor)

    {

        var sprite = new SpriteRef

        {

            TextureKey = "Sprites/Tanks",

            SourceX = 0,

            SourceY = 0,

            Width = GameConstants.TileSize,

            Height = GameConstants.TileSize,

        };

        ApplyTankSprite(ref sprite, cityId, isMayor);



        return _world.Create(

            new Transform2D { Position = position, PreviousPosition = position },

            new Velocity { Value = Vector2.Zero },

            sprite,

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

            new MayorStatus { IsMayor = isMayor },

            new NetworkIdentity { PlayerId = playerId },

            new TankStatus());

    }



    private void ApplyTankSprite(ref SpriteRef sprite, int playerCityId, bool isMayor)

    {

        sprite.SourceY = TankSpriteSelector.GetSourceY(ObserverCityId, playerCityId, isMayor)

            * GameConstants.TileSize;

    }

}


