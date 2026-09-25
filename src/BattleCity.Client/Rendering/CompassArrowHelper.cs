using System.Numerics;

namespace BattleCity.Client.Rendering;

/// <summary>Compass direction toward the home drive-platform reference (spawn pad center).</summary>
public static class CompassArrowHelper
{
    public static string GetDirectionGlyph(Vector2 playerWorldCenter, Vector2 cityWorldCenter)
    {
        var index = ComputeArrowIndex(playerWorldCenter, cityWorldCenter);
        return index switch
        {
            0 => "N",
            1 => "NE",
            2 => "E",
            3 => "SE",
            4 => "S",
            5 => "SW",
            6 => "W",
            _ => "NW",
        };
    }

    /// <summary>
    /// Returns 0..7 where 0 is north, increasing clockwise (N, NE, E, SE, S, SW, W, NW).
    /// </summary>
    public static int ComputeArrowIndex(Vector2 playerWorldCenter, Vector2 cityWorldCenter)
    {
        var delta = cityWorldCenter - playerWorldCenter;
        if (delta.LengthSquared() < 1f)
        {
            return 0;
        }

        // Atan2(x, -y): 0 = north, positive clockwise in screen space (y down).
        var radians = MathF.Atan2(delta.X, -delta.Y);
        var sector = (int)MathF.Round(radians / (MathF.PI / 4f));
        return ((sector % 8) + 8) % 8;
    }

    /// <summary>
    /// Maps 0..7 (N→NW clockwise) to the source-X frame index in legacy <c>imgArrows.bmp</c>
    /// (frames E, NE, N, NW, W, SW, S, SE at 40×40).
    /// </summary>
    public static int ToLegacyArrowFrame(int arrowIndex) =>
        arrowIndex switch
        {
            0 => 2, // N
            1 => 1, // NE
            2 => 0, // E
            3 => 7, // SE
            4 => 6, // S
            5 => 5, // SW
            6 => 4, // W
            _ => 3, // NW
        };

    public static float RadiansFromArrowIndex(int arrowIndex) =>
        arrowIndex * (MathF.PI / 4f);

    public const int LegacyArrowFrameSize = 40;
    public const int LegacyArrowFrameCount = 8;

    public static float ComputeArrowRadians(Vector2 playerWorldCenter, Vector2 cityWorldCenter)
    {
        var delta = cityWorldCenter - playerWorldCenter;
        if (delta.LengthSquared() < 1f)
        {
            return 0f;
        }

        return MathF.Atan2(delta.X, -delta.Y);
    }
}
