using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>
/// Maps legacy 48px sprite layout to higher-resolution PNG sheets (client render only).
/// Simulation and collision stay at <see cref="BattleCity.Shared.Constants.GameConstants.TileSize"/>.
/// </summary>
public static class WorldSpriteMetrics
{
    public static float Scale => DisplaySettings.WorldSpriteScale;

    public static int Scaled(int legacyPixels) =>
        Math.Max(1, (int)MathF.Round(legacyPixels * Scale));

    public static int AnchorOffset(int legacyPixels) =>
        (int)MathF.Floor((Scale - 1f) * legacyPixels / 2f);

    public static Rectangle ScaleSource(in Rectangle legacy) =>
        new(
            Scaled(legacy.X),
            Scaled(legacy.Y),
            Scaled(legacy.Width),
            Scaled(legacy.Height));

    public static Rectangle LegacyWorldDestination(
        float worldX,
        float worldY,
        int legacyWidth,
        int legacyHeight) =>
        new(
            (int)worldX - AnchorOffset(legacyWidth),
            (int)worldY - AnchorOffset(legacyHeight),
            Scaled(legacyWidth),
            Scaled(legacyHeight));

    public static void DrawLegacySprite(
        SpriteBatch spriteBatch,
        Texture2D texture,
        float worldX,
        float worldY,
        Rectangle legacySource,
        Color color)
    {
        spriteBatch.Draw(
            texture,
            LegacyWorldDestination(worldX, worldY, legacySource.Width, legacySource.Height),
            ScaleSource(legacySource),
            color);
    }
}
