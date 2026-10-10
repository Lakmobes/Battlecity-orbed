namespace BattleCity.Shared.Gameplay;

/// <summary>Lines for the right-click player and city panels (legacy PanelLine1–7).</summary>
public static class InspectPanelText
{
    public static string[] FormatPlayer(
        string name,
        int points,
        int monthlyPoints,
        int orbs,
        int assists,
        int deaths)
    {
        var deathsForRatio = deaths == 0 ? 1 : deaths;
        return
        [
            name,
            $"Points:  {points}",
            $"Orbs:    {orbs}",
            $"Assists: {assists}",
            $"Deaths:  {deaths}",
            $"Pts/Mon: {monthlyPoints}",
            $"Pts/Death: {points / deathsForRatio}",
        ];
    }

    public static string[] FormatCity(
        string cityName,
        string? mayorName,
        int players,
        int buildings,
        bool isOrbable,
        int orbs,
        int orbPoints,
        int uptimeMinutes)
    {
        var lines = new List<string>
        {
            cityName,
            $"Mayor:   {(string.IsNullOrWhiteSpace(mayorName) ? "(none)" : mayorName)}",
            $"Players: {players}",
            $"Size:    {buildings} {(buildings == 1 ? "building" : "buildings")}",
        };

        if (isOrbable)
        {
            var hours = Math.Max(0, uptimeMinutes) / 60;
            var minutes = Math.Max(0, uptimeMinutes) % 60;
            lines.Add($"Bounty:  {orbPoints} points");
            lines.Add($"Orbs:    {orbs}");
            lines.Add($"Uptime:  {hours}h {minutes}m");
        }

        return lines.ToArray();
    }
}
