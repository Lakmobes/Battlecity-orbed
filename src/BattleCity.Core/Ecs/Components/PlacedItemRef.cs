using BattleCity.Shared.Data;

namespace BattleCity.Core.Ecs.Components;

public struct PlacedItemRef
{
    public ItemType Type;
    public int GridX;
    public int GridY;
    public bool Active;
    public int CityId;
    public float FuseTimerSeconds;

    /// <summary>Player who dropped this item. 255 means unknown (factory or map spawn).</summary>
    public byte OwnerPlayerId;
}
