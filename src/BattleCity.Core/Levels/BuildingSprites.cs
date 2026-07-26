using BattleCity.Shared.Constants;

namespace BattleCity.Core.Levels;

public static class BuildingSprites
{
    public const int SpriteSize = GameConstants.BuildingCollisionSize;
    public const int AnimationColumnCount = 3;
    public const string TextureKey = "Sprites/Buildings";

    public static (int SourceX, int SourceY) GetSourceOrigin(int typeCode, int animationFrame = 0)
    {
        var category = typeCode / 100;
        var column = Math.Clamp(animationFrame / 2, 0, AnimationColumnCount - 1);
        return (column * SpriteSize, category * SpriteSize);
    }
}
