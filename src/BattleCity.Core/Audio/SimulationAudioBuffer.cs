using System.Numerics;

using BattleCity.Shared.Data;

namespace BattleCity.Core.Audio;

public readonly struct GameSoundEvent(SoundId sound, Vector2? worldPosition = null)
{
    public SoundId Sound { get; } = sound;
    public Vector2? WorldPosition { get; } = worldPosition;
}

public sealed class SimulationAudioBuffer
{
    private readonly List<GameSoundEvent> _events = new(capacity: 16);

    public void Play(SoundId sound, Vector2? worldPosition = null) =>
        _events.Add(new GameSoundEvent(sound, worldPosition));

    public void Clear() => _events.Clear();

    public GameSoundEvent[] Drain()
    {
        if (_events.Count == 0)
        {
            return Array.Empty<GameSoundEvent>();
        }

        var drained = _events.ToArray();
        _events.Clear();
        return drained;
    }
}
