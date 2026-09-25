namespace BattleCity.Shared.Gameplay;

public static class PromotionMessages
{
    public static string Format(string playerName, string rank) =>
        $"{playerName} has been promoted to {rank}!";
}
