using BattleCity.Shared.Audio;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace BattleCity.Client.Audio;

public sealed class AudioService
{
    private readonly ContentManager _content;
    private readonly Dictionary<SoundId, SoundEffect> _effects = new();
    private readonly Dictionary<SoundId, SoundEffectInstance> _loops = new();
    private SoundEffectInstance? _engineLoop;
    private bool _enabled = true;
    private float _masterVolume = 1f;

    public AudioService(ContentManager content)
    {
        _content = content;
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
            {
                StopEngine();
            }
        }
    }

    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0f, 1f);
    }

    public void LoadContent()
    {
        foreach (SoundId sound in Enum.GetValues<SoundId>())
        {
            if (TryLoadWav(sound, out var fromWav))
            {
                _effects[sound] = fromWav;
                continue;
            }

            try
            {
                _effects[sound] = _content.Load<SoundEffect>(SoundCatalog.GetContentPath(sound));
            }
            catch (ContentLoadException)
            {
                // Missing assets are skipped; Play becomes a no-op for that sound.
            }
        }
    }

    public void Play(SoundId sound, float volume = 1f, float pan = 0f)
    {
        if (!_enabled || !_effects.TryGetValue(sound, out var effect))
        {
            return;
        }

        // MonoGame: Play(volume, pitch, pan) — pan must not go in the pitch slot.
        effect.Play(Math.Clamp(volume * _masterVolume, 0f, 1f), pitch: 0f, pan: Math.Clamp(pan, -1f, 1f));
    }

    public void PlayAt(SoundId sound, float listenerX, float listenerY, float worldX, float worldY, float volume = 1f)
    {
        var distance = MathF.Sqrt(
            ((worldX - listenerX) * (worldX - listenerX))
            + ((worldY - listenerY) * (worldY - listenerY)));
        var heard = ComputeDistanceVolume(distance);
        if (heard <= 0f)
        {
            return;
        }

        var pan = ComputePan(listenerX, worldX, distance);
        Play(sound, volume * heard, pan);
    }

    public void SetEngineRunning(bool running)
    {
        if (!_enabled)
        {
            StopEngine();
            return;
        }

        if (running)
        {
            if (_engineLoop is null && _effects.TryGetValue(SoundId.Engine, out var effect))
            {
                _engineLoop = effect.CreateInstance();
                _engineLoop.IsLooped = true;
                _engineLoop.Volume = 0.35f * _masterVolume;
                _engineLoop.Play();
            }
            else if (_engineLoop?.State == SoundState.Stopped)
            {
                _engineLoop.Play();
            }
        }
        else
        {
            StopEngine();
        }
    }

    public void StopEngine()
    {
        if (_engineLoop is null)
        {
            return;
        }

        _engineLoop.Stop();
        _engineLoop.Dispose();
        _engineLoop = null;
    }

    public void StopAll()
    {
        StopEngine();
        foreach (var loop in _loops.Values)
        {
            loop.Stop();
            loop.Dispose();
        }

        _loops.Clear();
    }

    private bool TryLoadWav(SoundId sound, out SoundEffect effect)
    {
        effect = null!;
        var fileName = SoundCatalog.GetLegacyFileName(sound);
        var path = Path.Combine(AppContext.BaseDirectory, _content.RootDirectory, "Audio", fileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var fileStream = File.OpenRead(path);
            effect = SoundEffect.FromStream(fileStream);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// On-screen (and just off) stays loud. Volume falls off out to the radar radius, then cuts out.
    /// </summary>
    internal static float ComputeDistanceVolume(float distance)
    {
        const float fullVolumeRadius = 1200f;
        const float maxRadius = ClientConstants.RadarSize;
        if (distance >= maxRadius)
        {
            return 0f;
        }

        if (distance <= fullVolumeRadius)
        {
            return 1f;
        }

        return 1f - ((distance - fullVolumeRadius) / (maxRadius - fullVolumeRadius));
    }

    private static float ComputePan(float listenerX, float worldX, float distance)
    {
        if (distance < 1f)
        {
            return 0f;
        }

        var delta = worldX - listenerX;
        return Math.Clamp(delta / 1200f, -1f, 1f);
    }
}
