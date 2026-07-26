using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>
/// Maps higher-resolution PNG sheets onto legacy 48px world space (client render only).
/// Destination stays simulation-sized; only the source rect is scaled for sharper sampling.
/// </summary>
public static class WorldSpriteMetrics
{
    public static float Scale => DisplaySettings.WorldSpriteScale;

    public static int Scaled(int legacyPixels) =>
        Math.Max(1, (int)MathF.Round(legacyPixels * Scale));

    public static Rectangle ScaleSource(in Rectangle legacy) =>
        new(
            Scaled(legacy.X),
            Scaled(legacy.Y),
            Scaled(legacy.Width),
            Scaled(legacy.Height));

    /// <summary>World-space draw rect — always legacy size so zoom/framing stay stable.</summary>
    public static Rectangle LegacyWorldDestination(
        float worldX,
        float worldY,
        int legacyWidth,
        int legacyHeight) =>
        new((int)worldX, (int)worldY, legacyWidth, legacyHeight);

    public static void DrawLegacySprite(
        SpriteBatch spriteBatch,
        Texture2D texture,
        float worldX,
        float worldY,
        Rectangle legacySource,
        Color color) =>
        DrawLegacySprite(
            spriteBatch,
            texture,
            worldX,
            worldY,
            legacySource,
            legacySource.Width,
            legacySource.Height,
            color);

    /// <summary>Sample a legacy UV rect but draw at an explicit world size (building overlay icons).</summary>
    public static void DrawLegacySprite(
        SpriteBatch spriteBatch,
        Texture2D texture,
        float worldX,
        float worldY,
        Rectangle legacySource,
        int destWidth,
        int destHeight,
        Color color)
    {
        spriteBatch.Draw(
            texture,
            LegacyWorldDestination(worldX, worldY, destWidth, destHeight),
            ScaleSource(legacySource),
            color);
    }
}
