using System.Numerics;

using BattleCity.Core.Ecs.Systems;

namespace BattleCity.Core.Gameplay;

public static class WeaponGeometry
{
    /// <summary>
    /// Muzzle world position from tank sprite top-left and legacy travel direction.
    /// Legacy pivot is (+6, +10) inside the 48x48 sprite, extended 20px along aim
    /// (see legacy/client/CInput.cpp FlashX/FlashY with Player X/Y as top-left).
    /// </summary>
    public static Vector2 GetMuzzleWorldPosition(Vector2 tankTopLeft, int travelDirection)
    {
        var offset = GetMuzzleOffset(travelDirection);
        return tankTopLeft + offset;
    }

    public static Vector2 GetMuzzleOffset(int travelDirection)
    {
        var radians = InputSystem.LegacyDirectionToRadians(travelDirection);
        return new Vector2(
            6f + MathF.Sin(radians) * 20f,
            10f + MathF.Cos(radians) * 20f);
    }
}
