namespace BattleCity.Core.Ecs.Components;

public struct Collider
{
    public int OffsetX;
    public int OffsetY;
    public int Width;
    public int Height;
    public CollisionLayer Layer;
}
