using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

public static class NineSliceRenderer
{
    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D texture,
        Rectangle destination,
        int border,
        Color color)
    {
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        var sourceWidth = texture.Width;
        var sourceHeight = texture.Height;
        var centerSrcWidth = Math.Max(1, sourceWidth - border * 2);
        var centerSrcHeight = Math.Max(1, sourceHeight - border * 2);
        var centerDstWidth = Math.Max(1, destination.Width - border * 2);
        var centerDstHeight = Math.Max(1, destination.Height - border * 2);

        var source = new Rectangle[9];
        var dest = new Rectangle[9];

        source[0] = new Rectangle(0, 0, border, border);
        source[1] = new Rectangle(border, 0, centerSrcWidth, border);
        source[2] = new Rectangle(sourceWidth - border, 0, border, border);
        source[3] = new Rectangle(0, border, border, centerSrcHeight);
        source[4] = new Rectangle(border, border, centerSrcWidth, centerSrcHeight);
        source[5] = new Rectangle(sourceWidth - border, border, border, centerSrcHeight);
        source[6] = new Rectangle(0, sourceHeight - border, border, border);
        source[7] = new Rectangle(border, sourceHeight - border, centerSrcWidth, border);
        source[8] = new Rectangle(sourceWidth - border, sourceHeight - border, border, border);

        dest[0] = new Rectangle(destination.X, destination.Y, border, border);
        dest[1] = new Rectangle(destination.X + border, destination.Y, centerDstWidth, border);
        dest[2] = new Rectangle(destination.Right - border, destination.Y, border, border);
        dest[3] = new Rectangle(destination.X, destination.Y + border, border, centerDstHeight);
        dest[4] = new Rectangle(destination.X + border, destination.Y + border, centerDstWidth, centerDstHeight);
        dest[5] = new Rectangle(destination.Right - border, destination.Y + border, border, centerDstHeight);
        dest[6] = new Rectangle(destination.X, destination.Bottom - border, border, border);
        dest[7] = new Rectangle(destination.X + border, destination.Bottom - border, centerDstWidth, border);
        dest[8] = new Rectangle(destination.Right - border, destination.Bottom - border, border, border);

        for (var i = 0; i < 9; i++)
        {
            spriteBatch.Draw(texture, dest[i], source[i], color);
        }
    }
}
