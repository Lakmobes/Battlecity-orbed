using System.Numerics;

using BattleCity.Core.Levels;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.Collision;

/// <summary>
/// Legacy building footprints from <c>legacy/client/CCollision.cpp</c>.
/// Factories and hospitals block the upper structure; tanks drive on the southern bay row.
/// </summary>
public static class BuildingCollision
{
    public const int PlatformHeightPixels = GameConstants.TileSize;

    public const int RaisedBlockingHeightPixels = GameConstants.TileSize * 2;

    public static AxisAlignedBox GetSpriteBounds(Vector2 spriteTopLeft) =>
        new(spriteTopLeft.X, spriteTopLeft.Y, GameConstants.BuildingCollisionSize, GameConstants.BuildingCollisionSize);

    /// <summary>Southern bay row (grid anchor Y).</summary>
    public static AxisAlignedBox GetDrivePlatformBounds(Vector2 spriteTopLeft) =>
        new(
            spriteTopLeft.X,
            spriteTopLeft.Y + RaisedBlockingHeightPixels,
            GameConstants.BuildingCollisionSize,
            PlatformHeightPixels);

    public static AxisAlignedBox GetPlayerBlockingBounds(int typeCode, Vector2 spriteTopLeft)
    {
        if (UsesRaisedPlatformCollision(typeCode))
        {
            return new AxisAlignedBox(
                spriteTopLeft.X,
                spriteTopLeft.Y,
                GameConstants.BuildingCollisionSize,
                RaisedBlockingHeightPixels);
        }

        return GetSpriteBounds(spriteTopLeft);
    }

    /// <summary>
    /// Bullet hit box aligned with the drawn sprite / player blocking footprint.
    /// Legacy used <c>(anchor-3)*48</c> while draw lived at <c>anchor*48</c>; the rewrite
    /// draws at <c>(anchor-2)*48</c>, so the old bullet offset read as early/late hits.
    /// </summary>
    public static AxisAlignedBox GetBulletHitBounds(int typeCode, int gridAnchorX, int gridAnchorY)
    {
        var topLeft = BuildingPlacement.GridAnchorToWorldPosition(gridAnchorX, gridAnchorY);
        return GetPlayerBlockingBounds(typeCode, topLeft);
    }

    public static bool BlocksPlayerMovement(int typeCode, Vector2 spriteTopLeft, in AxisAlignedBox playerBounds) =>
        GetPlayerBlockingBounds(typeCode, spriteTopLeft).Intersects(playerBounds);

    public static bool BlocksItemPlacement(Vector2 tileCenter, int typeCode, Vector2 spriteTopLeft) =>
        GetPlayerBlockingBounds(typeCode, spriteTopLeft).ContainsPoint(tileCenter);

    public static bool IsPointOnDrivePlatform(int typeCode, Vector2 spriteTopLeft, Vector2 worldPoint)
    {
        if (!UsesRaisedPlatformCollision(typeCode))
        {
            return false;
        }

        return GetDrivePlatformBounds(spriteTopLeft).ContainsPoint(worldPoint);
    }

    public static bool UsesRaisedPlatformCollision(int typeCode) => typeCode / 100 <= 2;

    public static (int OffsetX, int OffsetY, int Width, int Height) GetPlayerColliderShape(int typeCode)
    {
        if (UsesRaisedPlatformCollision(typeCode))
        {
            return (0, 0, GameConstants.BuildingCollisionSize, RaisedBlockingHeightPixels);
        }

        return (0, 0, GameConstants.BuildingCollisionSize, GameConstants.BuildingCollisionSize);
    }

    public static int GetStructureSortDepth(Vector2 spriteTopLeft) =>
        (int)(spriteTopLeft.Y + RaisedBlockingHeightPixels - 1);

    public static int GetPlatformSortDepth(Vector2 spriteTopLeft) =>
        (int)(spriteTopLeft.Y + RaisedBlockingHeightPixels + PlatformHeightPixels + 1);
}

