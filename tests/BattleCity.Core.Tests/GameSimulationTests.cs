using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;

using Xunit;

namespace BattleCity.Core.Tests;

public class GameSimulationTests
{
    [Fact]
    public void MovementSystemAdvancesTransformOverTime()
    {
        using var simulation = new GameSimulation();
        var entity = simulation.CreateDemoEntity(Vector2.Zero, new Vector2(60f, 0f));

        simulation.Tick(0.5f);

        ref var transform = ref simulation.World.Get<Transform2D>(entity);
        Assert.Equal(30f, transform.Position.X, precision: 3);
        Assert.Equal(0f, transform.Position.Y, precision: 3);
    }

    [Fact]
    public void FixedTimestepAccumulatorRunsMultipleTicks()
    {
        using var simulation = new GameSimulation();
        var entity = simulation.CreateDemoEntity(Vector2.Zero, new Vector2(60f, 0f));

        simulation.Update(GameSimulation.FixedDeltaSeconds * 3f);

        ref var transform = ref simulation.World.Get<Transform2D>(entity);
        Assert.Equal(3f, transform.Position.X, precision: 3);
    }

    [Fact]
    public void DemoEntityIncludesColliderMatchingLegacyInset()
    {
        using var simulation = new GameSimulation();
        var entity = simulation.CreateDemoEntity(Vector2.Zero, Vector2.Zero);

        ref var collider = ref simulation.World.Get<Collider>(entity);
        Assert.Equal(CollisionLayer.Player, collider.Layer);
        Assert.Equal(8, collider.OffsetX);
        Assert.Equal(32, collider.Width);
    }

    [Fact]
    public void WorldClampReflectsVelocityAtMapEdge()
    {
        using var simulation = new GameSimulation();
        var max = Shared.Constants.GameConstants.WorldSizePixels - Shared.Constants.GameConstants.TileSize;
        var patrol = simulation.CreatePatrolEntity(new Vector2(max, 0f), new Vector2(100f, 0f));

        simulation.Tick(1f);

        ref var transform = ref simulation.World.Get<Transform2D>(patrol);
        ref var velocity = ref simulation.World.Get<Velocity>(patrol);

        Assert.Equal(max, transform.Position.X);
        Assert.True(velocity.Value.X < 0f);
    }
}
