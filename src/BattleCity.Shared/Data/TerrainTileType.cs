namespace BattleCity.Shared.Data;

/// <summary>
/// World terrain cell values from legacy map.dat / CConstants.h.
/// </summary>
public enum TerrainTileType : byte
{
    Open = 0,
    Lava = 1,
    Rock = 2,
    CityCenter = 3,
}
