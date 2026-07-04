using System.Numerics;

using Arch.Core;

using BattleCity.Core.Audio;
using BattleCity.Core.Collision;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Gameplay;
using BattleCity.Core.Levels;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Systems;

public static class CombatLifeSystem
{
    private static readonly QueryDescription TankQuery =
        new QueryDescription().WithAll<Transform2D, Velocity, Health, TankLifeState, SpriteRef>();

    private static readonly QueryDescription PlayerQuery =
        new QueryDescription().WithAll<InputControlled, Transform2D, Health, TankLifeState>();

    private static readonly QueryDescription BuildingQuery =
        new QueryDescription().WithAll<Transform2D, BuildingRef>();

    public static void Update(
        World world,
        float deltaSeconds,
        SimulationAudioBuffer? audio = null,
        bool networkPlayersUseLocalHealthDeath = true,
        Action<byte, byte>? onNetworkPlayerDied = null,
        CombatLifeSimulationHooks hooks = default)
    {
        ProcessDeathAndRespawn(
            world,
            deltaSeconds,
            audio,
            networkPlayersUseLocalHealthDeath,
            onNetworkPlayerDied,
            hooks);
        ProcessHospitalHealing(world, deltaSeconds);
    }

    private static void ProcessDeathAndRespawn(
        World world,
        float deltaSeconds,
        SimulationAudioBuffer? audio,
        bool networkPlayersUseLocalHealthDeath,
        Action<byte, byte>? onNetworkPlayerDied,
        CombatLifeSimulationHooks hooks)
    {
        world.Query(
            in TankQuery,
            (Entity entity, ref Transform2D transform, ref Velocity velocity, ref Health health, ref TankLifeState life, ref SpriteRef sprite) =>
            {
                life.HospitalHealCooldownSeconds = Math.Max(0f, life.HospitalHealCooldownSeconds - deltaSeconds);

                var isNetworkPlayer = world.Has<NetworkIdentity>(entity);
                if (!life.IsDead)
                {
                    if (health.Current > 0)
                    {
                        return;
                    }

                    if (isNetworkPlayer && !networkPlayersUseLocalHealthDeath)
                    {
                        return;
                    }

                    life.IsDead = true;
                    life.RespawnTimerSeconds = GameConstants.TimerRespawn / 1000f;
                    velocity.Value = Vector2.Zero;
                    transform.PreviousPosition = transform.Position;

                    var center = GetTankCenter(transform.Position);
                    GameplayEntityFactory.CreateExplosion(world, ExplosionKind.Small, center);
                    audio?.Play(SoundId.Die, center);

                    if (isNetworkPlayer)
                    {
                        var killerCity = life.KillerCityId;
                        onNetworkPlayerDied?.Invoke(world.Get<NetworkIdentity>(entity).PlayerId, killerCity);
                    }

                    return;
                }

                life.RespawnTimerSeconds -= deltaSeconds;
                if (life.RespawnTimerSeconds < 0f)
                {
                    life.RespawnTimerSeconds = 0f;
                }

                velocity.Value = Vector2.Zero;

                if (life.RespawnTimerSeconds > 0f)
                {
                    return;
                }

                if (world.Has<NetworkIdentity>(entity) && hooks.DeferNetworkPlayerRespawn)
                {
                    return;
                }

                if (world.Has<InputControlled>(entity) && hooks.SuppressLocalPlayerRespawn)
                {
                    return;
                }

                life.IsDead = false;
                life.KillerCityId = EntityCityLookup.UnknownCity;
                health.Current = health.Max;
                transform.Position = life.SpawnPosition;
                transform.PreviousPosition = life.SpawnPosition;
            });
    }

    private static void ProcessHospitalHealing(World world, float deltaSeconds)
    {
        world.Query(
            in PlayerQuery,
            (ref Transform2D transform, ref Health health, ref TankLifeState life) =>
            {
                if (life.IsDead || life.HospitalHealCooldownSeconds > 0f || health.Current >= health.Max)
                {
                    return;
                }

                var center = GetTankCenter(transform.Position);
                if (!TryGetHospitalHeal(world, center))
                {
                    return;
                }

                health.Current = Math.Min(health.Max, health.Current + 5);
                life.HospitalHealCooldownSeconds = 0.5f;
            });
    }

    private static bool TryGetHospitalHeal(World world, Vector2 tankCenter)
    {
        var centerBox = new AxisAlignedBox(tankCenter.X, tankCenter.Y, 1f, 1f);
        var found = false;

        world.Query(
            in BuildingQuery,
            (ref Transform2D transform, ref BuildingRef building) =>
            {
                if (found || !BuildingCatalog.IsHospital(building.TypeCode))
                {
                    return;
                }

                var fullBounds = BuildingCollision.GetSpriteBounds(transform.Position);

                if (fullBounds.Intersects(centerBox)
                    && BuildingCollision.IsPointOnDrivePlatform(building.TypeCode, transform.Position, tankCenter))
                {
                    found = true;
                }
            });

        return found;
    }

    private static Vector2 GetTankCenter(Vector2 tankTopLeft) =>
        new(
            tankTopLeft.X + GameConstants.TileSize / 2f,
            tankTopLeft.Y + GameConstants.TileSize / 2f);
}
