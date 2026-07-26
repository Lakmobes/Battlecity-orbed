using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Maps;

/// <summary>
/// Autotile source-X for lava/rock strips (16 columns × 48px legacy / 96px HD).
/// Column = open-edge bitmask: left=1, right=2, down=4, up=8
/// (screen space, Y increases downward — matches the current Lava/Rocks sheet order).
/// </summary>
public static class AutotileCalculator
{
    public static short[,] Calculate(TerrainTileType[,] terrain)
    {
        var size = terrain.GetLength(0);
        var autotiles = new short[size, size];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var tile = terrain[x, y];
                if (tile is not (TerrainTileType.Lava or TerrainTileType.Rock))
                {
                    continue;
                }

                var current = (int)tile;
                var left = ToBit(x == 0 || (int)terrain[x - 1, y] != current);
                var right = ToBit(x == size - 1 || (int)terrain[x + 1, y] != current);
                var up = ToBit(y == 0 || (int)terrain[x, y - 1] != current);
                var down = ToBit(y == size - 1 || (int)terrain[x, y + 1] != current);

                autotiles[x, y] = (short)((left + right * 2 + down * 4 + up * 8) * GameConstants.TileSize);
            }
        }

        return autotiles;
    }

    private static int ToBit(bool value) => value ? 1 : 0;
}
