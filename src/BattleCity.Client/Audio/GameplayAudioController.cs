using BattleCity.Core.Audio;

using Microsoft.Xna.Framework;

namespace BattleCity.Client.Audio;

public sealed class GameplayAudioController
{
    private readonly AudioService _audio;

    public GameplayAudioController(AudioService audio)
    {
        _audio = audio;
    }

    public void PlaySimulationEvents(GameSoundEvent[] events, Vector2 listenerWorldCenter)
    {
        foreach (var gameEvent in events)
        {
            if (gameEvent.WorldPosition is { } position)
            {
                _audio.PlayAt(
                    gameEvent.Sound,
                    listenerWorldCenter.X,
                    listenerWorldCenter.Y,
                    position.X,
                    position.Y);
            }
            else
            {
                _audio.Play(gameEvent.Sound);
            }
        }
    }
}
