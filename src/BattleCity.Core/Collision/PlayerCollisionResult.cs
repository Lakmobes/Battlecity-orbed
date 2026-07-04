namespace BattleCity.Core.Collision;

/// <summary>Return codes aligned with legacy <c>CCollision::CheckPlayerCollision</c>.</summary>
public enum PlayerCollisionResult
{
    None = 0,
    Blocking = 2,
    LeftMapEdge = 200,
    RightMapEdge = 201,
    TopMapEdge = 202,
    BottomMapEdge = 203,
}
