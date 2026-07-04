using System.Numerics;

namespace BattleCity.Client.Rendering;

/// <summary>8-way compass arrow index for the interface panel (legacy CDrawing::DrawArrow).</summary>
public static class CompassArrowHelper
{
    public static string GetDirectionGlyph(Vector2 playerWorldCenter, Vector2 cityWorldCenter)
    {
        var index = ComputeArrowIndex(playerWorldCenter, cityWorldCenter);
        return index switch
        {
            0 => "E",
            1 => "SE",
            2 => "S",
            3 => "SW",
            4 => "W",
            5 => "NW",
            6 => "N",
            _ => "NE",
        };
    }

    public static int ComputeArrowIndex(Vector2 playerWorldCenter, Vector2 cityWorldCenter)
    {
        var difX = cityWorldCenter.X - playerWorldCenter.X;
        var difY = cityWorldCenter.Y - playerWorldCenter.Y;

        if (MathF.Abs(difX) < 1f)
        {
            difX = difX >= 0 ? 1f : -1f;
        }

        if (MathF.Abs(difY) < 1f)
        {
            difY = difY >= 0 ? 1f : -1f;
        }

        var absX = MathF.Abs(difX);
        var absY = MathF.Abs(difY);

        if (playerWorldCenter.X <= cityWorldCenter.X)
        {
            if (playerWorldCenter.Y >= cityWorldCenter.Y)
            {
                if (absX / absY > 2f)
                {
                    return 4;
                }

                if (absY / absX > 2f)
                {
                    return 6;
                }

                return 5;
            }

            if (absX / absY > 2f)
            {
                return 4;
            }

            if (absY / absX > 2f)
            {
                return 2;
            }

            return 3;
        }

        if (playerWorldCenter.Y >= cityWorldCenter.Y)
        {
            if (absX / absY > 2f)
            {
                return 0;
            }

            if (absY / absX > 2f)
            {
                return 6;
            }

            return 7;
        }

        if (absX / absY > 2f)
        {
            return 0;
        }

        if (absY / absX > 2f)
        {
            return 2;
        }

        return 1;
    }
}
