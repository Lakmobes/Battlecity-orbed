using BattleCity.Shared.Audio;
using BattleCity.Shared.Data;

using Xunit;

namespace BattleCity.Shared.Tests;

public class SoundCatalogTests
{
    [Theory]
    [InlineData(SoundId.Laser, "laser.wav", "Audio/laser")]
    [InlineData(SoundId.Fire, "fire.wav", "Audio/fire")]
    [InlineData(SoundId.Engine, "engine.wav", "Audio/engine")]
    [InlineData(SoundId.Explode, "explode.wav", "Audio/explode")]
    [InlineData(SoundId.BigTurret, "bturr.wav", "Audio/bturr")]
    [InlineData(SoundId.Cloak, "cloak.wav", "Audio/cloak")]
    [InlineData(SoundId.Flare, "flare.wav", "Audio/flare")]
    public void MapsLegacySoundIdsToContentPaths(SoundId soundId, string legacyFile, string contentPath)
    {
        Assert.Equal(legacyFile, SoundCatalog.GetLegacyFileName(soundId));
        Assert.Equal(contentPath, SoundCatalog.GetContentPath(soundId));
    }

    [Fact]
    public void EverySoundIdHasContentPath()
    {
        foreach (SoundId soundId in Enum.GetValues<SoundId>())
        {
            var path = SoundCatalog.GetContentPath(soundId);
            Assert.StartsWith("Audio/", path);
            Assert.DoesNotContain('.', path);
        }
    }
}
