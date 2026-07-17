using Arch.Core;

using BattleCity.Core.Ecs.Components;

namespace BattleCity.Core.Gameplay;

public static class EntityCityLookup
{
    public const byte UnknownCity = byte.MaxValue;

    public static byte GetCityId(World world, Entity entity)
    {
        // Bullet owners can be destroyed before the bullet resolves.
        if (!world.IsAlive(entity) || !world.Has<CityAffiliation>(entity))
        {
            return UnknownCity;
        }

        return (byte)Math.Clamp(world.Get<CityAffiliation>(entity).CityId, 0, byte.MaxValue - 1);
    }
}
