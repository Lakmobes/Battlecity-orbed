namespace BattleCity.Shared.Gameplay;

/// <summary>Legacy tank sheet rows in Sprites/Tanks.png (48px per row).</summary>
public static class TankSpriteSelector
{
    public const int FriendCommandoRow = 0;
    public const int FriendMayorRow = 1;
    public const int EnemyCommandoRow = 2;
    public const int EnemyMayorRow = 3;

    public static int GetSourceY(int observerCityId, int playerCityId, bool isMayor)
    {
        var isFriendly = playerCityId == observerCityId;
        return (isFriendly, isMayor) switch
        {
            (true, false) => FriendCommandoRow,
            (true, true) => FriendMayorRow,
            (false, false) => EnemyCommandoRow,
            (false, true) => EnemyMayorRow,
        };
    }
}
