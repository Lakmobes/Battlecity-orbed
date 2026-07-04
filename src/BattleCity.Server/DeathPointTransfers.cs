namespace BattleCity.Server;

public static class DeathPointTransfers
{
    public const int PointTransferThreshold = 100;
    public const int PointTransferAmount = 2;

    public static void Apply(
        ClientSession victim,
        byte killerCityId,
        IEnumerable<ClientSession> inGameSessions,
        Action<ClientSession, int> onPointsChanged)
    {
        onPointsChanged(victim, 0);

        if (victim.Points <= PointTransferThreshold || killerCityId == victim.CityId)
        {
            return;
        }

        victim.Points -= PointTransferAmount;
        onPointsChanged(victim, -PointTransferAmount);

        foreach (var ally in inGameSessions.Where(session => session.CityId == killerCityId))
        {
            ally.Points += PointTransferAmount;
            onPointsChanged(ally, PointTransferAmount);
        }
    }
}
