namespace BattleCity.Core.Ecs.Components;

/// <summary>Scripted tank brain (offline practice + online AI City).</summary>
public struct BotController
{
    public float FireCooldownSeconds;
    public float AggroRangePixels;

    /// <summary>0 = defend (hold home), 1 = attack (push toward goal when idle).</summary>
    public byte Role;

    public float HomeX;
    public float HomeY;
    public float GoalX;
    public float GoalY;
    public bool HasGoal;
}

public static class BotRoles
{
    public const byte Defend = 0;
    public const byte Attack = 1;
}
