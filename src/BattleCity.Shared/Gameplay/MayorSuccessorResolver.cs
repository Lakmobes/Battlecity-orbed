namespace BattleCity.Shared.Gameplay;

/// <summary>
/// Legacy mayor succession (<c>CPlayer</c> leave / <c>cmSuccessor</c>): prefer a designated
/// in-city teammate, otherwise the first other in-game player in the city.
/// </summary>
public static class MayorSuccessorResolver
{
    public static bool TryResolve(
        byte cityId,
        byte excludedPlayerId,
        byte? designatedSuccessorId,
        IEnumerable<(byte PlayerId, byte CityId)> inGamePlayers,
        out byte successorPlayerId)
    {
        successorPlayerId = 0;

        if (designatedSuccessorId is { } designated
            && designated != excludedPlayerId)
        {
            foreach (var (playerId, playerCityId) in inGamePlayers)
            {
                if (playerId == designated && playerCityId == cityId)
                {
                    successorPlayerId = playerId;
                    return true;
                }
            }
        }

        foreach (var (playerId, playerCityId) in inGamePlayers)
        {
            if (playerId == excludedPlayerId || playerCityId != cityId)
            {
                continue;
            }

            successorPlayerId = playerId;
            return true;
        }

        return false;
    }
}
