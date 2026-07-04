using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Gameplay;

public static class ItemSprites
{
    public const string TextureKey = "Sprites/Items";
    public const int WorldSpriteSize = GameConstants.TileSize;
    public const int DroppedRowY = 42;

    public static (int SourceX, int SourceY) GetWorldSpriteOrigin(ItemType type, int animationFrame = 0)
    {
        if (type == ItemType.Orb)
        {
            return ((int)type * WorldSpriteSize, DroppedRowY + animationFrame * WorldSpriteSize);
        }

        if (type == ItemType.Bomb)
        {
            return (144, 91);
        }

        return ((int)type * WorldSpriteSize, DroppedRowY);
    }

    /// <summary>32×32 inventory icons (legacy panel uses row 0 of <c>imgItems</c>).</summary>
    public static (int SourceX, int SourceY) GetInventorySpriteOrigin(ItemType type, bool bombsActivated = true)
    {
        if (type == ItemType.Bomb && bombsActivated)
        {
            return (152, 89);
        }

        if (type == ItemType.Orb)
        {
            return (250, 41);
        }

        return ((int)type * 32, 0);
    }
}
