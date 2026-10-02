namespace BattleCity.Server;

/// <summary>
/// Legacy <c>CCity::DestructTimer</c>. An orbable city with no mayor is removed after the timer.
/// </summary>
public sealed class CityDestructSchedule
{
    private readonly Dictionary<byte, float> _remainingSeconds = new();

    public void Schedule(byte cityId, float seconds) =>
        _remainingSeconds[cityId] = Math.Max(0f, seconds);

    public void Cancel(byte cityId) => _remainingSeconds.Remove(cityId);

    public bool IsScheduled(byte cityId) => _remainingSeconds.ContainsKey(cityId);

    public List<byte> Tick(float deltaSeconds)
    {
        var due = new List<byte>();
        if (deltaSeconds <= 0f || _remainingSeconds.Count == 0)
        {
            return due;
        }

        foreach (var cityId in _remainingSeconds.Keys.ToList())
        {
            var left = _remainingSeconds[cityId] - deltaSeconds;
            if (left <= 0f)
            {
                _remainingSeconds.Remove(cityId);
                due.Add(cityId);
            }
            else
            {
                _remainingSeconds[cityId] = left;
            }
        }

        return due;
    }
}
