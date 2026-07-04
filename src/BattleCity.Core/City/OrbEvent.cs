using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;

namespace BattleCity.Core.City;

public readonly record struct OrbEvent(
    int VictimCityId,
    int AttackerCityId,
    uint VictimPoints,
    uint AttackerPoints);
