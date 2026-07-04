using Arch.Core;

using BattleCity.Core.Ecs.Components;

namespace BattleCity.Core.Gameplay;

public static class EntityCityLookup
{
    public const byte UnknownCity = byte.MaxValue;

    public static byte GetCityId(World world, Entity entity) =>
        world.Has<CityAffiliation>(entity)
            ? (byte)world.Get<CityAffiliation>(entity).CityId
            : UnknownCity;
}
