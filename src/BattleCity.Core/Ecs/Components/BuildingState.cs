namespace BattleCity.Core.Ecs.Components;

/// <summary>Legacy building population and production state.</summary>
public struct BuildingState
{
    public int Population;
    public int ItemsLeft;

    /// <summary>0..5; sheet column is <c>AnimationFrame / 2</c> (legacy CBuilding).</summary>
    public int AnimationFrame;

    public float AnimationCooldownSeconds;

    /// <summary>
    /// Non-house: network id of the house staffing this building (0 = unattached).
    /// House: unused (slots live in <see cref="AttachedBuildingNetworkId1"/> / 2).
    /// </summary>
    public ushort AttachedHouseNetworkId;

    /// <summary>House slot 1: attached research/factory/hospital network id.</summary>
    public ushort AttachedBuildingNetworkId1;

    /// <summary>House slot 2: attached research/factory/hospital network id.</summary>
    public ushort AttachedBuildingNetworkId2;
}
