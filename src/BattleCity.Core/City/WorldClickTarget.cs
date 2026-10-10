using System.Numerics;

using Arch.Core;

using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Constants;

namespace BattleCity.Core.City;

/// <summary>Right-click hit tests for tanks and buildings (legacy CInput map clicks).</summary>
public static class WorldClickTarget
{
    private static readonly QueryDescription NetworkPlayers = new QueryDescription()
        .WithAll<Transform2D, NetworkIdentity>();

    private static readonly QueryDescription LocalTanks = new QueryDescription()
        .WithAll<Transform2D, InputControlled>();

    private static readonly QueryDescription Buildings = new QueryDescription()
        .WithAll<BuildingRef>();

    public static bool TryFindPlayer(
        World world,
        float worldX,
        float worldY,
        out byte playerId,
        out bool isLocalTank)
    {
        var chosenId = (byte)0;
        var chosenLocal = false;
        var bestDistance = float.MaxValue;
        var found = false;

        void Consider(Vector2 topLeft, byte id, bool local)
        {
            if (worldX < topLeft.X
                || worldY < topLeft.Y
                || worldX >= topLeft.X + GameConstants.TileSize
                || worldY >= topLeft.Y + GameConstants.TileSize)
            {
                return;
            }

            var centerX = topLeft.X + (GameConstants.TileSize / 2f);
            var centerY = topLeft.Y + (GameConstants.TileSize / 2f);
            var distance = ((worldX - centerX) * (worldX - centerX))
                + ((worldY - centerY) * (worldY - centerY));
            if (distance >= bestDistance)
            {
                return;
            }

            bestDistance = distance;
            chosenId = id;
            chosenLocal = local;
            found = true;
        }

        world.Query(
            in NetworkPlayers,
            (ref Transform2D transform, ref NetworkIdentity identity) =>
                Consider(transform.Position, identity.PlayerId, local: false));

        world.Query(
            in LocalTanks,
            (Entity entity, ref Transform2D transform) =>
            {
                if (world.Has<NetworkIdentity>(entity))
                {
                    return;
                }

                Consider(transform.Position, 0, local: true);
            });

        playerId = chosenId;
        isLocalTank = chosenLocal;
        return found;
    }

    public static bool TryFindBuildingCity(World world, float worldX, float worldY, out int cityId)
    {
        cityId = 0;
        var tileX = (int)MathF.Floor(worldX / GameConstants.TileSize);
        var tileY = (int)MathF.Floor(worldY / GameConstants.TileSize);
        var found = false;
        var foundCity = 0;

        world.Query(
            in Buildings,
            (ref BuildingRef building) =>
            {
                if (found)
                {
                    return;
                }

                if (tileX < building.GridAnchorX - GameConstants.BuildingCollisionOffset
                    || tileX > building.GridAnchorX
                    || tileY < building.GridAnchorY - GameConstants.BuildingCollisionOffset
                    || tileY > building.GridAnchorY)
                {
                    return;
                }

                foundCity = building.CityId;
                found = true;
            });

        cityId = foundCity;
        return found;
    }
}
