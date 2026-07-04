namespace BattleCity.Core.Levels;

/// <summary>
/// One building from a legacy <c>.city</c> file (<c>menuIndex gridX gridY</c>).
/// </summary>
public readonly record struct CityBuildingPlacement(int MenuIndex, int GridX, int GridY, int TypeCode);
