using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

/// <summary>
/// Items atlas: columns = <see cref="ItemType"/> (0..11), rows = animation frames (0..3).
/// Coordinates are legacy 48px cells; <c>WorldSpriteScale</c> maps them to 96px in the PNG.
/// </summary>
public static class ItemSprites
{
    public const string TextureKey = "Sprites/Items";
    public const int WorldSpriteSize = GameConstants.TileSize;
    public const int ColumnCount = 12;
    public const int FrameCount = 4;

    /// <summary>Slow discrete frame cycle for field item bobbing.</summary>
    public const float FramesPerSecond = 0.8f;

    /// <summary>PNG cell size when <c>DisplaySettings.WorldSpriteScale == 2</c>.</summary>
    public const int HdCellSize = WorldSpriteSize * 2;

    /// <summary>Expected PNG size for a tight 12×4 sheet at 2× HD.</summary>
    public const int ExpectedSheetWidth = ColumnCount * HdCellSize;

    public const int ExpectedSheetHeight = FrameCount * HdCellSize;

    /// <summary>No legacy Y nudge — new sheet is centered on the tile.</summary>
    public const int WorldDrawOffsetY = 0;

    public static (int SourceX, int SourceY) GetWorldSpriteOrigin(ItemType type, int animationFrame = 0)
    {
        var frame = Math.Clamp(animationFrame, 0, FrameCount - 1);
        return ((int)type * WorldSpriteSize, frame * WorldSpriteSize);
    }

    /// <summary>Same atlas as world sprites; inventory uses frame 0.</summary>
    public static (int SourceX, int SourceY) GetInventorySpriteOrigin(ItemType type, bool bombsActivated = true)
    {
        _ = bombsActivated;
        return GetWorldSpriteOrigin(type, animationFrame: 0);
    }

    public static int ResolveAnimationFrame(ItemType type, bool active, float timeSeconds)
    {
        if (type == ItemType.Bomb && !active)
        {
            return 0;
        }

        if (UsesTurretSheet(type))
        {
            return 0;
        }

        var frame = (int)(timeSeconds * FramesPerSecond) % FrameCount;
        return frame < 0 ? 0 : frame;
    }

    public static bool UsesItemSheetAnimation(ItemType type) => !UsesTurretSheet(type);

    public static bool UsesTurretSheet(ItemType type) =>
        type is ItemType.Turret or ItemType.Sleeper or ItemType.Plasma;
}
