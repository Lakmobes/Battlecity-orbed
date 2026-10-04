using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>
/// Builds a lava sheet where solid magenta is replaced by the current bubble frame,
/// and real transparency is left empty so the ground shows through.
/// </summary>
public sealed class LavaSheetComposer
{
    private Color[]? _mask;
    private Color[]? _fill;
    private Texture2D? _baked;
    private int _bakedFrame = -1;
    private int _width;
    private int _height;

    public Texture2D Compose(GraphicsDevice device, Texture2D lava, Texture2D fill, int frame, int cellSize)
    {
        EnsureMask(lava);
        EnsureFill(fill);
        if (_baked is null || _baked.IsDisposed || _baked.Width != _width || _baked.Height != _height)
        {
            _baked?.Dispose();
            _baked = new Texture2D(device, _width, _height);
            _bakedFrame = -1;
        }

        if (_bakedFrame == frame)
        {
            return _baked;
        }

        var frames = FrameCount(fill);
        var frameHeight = fill.Height / frames;
        var frameRow = Math.Clamp(frame, 0, frames - 1) * frameHeight;
        var cell = Math.Max(1, cellSize);
        var pixels = new Color[_mask!.Length];
        for (var i = 0; i < _mask.Length; i++)
        {
            var pixel = _mask[i];
            if (pixel.A < 20)
            {
                pixels[i] = Color.Transparent;
                continue;
            }

            if (pixel.R > 220 && pixel.G < 50 && pixel.B > 220)
            {
                var x = i % _width;
                var y = i / _width;
                var localX = x % cell;
                var localY = y % cell;
                var sampleX = Math.Clamp(localX * fill.Width / cell, 0, fill.Width - 1);
                var sampleY = Math.Clamp(localY * frameHeight / cell, 0, frameHeight - 1);
                pixels[i] = _fill![((frameRow + sampleY) * fill.Width) + sampleX];
                continue;
            }

            pixels[i] = pixel;
        }

        _baked.SetData(pixels);
        _bakedFrame = frame;
        return _baked;
    }

    public static int FrameCount(Texture2D fill)
    {
        if (fill.Width > 0 && fill.Height >= fill.Width && fill.Height % fill.Width == 0)
        {
            return fill.Height / fill.Width;
        }

        return 1;
    }

    private void EnsureMask(Texture2D lava)
    {
        if (_mask is not null && _width == lava.Width && _height == lava.Height)
        {
            return;
        }

        _width = lava.Width;
        _height = lava.Height;
        _mask = new Color[_width * _height];
        lava.GetData(_mask);
        _bakedFrame = -1;
    }

    private void EnsureFill(Texture2D fill)
    {
        if (_fill is not null && _fill.Length == fill.Width * fill.Height)
        {
            return;
        }

        _fill = new Color[fill.Width * fill.Height];
        fill.GetData(_fill);
        _bakedFrame = -1;
    }
}
