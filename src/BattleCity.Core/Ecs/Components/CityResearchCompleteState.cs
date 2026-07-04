namespace BattleCity.Core.Ecs.Components;

/// <summary>Research completion notification overlay.</summary>
public struct CityResearchCompleteState
{
    public bool ShowOverlay;
    public float RemainingSeconds;
    public string Message;
}
