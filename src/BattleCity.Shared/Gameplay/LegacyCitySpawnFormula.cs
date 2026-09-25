using BattleCity.Shared.Constants;

namespace BattleCity.Shared.Gameplay;

/// <summary>
/// Legacy city spawn / compass coords from <c>legacy/server/CMap.cpp</c> <c>CalculateTiles</c>.
/// The rewrite intentionally uses the real CC drive platform instead — see LEGACY-AUDIT-PLAN § Spawning.
/// </summary>
public static class LegacyCitySpawnFormula
{
    /// <summary>Legacy <c>City[citIndex]->x/y</c> in world pixels for city index 0..63.</summary>
    public static (int X, int Y) GetPixels(int cityIndex)
    {
        var index = Math.Clamp(cityIndex, 0, 63);
        var x = (GameConstants.MapSize * GameConstants.TileSize)
            - (32 + (index % 8 * 64) + 1) * GameConstants.TileSize;
        var y = (GameConstants.MapSize * GameConstants.TileSize)
            - (32 + (index / 8 * 64) + 1) * GameConstants.TileSize;
        return (x, y);
    }
}
