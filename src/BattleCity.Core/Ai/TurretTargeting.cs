using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Core.Gameplay;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.Ai;

public static class TurretTargeting
{
    private static readonly QueryDescription TankQuery =
        new QueryDescription().WithAll<Transform2D, CityAffiliation, Health, TankStatus>();

    public static bool TryFindNearestEnemy(
        World world,
        int ownerCityId,
        Vector2 turretWorldCenter,
        float maxRangePixels,
        out Entity target,
        out Vector2 targetCenter)
    {
        target = default;
        targetCenter = Vector2.Zero;
        var bestDistance = maxRangePixels;
        var found = false;
        var bestTarget = target;
        var bestCenter = targetCenter;

        world.Query(
            in TankQuery,
            (Entity entity, ref Transform2D transform, ref CityAffiliation city, ref Health health, ref TankStatus status) =>
            {
                if (city.CityId == ownerCityId || health.Current <= 0 || status.IsCloaked)
                {
                    return;
                }

                var center = GetTankCenter(transform.Position);
                var distance = Vector2.Distance(turretWorldCenter, center);
                if (distance >= bestDistance)
                {
                    return;
                }

                bestDistance = distance;
                bestTarget = entity;
                bestCenter = center;
                found = true;
            });

        target = bestTarget;
        targetCenter = bestCenter;
        return found;
    }

    public static float ComputeAimAngleDegrees(int gridX, int gridY, Vector2 targetWorldCenter)
    {
        var targetGridX = targetWorldCenter.X / GameConstants.TileSize;
        var targetGridY = targetWorldCenter.Y / GameConstants.TileSize;
        var deltaX = targetGridX - gridX;
        var deltaY = targetGridY - gridY;

        if (MathF.Abs(deltaY) < 0.001f)
        {
            return targetGridX < gridX ? 270f : 90f;
        }

        var angle = MathF.Atan(deltaX / deltaY) * (180f / MathF.PI);

        if (targetGridX < gridX)
        {
            angle = targetGridY < gridY ? 180f - angle : -angle;
        }
        else
        {
            angle = targetGridY < gridY ? 180f - angle : 360f - angle;
        }

        return angle;
    }

    public static int AngleDegreesToLegacyDirection(float angleDegrees)
    {
        var tmpAngle = (int)(angleDegrees / 1.125f);
        if (tmpAngle % 10 >= 5)
        {
            tmpAngle += 10;
        }

        tmpAngle /= 10;
        tmpAngle %= TankFacing.DirectionCount;
        if (tmpAngle < 0)
        {
            tmpAngle += TankFacing.DirectionCount;
        }

        return tmpAngle;
    }

    public static int AngleDegreesToHeadOrientation(float angleDegrees)
    {
        // Point the head where the bullet will travel. TurretHead art is compass-ordered
        // (column 0 = north) while legacy aim 0 fires south — same half-turn offset tanks use.
        var fireDirection = AngleDegreesToLegacyDirection(angleDegrees);
        return (fireDirection / 2 + 8) % 16;
    }

    public static Vector2 GetTurretWorldCenter(int gridX, int gridY) =>
        new(
            gridX * GameConstants.TileSize + GameConstants.TileSize / 2f,
            gridY * GameConstants.TileSize + GameConstants.TileSize / 2f);

    /// <summary>
    /// Muzzle origin for turret fire. Legacy <c>CItem.cpp</c> used <c>grid*48-24</c>, but that
    /// formula assumes a tank-style center position; item grid coords are already sprite
    /// top-left (same as remake tanks), so use the shared tank muzzle pivot instead.
    /// </summary>
    public static Vector2 GetTurretMuzzlePosition(int gridX, int gridY, int direction)
    {
        var topLeft = new Vector2(
            gridX * GameConstants.TileSize,
            gridY * GameConstants.TileSize);
        return WeaponGeometry.GetMuzzleWorldPosition(topLeft, direction);
    }

    public static Vector2 GetTankCenter(Vector2 tankTopLeft) =>
        new(
            tankTopLeft.X + GameConstants.TileSize / 2f,
            tankTopLeft.Y + GameConstants.TileSize / 2f);

    public static int WorldPositionToLegacyDirection(Vector2 from, Vector2 to)
    {
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        if (MathF.Abs(deltaX) < 0.001f && MathF.Abs(deltaY) < 0.001f)
        {
            return 0;
        }

        // Return sprite facing so movement via ToTravelDirection points toward the target.
        var travelDirection = ComputeTravelDirection(from, to);
        return InputSystem.ToSpriteFacing(travelDirection);
    }

    public static int ComputeTravelDirection(Vector2 from, Vector2 to)
    {
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        if (MathF.Abs(deltaX) < 0.001f && MathF.Abs(deltaY) < 0.001f)
        {
            return 0;
        }

        var fDir = MathF.Atan2(deltaX, deltaY);
        var direction = 32f - fDir * 16f / MathF.PI;
        var rounded = (int)MathF.Round(direction) % TankFacing.DirectionCount;
        if (rounded < 0)
        {
            rounded += TankFacing.DirectionCount;
        }

        return rounded;
    }

    public static int DirectionDifference(int a, int b)
    {
        var diff = Math.Abs(a - b) % TankFacing.DirectionCount;
        return Math.Min(diff, TankFacing.DirectionCount - diff);
    }
}
