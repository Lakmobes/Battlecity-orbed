using System.Numerics;

namespace BattleCity.Client.Rendering;

/// <summary>Compass direction toward the home command center (legacy CDrawing::DrawArrow).</summary>
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
