namespace BattleCity.Server;

public readonly record struct ConnectedPlayerInfo(
    byte PlayerId,
    string DisplayName,
    string State,
    byte CityId,
    bool IsAdmin,
    bool IsMayor,
    bool IsGuest);
