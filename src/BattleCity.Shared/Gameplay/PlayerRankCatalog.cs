namespace BattleCity.Shared.Gameplay;

public static class PlayerRankCatalog
{
    public static string GetRank(int points) =>
        points switch
        {
            < 100 => "Private",
            < 200 => "Corporal",
            < 500 => "Sergeant",
            < 1000 => "Sergeant Major",
            < 2000 => "Lieutenant",
            < 4000 => "Captain",
            < 8000 => "Major",
            < 16000 => "Colonel",
            < 30000 => "Brigadier",
            < 45000 => "General",
            < 60000 => "Baron",
            < 80000 => "Earl",
            < 100000 => "Count",
            < 125000 => "Duke",
            < 150000 => "Archduke",
            < 200000 => "Grand Duke",
            < 250000 => "Lord",
            < 300000 => "Chancellor",
            < 350000 => "Royaume",
            < 400000 => "Emperor",
            < 500000 => "Auror",
            _ => "King",
        };

    public static string FormatChatName(string displayName, uint points, bool isAdmin = false) =>
        isAdmin ? $"Admin {displayName}" : $"{GetRank((int)points)} {displayName}";
}
