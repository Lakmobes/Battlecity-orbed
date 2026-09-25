namespace BattleCity.Shared.Network;

/// <summary>Legacy <c>sCMAdmin.command</c> / <c>sSMAdmin.command</c> values used by the remake.</summary>
public static class AdminCommands
{
    public const byte Kick = 1;
    public const byte JoinCity = 2;
    public const byte Warp = 3;
    public const byte Summon = 4;
    public const byte Ban = 5;
    public const byte Shutdown = 6;
    public const byte SpawnItem = 7;
    public const byte RequestBans = 8;
    public const byte Unban = 9;
    public const byte RequestNews = 10;
}
