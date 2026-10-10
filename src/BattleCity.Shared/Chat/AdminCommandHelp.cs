namespace BattleCity.Shared.Chat;

/// <summary>In-game <c>/help</c> lines. Kept short so they fit the chat log.</summary>
public static class AdminCommandHelp
{
    public static IReadOnlyList<string> Lines { get; } =
    [
        "/help  /g msg  /kick Name  /ban Name  /unban user  /bans",
        "/warp Name  /summon Name  /city id|name  /spawn 0-11|name",
        "/shutdown  /news  /setnews text  /startcity id|name",
        "/account user  /editaccount user points=N deaths=N admin=0|1",
        "/load name   (mayor, or admin)",
    ];
}
