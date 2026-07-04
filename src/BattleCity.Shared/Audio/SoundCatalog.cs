using BattleCity.Shared.Data;

namespace BattleCity.Shared.Audio;

/// <summary>Legacy WAV filenames and MonoGame content paths (legacy/client/CSound.cpp).</summary>
public static class SoundCatalog
{
    public static string GetLegacyFileName(SoundId soundId) =>
        soundId switch
        {
            SoundId.Laser => "laser.wav",
            SoundId.Fire => "fire.wav",
            SoundId.Engine => "engine.wav",
            SoundId.Build => "build.wav",
            SoundId.Die => "die.wav",
            SoundId.Explode => "explode.wav",
            SoundId.TurretFire => "turretfire.wav",
            SoundId.Buzz => "buzz.wav",
            SoundId.Click => "click.wav",
            SoundId.BigTurret => "bturr.wav",
            SoundId.Demolish => "demo.wav",
            SoundId.Screech => "screech.wav",
            SoundId.Hit => "hit.wav",
            SoundId.Cloak => "cloak.wav",
            SoundId.Flare => "flare.wav",
            _ => "click.wav",
        };

    public static string GetContentPath(SoundId soundId)
    {
        var fileName = Path.GetFileNameWithoutExtension(GetLegacyFileName(soundId));
        return $"Audio/{fileName}";
    }
}
