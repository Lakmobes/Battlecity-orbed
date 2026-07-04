using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Shared.Constants;

using Xunit;

namespace BattleCity.Core.Tests;

public class CombatLifeTests
{
    [Fact]
    public void CombatLifeSystem_RespawnsPlayerAfterTimer()
    {
        using var world = World.Create();
        var spawn = new Vector2(100f, 200f);

        var entity = world.Create(
            new InputControlled(),
            new Transform2D { Position = spawn + new Vector2(50f, 0f), PreviousPosition = spawn },
            new Velocity(),
            new Health { Current = 0, Max = 100 },
            new TankLifeState { IsDead = true, RespawnTimerSeconds = 0.1f, SpawnPosition = spawn },
            new SpriteRef { TextureKey = "Sprites/Tanks", Width = GameConstants.TileSize, Height = GameConstants.TileSize });

        CombatLifeSystem.Update(world, 0.2f);

        ref var life = ref world.Get<TankLifeState>(entity);
        ref var health = ref world.Get<Health>(entity);
        ref var transform = ref world.Get<Transform2D>(entity);

        Assert.False(life.IsDead);
        Assert.Equal(100, health.Current);
        Assert.Equal(spawn, transform.Position);
    }

    [Fact]
    public void CombatLifeSystem_MarksTankDeadWhenHealthReachesZero()
    {
        using var world = World.Create();
        var spawn = new Vector2(64f, 64f);

        var entity = world.Create(
            new Transform2D { Position = spawn, PreviousPosition = spawn },
            new Velocity(),
            new Health { Current = 0, Max = 100 },
            new TankLifeState { SpawnPosition = spawn },
            new SpriteRef { TextureKey = "Sprites/Tanks", Width = GameConstants.TileSize, Height = GameConstants.TileSize });

        CombatLifeSystem.Update(world, 0.016f);

        ref var life = ref world.Get<TankLifeState>(entity);

        Assert.True(life.IsDead);
        Assert.True(life.RespawnTimerSeconds > 0f);
    }
}
