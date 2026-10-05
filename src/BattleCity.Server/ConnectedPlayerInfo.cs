namespace BattleCity.Server;

public readonly record struct ConnectedPlayerInfo(
    byte PlayerId,
    string DisplayName,
    string State,
    byte CityId,
    string CityName,
    int CitySize,
    int Points,
    bool IsAdmin,
    bool IsMayor,
    bool IsGuest);
