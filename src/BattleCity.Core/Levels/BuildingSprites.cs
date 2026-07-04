using BattleCity.Shared.Constants;

namespace BattleCity.Core.Levels;

public static class BuildingSprites
{
    public const int SpriteSize = GameConstants.BuildingCollisionSize;
    public const string TextureKey = "Sprites/Buildings";

    public static (int SourceX, int SourceY) GetSourceOrigin(int typeCode)
    {
        var category = typeCode / 100;
        return (0, category * SpriteSize);
    }
}
