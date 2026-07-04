using System.Numerics;

using Arch.Core;

using BattleCity.Core.Audio;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Core.Ecs.Systems;

public static class WeaponSystem
{
    private static readonly QueryDescription PlayerQuery =
        new QueryDescription().WithAll<InputControlled, InputCommand, Transform2D, TankFacing, TankLifeState, WeaponState, PlayerInventory, TankStatus>();

    public static void Update(
        World world,
        float deltaSeconds,
        SimulationAudioBuffer? audio = null,
        Action<ClientShotPacket>? reportLocalShot = null)
    {
        world.Query(
            in PlayerQuery,
            (Entity entity, ref InputCommand input, ref Transform2D transform, ref TankFacing facing, ref TankLifeState life, ref WeaponState weapons, ref PlayerInventory inventory, ref TankStatus status) =>
            {
                WeaponActions.AdvanceCooldowns(ref weapons, deltaSeconds);

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
                        audio,
                        out var networkShot)
                    && networkShot is { } shot)
                {
                    reportLocalShot?.Invoke(shot);
                }
            });
    }
}
