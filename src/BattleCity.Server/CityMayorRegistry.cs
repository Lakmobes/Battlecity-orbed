namespace BattleCity.Server;

public sealed class CityMayorRegistry
{
    private readonly Dictionary<byte, byte> _mayorByCity = new();

    public bool HasMayor(byte cityId) => _mayorByCity.ContainsKey(cityId);

    public bool TryGetMayorPlayerId(byte cityId, out byte playerId) =>
        _mayorByCity.TryGetValue(cityId, out playerId);

    public bool IsMayor(byte cityId, byte playerId) =>
        _mayorByCity.TryGetValue(cityId, out var mayorId) && mayorId == playerId;

    public void Assign(byte cityId, byte playerId) => _mayorByCity[cityId] = playerId;

    public IEnumerable<byte> GetMayoredCityIds() => _mayorByCity.Keys;

    public void Remove(byte cityId, byte playerId)
    {
        if (_mayorByCity.TryGetValue(cityId, out var mayorId) && mayorId == playerId)
        {
            _mayorByCity.Remove(cityId);
        }
    }
}
