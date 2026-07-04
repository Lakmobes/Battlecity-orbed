using System.Numerics;

namespace BattleCity.Core.Ecs.Components;

public struct Transform2D
{
    public Vector2 Position;
    public Vector2 PreviousPosition;
    public float RotationDegrees;
}
