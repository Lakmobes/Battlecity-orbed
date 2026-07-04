using System.Numerics;

using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;

using Xunit;

namespace BattleCity.Core.Tests;

public sealed class CityAlertTests
{
    [Fact]
    public void CityAlertSystem_TriggerForCity_SetsTimer()
    {
        using var simulation = new GameSimulation();
        var player = simulation.CreatePlayerEntity(Vector2.Zero);
        ref var alert = ref simulation.World.Get<CityAlertState>(player);

        CityAlertSystem.TriggerForCity(simulation.World, 0);

        Assert.True(alert.IsUnderAttack);
        Assert.InRange(alert.UnderAttackRemainingSeconds, 2.9f, 3f);
    }
}
