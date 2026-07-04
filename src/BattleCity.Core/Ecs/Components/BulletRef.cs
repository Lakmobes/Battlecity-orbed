using Arch.Core;

using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Components;

public struct BulletRef
{
    public BulletKind Kind;
    public int Direction;
    public Entity Owner;
    public int AnimationFrame;
    public float CollisionGraceSeconds;
}
