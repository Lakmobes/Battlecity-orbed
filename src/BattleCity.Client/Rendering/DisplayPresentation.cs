using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

/// <summary>Maps the fixed logical frame to the current back buffer (integer scale + letterboxing).</summary>
public sealed class DisplayPresentation
{
    public int BackBufferWidth { get; init; }

    public int BackBufferHeight { get; init; }

    public int IntegerScale { get; init; } = 1;

    public int OffsetX { get; init; }

    public int OffsetY { get; init; }

    public int ScaledWidth => DisplaySettings.LogicalWidth * IntegerScale;

    public int ScaledHeight => DisplaySettings.LogicalHeight * IntegerScale;

    public Matrix TransformMatrix { get; init; } = Matrix.Identity;

    public static DisplayPresentation Create(int backBufferWidth, int backBufferHeight)
    {
        if (backBufferWidth <= 0 || backBufferHeight <= 0)
        {
            return new DisplayPresentation
            {
                BackBufferWidth = backBufferWidth,
                BackBufferHeight = backBufferHeight,
            };
        }

        if (!DisplaySettings.UseIntegerScaling)
        {
            var scaleX = backBufferWidth / (float)DisplaySettings.LogicalWidth;
            var scaleY = backBufferHeight / (float)DisplaySettings.LogicalHeight;
            return new DisplayPresentation
            {
                BackBufferWidth = backBufferWidth,
                BackBufferHeight = backBufferHeight,
                IntegerScale = 1,
                TransformMatrix = Matrix.CreateScale(scaleX, scaleY, 1f),
            };
        }

        var scale = Math.Max(
            1,
            Math.Min(
                backBufferWidth / DisplaySettings.LogicalWidth,
                backBufferHeight / DisplaySettings.LogicalHeight));

        var scaledWidth = DisplaySettings.LogicalWidth * scale;
        var scaledHeight = DisplaySettings.LogicalHeight * scale;
        var offsetX = (backBufferWidth - scaledWidth) / 2;
        var offsetY = (backBufferHeight - scaledHeight) / 2;

        var transform = Matrix.CreateScale(scale, scale, 1f)
            * Matrix.CreateTranslation(offsetX, offsetY, 0f);

        return new DisplayPresentation
        {
            BackBufferWidth = backBufferWidth,
            BackBufferHeight = backBufferHeight,
            IntegerScale = scale,
            OffsetX = offsetX,
            OffsetY = offsetY,
            TransformMatrix = transform,
        };
    }

    public Vector2 ScreenToLogical(Vector2 screenPoint)
    {
        if (TransformMatrix == Matrix.Identity)
        {
            return screenPoint;
        }

        return Vector2.Transform(screenPoint, Matrix.Invert(TransformMatrix));
    }

    public int LogicalWidth => DisplaySettings.LogicalWidth;

    public int LogicalHeight => DisplaySettings.LogicalHeight;
}
