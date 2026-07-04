namespace BattleCity.Core.Ecs.Components;

/// <summary>Legacy orbed notification overlay (victim message box / attacker screech).</summary>
public struct CityOrbedState
{
    public bool ShowOverlay;
    public float RemainingSeconds;
    public bool IsVictim;
    public string Message;
}
