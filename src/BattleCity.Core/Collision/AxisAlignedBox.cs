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

    /// <summary>
    /// Nearest point on the AABB surface (or interior) to <paramref name="point"/>.
    /// Used to explode bullets at the building face instead of deep inside.
    /// </summary>
    public Vector2 ClosestPoint(Vector2 point) =>
        new(
            Math.Clamp(point.X, Left, Right),
            Math.Clamp(point.Y, Top, Bottom));

    /// <summary>
    /// First intersection of the segment <paramref name="from"/> → <paramref name="to"/>
    /// with this AABB, or null if the segment never enters.
    /// </summary>
    public Vector2? TryGetSegmentEntryPoint(Vector2 from, Vector2 to)
    {
        if (ContainsPoint(from))
        {
            return ClosestPoint(from);
        }

        var delta = to - from;
        var tMin = 0f;
        var tMax = 1f;

        if (!ClipAxis(from.X, delta.X, Left, Right, ref tMin, ref tMax)
            || !ClipAxis(from.Y, delta.Y, Top, Bottom, ref tMin, ref tMax))
        {
            return null;
        }

        if (tMin > 1f || tMax < 0f)
        {
            return null;
        }

        var t = Math.Clamp(tMin, 0f, 1f);
        return from + delta * t;
    }

    private static bool ClipAxis(
        float origin,
        float direction,
        float min,
        float max,
        ref float tMin,
        ref float tMax)
    {
        const float epsilon = 0.0001f;
        if (MathF.Abs(direction) < epsilon)
        {
            return origin >= min && origin <= max;
        }

        var inv = 1f / direction;
        var t1 = (min - origin) * inv;
        var t2 = (max - origin) * inv;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);
        return tMin <= tMax;
    }
}
