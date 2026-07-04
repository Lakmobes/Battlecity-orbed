using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

public static class ExplosionSprites
{
    public const int SmallFrameSize = GameConstants.TileSize;
    public const int LargeFrameSize = GameConstants.BuildingCollisionSize;
    public const int MuzzleFrameSize = 12;

    public static string GetTextureKey(ExplosionKind kind) =>
        kind switch
        {
            ExplosionKind.Large => "Sprites/LExplosion",
            ExplosionKind.MuzzleFlash => "Sprites/MuzzleFlash",
            _ => "Sprites/SExplosion",
        };

    public static int GetFrameCount(ExplosionKind kind) =>
        kind == ExplosionKind.MuzzleFlash ? 3 : 10;

    public static (int SourceX, int SourceY, int Width, int Height) GetFrameRect(ExplosionKind kind, int frame)
    {
        frame = Math.Clamp(frame, 0, GetFrameCount(kind) - 1);
        return kind switch
        {
            ExplosionKind.Large => (0, frame * LargeFrameSize, LargeFrameSize, LargeFrameSize),
            ExplosionKind.MuzzleFlash => (frame * MuzzleFrameSize, 0, MuzzleFrameSize, MuzzleFrameSize),
            _ => (frame * SmallFrameSize, 0, SmallFrameSize, SmallFrameSize),
        };
    }
}
