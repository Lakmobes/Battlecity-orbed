namespace BattleCity.Shared.Constants;

/// <summary>Server-side economy constants (legacy/server/CConstants.h).</summary>
public static class EconomyConstants
{
    public const int CostItem = 750_000;
    public const int CostIncomePopulation = 10_000;
    public const int CostUpkeepResearch = 2_000_000;
    public const int CostUpkeepHospital = 2_000_000;

    public const int MoneyMaxValue = 95_000_000;
    public const int MoneyStartingValue = 95_000_000;

    public const int OrbableSize = 21;
    public const int PopulationMaxHouse = 100;
    public const int PopulationMaxNonHouse = 50;

    public const int TimerBomb = 5000;
    public const int TimerCityDestruct = 120_000;
    public const int TimerResearch = 10_000;
}
