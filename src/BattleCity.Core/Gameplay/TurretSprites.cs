using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

public static class TurretSprites
{
    public const string BaseTextureKey = "Sprites/TurretBase";
    public const string HeadTextureKey = "Sprites/TurretHead";
    public const int SpriteSize = GameConstants.TileSize;
    public const int VerticalDrawOffset = -10;

    public static int GetSheetRow(ItemType type) => (int)type - (int)ItemType.Turret;
}
