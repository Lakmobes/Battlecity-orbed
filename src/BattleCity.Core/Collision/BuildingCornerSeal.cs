using System.Numerics;

namespace BattleCity.Core.Collision;

/// <summary>
/// Stops bullets that would slip through the point where two building corners meet.
/// Edge-to-edge neighbors are already solid; only a diagonal corner touch is sealed.
/// </summary>
public static class BuildingCornerSeal
{
    public const float TouchEpsilon = 2f;
    public const float SealSize = 16f;

    public static bool BlocksSegment(
        IReadOnlyList<AxisAlignedBox> buildings,
        in AxisAlignedBox bulletBounds,
        in AxisAlignedBox previousBounds,
        Vector2 from,
        Vector2 to)
    {
        for (var i = 0; i < buildings.Count; i++)
        {
            for (var j = i + 1; j < buildings.Count; j++)
            {
                if (!TryGetSharedCorner(buildings[i], buildings[j], out var corner))
                {
                    continue;
                }

                var seal = new AxisAlignedBox(
                    corner.X - (SealSize / 2f),
                    corner.Y - (SealSize / 2f),
                    SealSize,
                    SealSize);
                if (seal.Intersects(bulletBounds)
                    || seal.Intersects(previousBounds)
                    || seal.TryGetSegmentEntryPoint(from, to) is not null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool TryGetSharedCorner(in AxisAlignedBox a, in AxisAlignedBox b, out Vector2 corner)
    {
        corner = default;
        var overlapX = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
        var overlapY = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
        if (overlapX < -TouchEpsilon || overlapX > TouchEpsilon
            || overlapY < -TouchEpsilon || overlapY > TouchEpsilon)
        {
            return false;
        }

        var sharedX = Math.Abs(a.Right - b.Left) <= TouchEpsilon
            ? a.Right
            : Math.Abs(b.Right - a.Left) <= TouchEpsilon
                ? b.Right
                : (Math.Min(a.Right, b.Right) + Math.Max(a.Left, b.Left)) / 2f;
        var sharedY = Math.Abs(a.Bottom - b.Top) <= TouchEpsilon
            ? a.Bottom
            : Math.Abs(b.Bottom - a.Top) <= TouchEpsilon
                ? b.Bottom
                : (Math.Min(a.Bottom, b.Bottom) + Math.Max(a.Top, b.Top)) / 2f;
        corner = new Vector2(sharedX, sharedY);
        return true;
    }
}
