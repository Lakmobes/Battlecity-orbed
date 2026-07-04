using BattleCity.Core.Ecs.Components;

namespace BattleCity.Core.Ecs.Rendering;

public readonly struct DrawableEntity(int sortDepth, Transform2D transform, SpriteRef sprite)
    : IComparable<DrawableEntity>
{
    public int SortDepth { get; } = sortDepth;
    public Transform2D Transform { get; } = transform;
    public SpriteRef Sprite { get; } = sprite;

    public static int ComputeSortDepth(in Transform2D transform, in SpriteRef sprite) =>
        (int)transform.Position.Y + sprite.Height;

    public int CompareTo(DrawableEntity other) => SortDepth.CompareTo(other.SortDepth);
}

public static class EntityDrawSorter
{
    public static void Sort(List<DrawableEntity> drawables) => drawables.Sort();
}
