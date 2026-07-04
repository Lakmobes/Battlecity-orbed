using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Rendering;

using Xunit;

using NumericsVector2 = System.Numerics.Vector2;

namespace BattleCity.Core.Tests;

public class EntityDrawSorterTests
{
    [Fact]
    public void SortOrdersByBottomEdgeAscending()
    {
        var drawables = new List<DrawableEntity>
        {
            Create(100, 48, 200),
            Create(50, 48, 100),
            Create(75, 48, 150),
        };

        EntityDrawSorter.Sort(drawables);

        Assert.Equal(98, drawables[0].SortDepth);
        Assert.Equal(123, drawables[1].SortDepth);
        Assert.Equal(148, drawables[2].SortDepth);
    }

    private static DrawableEntity Create(float y, int height, float x)
    {
        var transform = new Transform2D { Position = new NumericsVector2(x, y) };
        var sprite = new SpriteRef { Width = 48, Height = height };
        return new DrawableEntity(DrawableEntity.ComputeSortDepth(in transform, in sprite), transform, sprite);
    }
}
