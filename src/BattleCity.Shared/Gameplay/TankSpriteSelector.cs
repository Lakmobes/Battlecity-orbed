namespace BattleCity.Shared.Gameplay;

/// <summary>
/// Tank atlas rows in <c>Sprites/Tanks.png</c> (48px legacy cells; 96px at 2× HD).
/// Layout: 16 facing columns × 5 role rows.
/// </summary>
public static class TankSpriteSelector
{
    public const int ColumnCount = 16;
    public const int RowCount = 5;

    public const int TeamRegularRow = 0;
    public const int TeamMayorRow = 1;
    public const int EnemyRegularRow = 2;
    public const int EnemyMayorRow = 3;
    public const int AdminRow = 4;

    public static bool IsAdminAccount(string? username) =>
        string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the sheet row index (not pixel Y).</summary>
    public static int GetSourceY(
        int observerCityId,
        int playerCityId,
        bool isMayor,
        bool isAdmin = false)
    {
        if (isAdmin)
        {
            return AdminRow;
        }

        var isFriendly = playerCityId == observerCityId;
        return (isFriendly, isMayor) switch
        {
            (true, false) => TeamRegularRow,
            (true, true) => TeamMayorRow,
            (false, false) => EnemyRegularRow,
            (false, true) => EnemyMayorRow,
        };
    }
}
