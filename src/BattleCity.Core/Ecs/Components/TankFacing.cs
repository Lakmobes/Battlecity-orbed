namespace BattleCity.Core.Ecs.Components;

/// <summary>
/// Tank heading in legacy 32-direction units (see legacy/client/CPlayer.cpp).
/// </summary>
public struct TankFacing
{
    public const int DirectionCount = 32;
    public const float TurnIntervalSeconds = 0.05f;

    public int Direction;
    public float TurnCooldownSeconds;
}
