namespace BattleCity.Shared.Chat;

public static class WhisperRecipientMatcher
{
    public static bool TryMatch(
        string recipientPrefix,
        byte localPlayerId,
        IEnumerable<(byte PlayerId, string DisplayName)> players,
        out byte recipientId,
        out string recipientName)
    {
        recipientId = 0;
        recipientName = string.Empty;

        if (string.IsNullOrWhiteSpace(recipientPrefix))
        {
            return false;
        }

        var normalizedPrefix = recipientPrefix.Trim().ToLowerInvariant();
        var matchCount = 0;

        foreach (var (playerId, displayName) in players)
        {
            if (playerId == localPlayerId || string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            if (!displayName.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            recipientId = playerId;
            recipientName = displayName;
            matchCount++;
        }

        return matchCount == 1;
    }
}
