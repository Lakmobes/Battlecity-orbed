using System.Numerics;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.Collision;

/// <summary>Pixel-space AABB matching legacy <c>CCollision::RectCollision</c>.</summary>
public readonly struct AxisAlignedBox
{
    public float Left { get; }
    public float Top { get; }
    public float Width { get; }
    public float Height { get; }

    public float Right => Left + Width;
    public float Bottom => Top + Height;

    public AxisAlignedBox(float left, float top, float width, float height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    public static AxisAlignedBox FromCollider(Vector2 position, in Collider collider) =>
        new(
            position.X + collider.OffsetX,
            position.Y + collider.OffsetY,
            collider.Width,
            collider.Height);

    public static AxisAlignedBox FromBuildingGrid(int gridX, int gridY)
    {
        var offset = GameConstants.BuildingCollisionOffset;
        var size = GameConstants.BuildingCollisionSize;
        return new AxisAlignedBox(
            (gridX - offset) * GameConstants.TileSize,
            (gridY - offset) * GameConstants.TileSize,
            size,
            size);
    }

    public bool Intersects(in AxisAlignedBox other)
    {
        var right = Right;
        var bottom = Bottom;
        var otherRight = other.Right;
        var otherBottom = other.Bottom;

        if (right < other.Left || otherRight < Left)
        {
            return false;
        }

        if (bottom < other.Top || otherBottom < Top)
        {
            return false;
        }

        return true;
    }

    public bool ContainsPoint(Vector2 point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;
}
