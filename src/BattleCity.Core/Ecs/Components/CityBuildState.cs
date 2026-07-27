namespace BattleCity.Core.Ecs.Components;

using BattleCity.Shared.Constants;

/// <summary>
/// Per-city build permissions (legacy canBuild, without economy).
/// CanBuild: 0 = locked, 1 = buildable, 2 = already has one.
/// </summary>
public sealed class CityBuildState
{
    public const int MenuSlotCount = 26;
    public const int ResearchSlotCount = 12;

    public int CityId { get; set; }

    public int[] CanBuild { get; } = new int[MenuSlotCount];

    /// <summary>0 = idle/in progress, -1 = complete (legacy city.research).</summary>
    public int[] ResearchStatus { get; } = new int[ResearchSlotCount];

    public float[] ResearchTimers { get; } = new float[ResearchSlotCount];

    public int CommandCenterGridX { get; set; }

    public int CommandCenterGridY { get; set; }

    public int CurrentBuildingCount { get; set; } = 1;

    public int MaxBuildingCount { get; set; } = 1;

    public bool HadBombFactory { get; set; }

    public bool HadOrbFactory { get; set; }

    /// <summary>Successful orbs scored by this city (legacy <c>CCity::Orbs</c>).</summary>
    public int Orbs { get; set; }

    public bool IsOrbable =>
        HadBombFactory || HadOrbFactory || MaxBuildingCount >= EconomyConstants.OrbableSize;

    /// <summary>Legacy <c>CCity::getOrbValue</c> — points awarded when this city is orbed.</summary>
    public int GetOrbValue()
    {
        var points = 0;
        if (MaxBuildingCount >= EconomyConstants.OrbableSize + 10)
        {
            points = 50;
        }
        else if (MaxBuildingCount >= EconomyConstants.OrbableSize + 5)
        {
            points = 40;
        }
        else if (MaxBuildingCount >= EconomyConstants.OrbableSize)
        {
            points = 30;
        }
        else if (HadOrbFactory)
        {
            points = 20;
        }
        else if (HadBombFactory)
        {
            points = 10;
        }

        return points + (Orbs * 5);
    }

    public void RegisterBuildingPlaced(int menuIndex, int typeCode)
    {
        CurrentBuildingCount++;
        if (CurrentBuildingCount > MaxBuildingCount)
        {
            MaxBuildingCount = CurrentBuildingCount;
        }

        if (typeCode == 101)
        {
            HadBombFactory = true;
        }
        else if (typeCode == 105)
        {
            HadOrbFactory = true;
        }
    }

    public void RegisterBuildingRemoved()
    {
        if (CurrentBuildingCount > 1)
        {
            CurrentBuildingCount--;
        }
    }
}
