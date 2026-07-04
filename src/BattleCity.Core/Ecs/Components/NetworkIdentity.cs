namespace BattleCity.Core.Ecs.Components;

/// <summary>Marks a player entity owned by a networked session.</summary>
public struct NetworkIdentity
{
    public byte PlayerId;
}
