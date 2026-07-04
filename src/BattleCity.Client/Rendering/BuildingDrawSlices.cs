using BattleCity.Core.Collision;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Rendering;
using BattleCity.Shared.Constants;

namespace BattleCity.Client.Rendering;

internal static class BuildingDrawSlices
{
    public static void AddDrawables(
        List<DrawableEntity> drawList,
        in Transform2D transform,
        in SpriteRef sprite,
        int typeCode)
    {
        if (!BuildingCollision.UsesRaisedPlatformCollision(typeCode))
        {
            drawList.Add(new DrawableEntity(
                DrawableEntity.ComputeSortDepth(in transform, in sprite),
                transform,
                sprite));
            return;
        }

        var topLeft = transform.Position;
        var platformHeight = BuildingCollision.PlatformHeightPixels;
        var structureHeight = BuildingCollision.RaisedBlockingHeightPixels;

        drawList.Add(new DrawableEntity(
            BuildingCollision.GetStructureSortDepth(topLeft),
            transform,
            SliceSprite(in sprite, 0, structureHeight)));

        var platformTransform = new Transform2D
        {
            Position = topLeft + new System.Numerics.Vector2(0, structureHeight),
            PreviousPosition = transform.PreviousPosition,
            RotationDegrees = transform.RotationDegrees,
        };

        drawList.Add(new DrawableEntity(
            BuildingCollision.GetPlatformSortDepth(topLeft),
            platformTransform,
            SliceSprite(in sprite, structureHeight, platformHeight)));
    }

    private static SpriteRef SliceSprite(in SpriteRef sprite, int sourceYOffset, int height) =>
        new()
        {
            TextureKey = sprite.TextureKey,
            SourceX = sprite.SourceX,
            SourceY = sprite.SourceY + sourceYOffset,
            Width = sprite.Width,
            Height = height,
        };
}
