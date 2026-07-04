using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Core.Tests;

public sealed class GadgetTests
{
    [Fact]
    public void Cloak_ActivatesAndExpires()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(Vector2.Zero);
        ref var input = ref simulation.World.Get<InputCommand>(player);
        ref var inventory = ref simulation.World.Get<PlayerInventory>(player);
        ref var status = ref simulation.World.Get<TankStatus>(player);

        inventory.Cloak = 1;
        input.UseCloakPressed = true;
        ItemDropSystem.Update(simulation.World);

        Assert.Equal(0, inventory.Cloak);
        Assert.True(status.IsCloaked);
        Assert.InRange(status.CloakRemainingSeconds, 4.9f, 5f);

        for (var i = 0; i < 301; i++)
        {
            simulation.Tick(GameSimulation.FixedDeltaSeconds);
        }

        Assert.False(status.IsCloaked);
    }

    [Fact]
    public void Cloak_HidesPlayerFromTurretTargeting()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(new Vector2(10 * 48f, 10 * 48f));
        GameplayEntityFactory.CreatePlacedItem(simulation.World, ItemType.Turret, 12, 10, cityId: 0);

        ref var status = ref simulation.World.Get<TankStatus>(player);
        TankStatusSystem.ActivateCloak(ref status);

        for (var i = 0; i < 130; i++)
        {
            simulation.Tick(GameSimulation.FixedDeltaSeconds);
        }

        var turretQuery = new QueryDescription().WithAll<TurretState>();
        var hasTarget = false;
        simulation.World.Query(in turretQuery, (ref TurretState turret) =>
        {
            if (turret.HasTarget)
            {
                hasTarget = true;
            }
        });

        Assert.False(hasTarget);
    }

    [Fact]
    public void Dfg_FreezesEnemyTank()
    {
        using var simulation = new GameSimulation();
        var dfg = GameplayEntityFactory.CreatePlacedItem(
            simulation.World,
            ItemType.Dfg,
            10,
            10,
            cityId: 0);
        var bot = simulation.CreateBotEntity(new Vector2(10 * 48f, 10 * 48f), cityId: 1);
        ref var status = ref simulation.World.Get<TankStatus>(bot);

        simulation.Tick(GameSimulation.FixedDeltaSeconds);

        Assert.False(simulation.World.IsAlive(dfg));
        Assert.True(status.IsFrozen);
    }

    [Fact]
    public void FrozenTank_CannotMove()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(Vector2.Zero);
        ref var input = ref simulation.World.Get<InputCommand>(player);
        ref var velocity = ref simulation.World.Get<Velocity>(player);
        ref var status = ref simulation.World.Get<TankStatus>(player);

        TankStatusSystem.ActivateFreeze(ref status);
        input.Move = 1;

        InputSystem.Update(simulation.World, GameSimulation.FixedDeltaSeconds);

        Assert.Equal(Vector2.Zero, velocity.Value);
    }
}
