namespace BattleCity.Core.Ecs.Components;

/// <summary>Legacy under-attack panel alert (3 s timer, flashing compass).</summary>
public struct CityAlertState
{
    public bool IsUnderAttack;
    public float UnderAttackRemainingSeconds;
    public float ArrowFlashTimerSeconds;
    public bool FlashArrowVisible;
}
