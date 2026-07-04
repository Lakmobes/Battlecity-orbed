using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

public static class BulletSprites
{
    public const string TextureKey = "Sprites/Bullets";
    public const int SpriteSize = 8;

    public static (int SourceX, int SourceY) GetSourceOrigin(BulletKind kind, int animationFrame) =>
        (animationFrame * SpriteSize, (int)kind * SpriteSize);
}
