namespace BattleCity.Shared.Data;

/// <summary>Legacy bullet types from <c>CBullet.cpp</c> (not the same as <see cref="ItemType"/>).</summary>
public enum BulletKind : byte
{
    Laser = 0,
    Rocket = 1,
    Plasma = 2,
    Flare = 3,
}
