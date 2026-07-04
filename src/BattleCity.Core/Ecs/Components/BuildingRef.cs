namespace BattleCity.Core.Ecs.Components;

/// <summary>Legacy building metadata for a placed structure.</summary>
public struct BuildingRef
{
    public int MenuIndex;
    public int TypeCode;
    public int GridAnchorX;
    public int GridAnchorY;

    /// <summary>Legacy network building id (<c>sSMBuild.id</c>); 0 until assigned.</summary>
    public ushort NetworkId;
}
