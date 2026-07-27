using System.Numerics;

using Arch.Core;

using BattleCity.Core.Collision;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Levels;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.City;

public static class CommandCenterLookup
{
    private static readonly QueryDescription CommandCenterQuery =
        new QueryDescription().WithAll<Transform2D, BuildingRef>();

    public static bool TryGetWorldPosition(World world, out Vector2 position) =>
        TryGetWorldPosition(world, homeGridAnchorX: null, homeGridAnchorY: null, out position);

    public static bool TryGetWorldPosition(
        World world,
        int? homeGridAnchorX,
        int? homeGridAnchorY,
        out Vector2 position)
    {
        var found = false;
        var worldPosition = Vector2.Zero;

        world.Query(
            in CommandCenterQuery,
            (ref Transform2D transform, ref BuildingRef building) =>
            {
                if (found || !BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    return;
                }

                if (homeGridAnchorX is int homeX
                    && homeGridAnchorY is int homeY
                    && (building.GridAnchorX != homeX || building.GridAnchorY != homeY))
                {
                    return;
                }

                worldPosition = transform.Position
                    + new Vector2(GameConstants.TileSize * 1.5f, GameConstants.TileSize * 1.5f);
                found = true;
            });

        // Fallback: first CC if home anchors were requested but not matched yet.
        if (!found && homeGridAnchorX is not null)
        {
            return TryGetWorldPosition(world, null, null, out position);
        }

        position = worldPosition;
        return found;
    }

    public static Vector2 GridAnchorToWorldCenter(int gridAnchorX, int gridAnchorY)
    {
        var topLeft = BuildingPlacement.GridAnchorToWorldPosition(gridAnchorX, gridAnchorY);
        return topLeft + new Vector2(GameConstants.TileSize * 1.5f, GameConstants.TileSize * 1.5f);
    }

    /// <summary>Nearest command center that is not the home CC (orbable-city compass target).</summary>
    public static bool TryFindNearestOtherWorldPosition(
        World world,
        int homeGridAnchorX,
        int homeGridAnchorY,
        Vector2 fromWorldCenter,
        out Vector2 position,
        Func<int, bool>? cityIsOrbable = null)
    {
        var found = false;
        var bestDistance = float.MaxValue;
        var best = Vector2.Zero;

        world.Query(
            in CommandCenterQuery,
            (ref Transform2D transform, ref BuildingRef building) =>
            {
                if (!BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    return;
                }

                if (building.GridAnchorX == homeGridAnchorX && building.GridAnchorY == homeGridAnchorY)
                {
                    return;
                }

                if (cityIsOrbable is not null && !cityIsOrbable(building.CityId))
                {
                    return;
                }

                var center = transform.Position
                    + new Vector2(GameConstants.TileSize * 1.5f, GameConstants.TileSize * 1.5f);
                var distance = Vector2.DistanceSquared(fromWorldCenter, center);
                if (distance >= bestDistance)
                {
                    return;
                }

                bestDistance = distance;
                best = center;
                found = true;
            });

        position = best;
        return found;
    }

    /// <summary>
    /// Legacy CC respawn / join position: center of the southern drive row
    /// (<c>legacy/client/CCollision.cpp</c> — only the bottom tile row is drivable).
    /// </summary>
    public static Vector2 GetDrivePlatformSpawnPosition(Vector2 spriteTopLeft)
    {
        var platform = BuildingCollision.GetDrivePlatformBounds(spriteTopLeft);
        return new Vector2(
            platform.Left + (platform.Width - GameConstants.TileSize) / 2f,
            platform.Top + (platform.Height - GameConstants.TileSize) / 2f);
    }

    public static Vector2 GetRespawnPositionFromGridAnchor(int gridAnchorX, int gridAnchorY) =>
        GetDrivePlatformSpawnPosition(BuildingPlacement.GridAnchorToWorldPosition(gridAnchorX, gridAnchorY));

    public static bool TryGetRespawnPosition(World world, out Vector2 position) =>
        TryGetRespawnPosition(world, homeGridAnchorX: null, homeGridAnchorY: null, out position);

    public static bool TryGetRespawnPosition(
        World world,
        int homeGridAnchorX,
        int homeGridAnchorY,
        out Vector2 position) =>
        TryGetRespawnPosition(world, homeGridAnchorX, (int?)homeGridAnchorY, out position);

    public static bool TryGetRespawnPosition(
        World world,
        int? homeGridAnchorX,
        int? homeGridAnchorY,
        out Vector2 position)
    {
        var foundPosition = Vector2.Zero;
        var found = false;

        world.Query(
            in CommandCenterQuery,
            (ref Transform2D transform, ref BuildingRef building) =>
            {
                if (found || !BuildingCatalog.IsCommandCenter(building.TypeCode))
                {
                    return;
                }

                if (homeGridAnchorX is int homeX
                    && homeGridAnchorY is int homeY
                    && (building.GridAnchorX != homeX || building.GridAnchorY != homeY))
                {
                    return;
                }

                foundPosition = GetDrivePlatformSpawnPosition(transform.Position);
                found = true;
            });

        if (!found && homeGridAnchorX is not null)
        {
            position = GetRespawnPositionFromGridAnchor(homeGridAnchorX.Value, homeGridAnchorY!.Value);
            return true;
        }

        position = foundPosition;
        return found;
    }
}
