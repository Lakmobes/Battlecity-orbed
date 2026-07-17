using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

public static class TurretSprites
{
    public const string BaseTextureKey = "Sprites/TurretBase";
    public const string HeadTextureKey = "Sprites/TurretHead";
    public const int SpriteSize = GameConstants.TileSize;

    /// <summary>
    /// Legacy draws turrets at <c>tileY - 10</c> after a shared <c>+10</c> item nudge, netting tile-aligned.
    /// World position is already grid-aligned, so no extra draw offset.
    /// </summary>
    public const int VerticalDrawOffset = 0;

    public static int GetSheetRow(ItemType type) => (int)type - (int)ItemType.Turret;
}
