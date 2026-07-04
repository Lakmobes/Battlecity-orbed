using BattleCity.Core.Audio;
using BattleCity.Core.Ecs;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Core.Tests;

public class SimulationAudioTests
{
    [Fact]
    public void WeaponSystem_QueuesLaserSoundWhenFiring()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(new System.Numerics.Vector2(100f, 100f));
        ref var input = ref simulation.World.Get<BattleCity.Core.Ecs.Components.InputCommand>(player);
        ref var inventory = ref simulation.World.Get<BattleCity.Core.Ecs.Components.PlayerInventory>(player);
        input.FireHeld = true;
        inventory.Rocket = 0;

        simulation.Tick(GameSimulation.FixedDeltaSeconds);

        var events = simulation.ConsumeSoundEvents();
        Assert.Contains(events, e => e.Sound == SoundId.Laser);
    }
}
