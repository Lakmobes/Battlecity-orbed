using System.Numerics;

using Arch.Core;

using BattleCity.Core.Audio;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Core.Ecs.Systems;

public static class WeaponSystem
{
    private static readonly QueryDescription PlayerQuery =
        new QueryDescription().WithAll<InputControlled, InputCommand, Transform2D, TankFacing, TankLifeState, WeaponState, PlayerInventory, TankStatus, CityAffiliation>();

    public static void Update(
        World world,
        float deltaSeconds,
        Func<int, CityBuildState?>? resolveCityBuild = null,
        SimulationAudioBuffer? audio = null,
        Action<ClientShotPacket>? reportLocalShot = null)
    {
        world.Query(
            in PlayerQuery,
            (Entity entity, ref InputCommand input, ref Transform2D transform, ref TankFacing facing, ref TankLifeState life, ref WeaponState weapons, ref PlayerInventory inventory, ref TankStatus status, ref CityAffiliation city) =>
            {
                var cityBuild = resolveCityBuild?.Invoke(city.CityId);
                if (WeaponActions.TryFireFromInput(
                        world,
                        entity,
                        ref input,
                        ref weapons,
                        ref inventory,
                        ref facing,
                        ref life,
                        ref status,
                        transform.Position,
                        cityBuild,
                        audio,
                        out var networkShot)
                    && networkShot is { } shot)
                {
                    reportLocalShot?.Invoke(shot);
                }
            });
    }

    /// <summary>Backward-compatible overload used by older call sites/tests.</summary>
    public static void Update(
        World world,
        float deltaSeconds,
        CityBuildState? cityBuild,
        SimulationAudioBuffer? audio = null,
        Action<ClientShotPacket>? reportLocalShot = null) =>
        Update(world, deltaSeconds, _ => cityBuild, audio, reportLocalShot);
}
