using Arch.Core;

using BattleCity.Core.Ecs.Components;

namespace BattleCity.Core.Ecs.Systems;

public static class MovementSystem
{
    private static readonly QueryDescription AllQuery =
        new QueryDescription().WithAll<Transform2D, Velocity>();

    private static readonly QueryDescription NonBulletQuery =
        new QueryDescription().WithAll<Transform2D, Velocity>().WithNone<BulletRef>();

    private static readonly QueryDescription BulletQuery =
        new QueryDescription().WithAll<Transform2D, Velocity, BulletRef>();

    public static void Update(World world, float deltaSeconds) =>
        Apply(world, deltaSeconds, in AllQuery);

    public static void UpdateNonBullets(World world, float deltaSeconds) =>
        Apply(world, deltaSeconds, in NonBulletQuery);

    public static void UpdateBullets(World world, float deltaSeconds) =>
        Apply(world, deltaSeconds, in BulletQuery);

    private static void Apply(World world, float deltaSeconds, in QueryDescription query)
    {
        world.Query(
            in query,
            (ref Transform2D transform, ref Velocity velocity) =>
            {
                transform.PreviousPosition = transform.Position;
                transform.Position += velocity.Value * deltaSeconds;
            });
    }
}
