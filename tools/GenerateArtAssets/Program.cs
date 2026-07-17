using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace BattleCity.Tools.GenerateArtAssets;

internal static class Program
{
    private static readonly Rgba32 Magenta = new(255, 0, 255, 255);

    public static int Main(string[] args)
    {
        var repoRoot = ResolveRepoRoot(args);
        var generateGameSprites = args.Any(static arg => arg is "--game" or "-g");
        var spriteOutput = Path.Combine(repoRoot, "src", "BattleCity.Client", "Content", "Sprites");
        var hudOutput = Path.Combine(spriteOutput, "Hud");
        Directory.CreateDirectory(hudOutput);

        Console.WriteLine("==> Generating HUD art");
        GenerateHudPanel(Path.Combine(hudOutput, "Panel.png"));
        GenerateHudSlot(Path.Combine(hudOutput, "Slot.png"), selected: false);
        GenerateHudSlot(Path.Combine(hudOutput, "SlotSelected.png"), selected: true);
        GenerateCompassRing(Path.Combine(hudOutput, "CompassRing.png"));

        if (generateGameSprites)
        {
            Console.WriteLine("==> Generating game sprite reskins (48px grid)");
            GenerateGround(Path.Combine(spriteOutput, "Ground.png"));
            GenerateAutotileStrip(Path.Combine(spriteOutput, "Lava.png"), lava: true);
            GenerateAutotileStrip(Path.Combine(spriteOutput, "Rocks.png"), lava: false);
            GenerateBullets(Path.Combine(spriteOutput, "Bullets.png"));
            GenerateSmallExplosion(Path.Combine(spriteOutput, "SExplosion.png"));
            GenerateTanks(Path.Combine(spriteOutput, "Tanks.png"));
        }
        else
        {
            Console.WriteLine("Skipping game sprites (pass --game to regenerate placeholders).");
        }

        Console.WriteLine($"Art assets written under {spriteOutput}");
        return 0;
    }

    private static string ResolveRepoRoot(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--root" or "-r")
            {
                return Path.GetFullPath(args[i + 1]);
            }
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static void GenerateHudPanel(string path)
    {
        const int size = 64;
        const int border = 16;
        using var image = new Image<Rgba32>(size, size);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < size; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < size; x++)
                {
                    var edge = x < border || y < border || x >= size - border || y >= size - border;
                    var corner = (x < border || x >= size - border) && (y < border || y >= size - border);
                    row[x] = edge
                        ? corner
                            ? new Rgba32(40, 55, 90, 230)
                            : new Rgba32(24, 32, 52, 200)
                        : new Rgba32(12, 16, 28, 170);
                }
            }
        });

        HighlightBorder(image, border, new Rgba32(120, 170, 255, 120));
        image.SaveAsPng(path);
    }

    private static void GenerateHudSlot(string path, bool selected)
    {
        const int size = 48;
        using var image = new Image<Rgba32>(size, size);
        var fill = selected ? new Rgba32(50, 90, 140, 220) : new Rgba32(20, 28, 44, 190);
        var border = selected ? new Rgba32(140, 210, 255, 255) : new Rgba32(80, 110, 150, 200);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < size; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < size; x++)
                {
                    var onBorder = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                    row[x] = onBorder ? border : fill;
                }
            }
        });

        image.SaveAsPng(path);
    }

    private static void GenerateCompassRing(string path)
    {
        const int size = 96;
        var center = (size - 1) / 2f;
        using var image = new Image<Rgba32>(size, size);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < size; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > 44 || dist < 30)
                    {
                        row[x] = new Rgba32(0, 0, 0, 0);
                        continue;
                    }

                    var ring = dist > 40 || dist < 34;
                    row[x] = ring
                        ? new Rgba32(100, 160, 230, 220)
                        : new Rgba32(10, 14, 26, 160);
                }
            }
        });

        DrawCompassTicks(image, center);
        image.SaveAsPng(path);
    }

    private static void DrawCompassTicks(Image<Rgba32> image, float center)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (var i = 0; i < 8; i++)
            {
                var angle = i * MathF.PI / 4f;
                var outerX = (int)MathF.Round(center + MathF.Sin(angle) * 38f);
                var outerY = (int)MathF.Round(center - MathF.Cos(angle) * 38f);
                if (outerX >= 0 && outerX < image.Width && outerY >= 0 && outerY < image.Height)
                {
                    accessor.GetRowSpan(outerY)[outerX] = new Rgba32(200, 220, 255, 240);
                }
            }
        });
    }

    private static void GenerateGround(string path)
    {
        const int size = 128;
        using var image = new Image<Rgba32>(size, size);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < size; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < size; x++)
                {
                    var noise = ((x * 17 + y * 31) % 17) - 8;
                    var g = (byte)Math.Clamp(42 + noise, 28, 58);
                    row[x] = new Rgba32((byte)(g - 8), g, (byte)(g - 14), 255);
                }
            }
        });

        image.SaveAsPng(path);
    }

    private static void GenerateAutotileStrip(string path, bool lava)
    {
        const int tile = 48;
        const int tiles = 16;
        using var image = new Image<Rgba32>(tile * tiles, tile);
        for (var index = 0; index < tiles; index++)
        {
            DrawAutotile(image, index * tile, 0, tile, index, lava);
        }

        image.SaveAsPng(path);
    }

    private static void DrawAutotile(Image<Rgba32> image, int originX, int originY, int tile, int variant, bool lava)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < tile; y++)
            {
                var row = accessor.GetRowSpan(originY + y);
                for (var x = 0; x < tile; x++)
                {
                    var edge = x < 4 || y < 4 || x >= tile - 4 || y >= tile - 4;
                    var pulse = (variant + x + y) % 6;
                    if (lava)
                    {
                        row[originX + x] = edge
                            ? new Rgba32(120, 30, 0, 255)
                            : new Rgba32((byte)(200 + pulse * 5), (byte)(60 + pulse * 8), 10, 255);
                    }
                    else
                    {
                        var shade = (byte)(90 + pulse * 4);
                        row[originX + x] = edge
                            ? new Rgba32((byte)(shade - 20), (byte)(shade - 20), (byte)(shade - 10), 255)
                            : new Rgba32(shade, shade, (byte)(shade + 8), 255);
                    }
                }
            }
        });
    }

    private static void GenerateBullets(string path)
    {
        const int size = 32;
        using var image = new Image<Rgba32>(size, size);
        Fill(image, Magenta);
        var colors = new[]
        {
            new Rgba32(255, 240, 80, 255),
            new Rgba32(255, 120, 40, 255),
            new Rgba32(120, 220, 255, 255),
            new Rgba32(255, 80, 180, 255),
        };

        for (var kind = 0; kind < 4; kind++)
        {
            for (var frame = 0; frame < 4; frame++)
            {
                DrawBullet(image, frame * 8, kind * 8, colors[kind], frame);
            }
        }

        image.SaveAsPng(path);
    }

    private static void DrawBullet(Image<Rgba32> image, int ox, int oy, Rgba32 color, int frame)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < 8; y++)
            {
                var row = accessor.GetRowSpan(oy + y);
                for (var x = 0; x < 8; x++)
                {
                    var cx = 3.5f + frame * 0.2f;
                    var cy = 3.5f;
                    var dx = x - cx;
                    var dy = y - cy;
                    if (dx * dx + dy * dy <= 9)
                    {
                        row[ox + x] = color;
                    }
                }
            }
        });
    }

    private static void GenerateSmallExplosion(string path)
    {
        const int frameSize = 48;
        const int frames = 10;
        using var image = new Image<Rgba32>(frameSize * frames, frameSize);
        Fill(image, Magenta);
        for (var frame = 0; frame < frames; frame++)
        {
            DrawExplosionFrame(image, frame * frameSize, 0, frameSize, frame, frames);
        }

        image.SaveAsPng(path);
    }

    private static void DrawExplosionFrame(Image<Rgba32> image, int ox, int oy, int size, int frame, int frameCount)
    {
        var progress = frame / (float)(frameCount - 1);
        var radius = size * (0.2f + progress * 0.35f);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < size; y++)
            {
                var row = accessor.GetRowSpan(oy + y);
                for (var x = 0; x < size; x++)
                {
                    var dx = x - size / 2f;
                    var dy = y - size / 2f;
                    var dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > radius)
                    {
                        continue;
                    }

                    row[ox + x] = dist < radius * 0.45f
                        ? new Rgba32(255, 240, 120, 255)
                        : new Rgba32(255, 120, 30, 255);
                }
            }
        });
    }

    private static void GenerateTanks(string path)
    {
        const int cols = 16;
        const int rows = 50;
        const int tile = 48;
        using var image = new Image<Rgba32>(cols * tile, rows * tile);
        Fill(image, Magenta);

        var palettes = new[]
        {
            new Rgba32(70, 170, 90, 255),
            new Rgba32(220, 190, 60, 255),
            new Rgba32(200, 70, 70, 255),
            new Rgba32(170, 90, 200, 255),
        };

        for (var row = 0; row < rows; row++)
        {
            var type = row % 4;
            var shade = (byte)Math.Clamp(20 + (row / 4) * 3, 0, 60);
            var body = OffsetColor(palettes[type], shade);
            var track = OffsetColor(body, -35);
            for (var col = 0; col < cols; col++)
            {
                DrawTank(image, col * tile, row * tile, tile, col, body, track);
            }
        }

        image.SaveAsPng(path);
    }

    private static void DrawTank(Image<Rgba32> image, int ox, int oy, int tile, int direction, Rgba32 body, Rgba32 track)
    {
        var angle = direction * MathF.PI / 8f;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < tile; y++)
            {
                var row = accessor.GetRowSpan(oy + y);
                for (var x = 0; x < tile; x++)
                {
                    var lx = x - tile / 2f;
                    var ly = y - tile / 2f;
                    var rx = lx * MathF.Cos(angle) + ly * MathF.Sin(angle);
                    var ry = -lx * MathF.Sin(angle) + ly * MathF.Cos(angle);

                    if (MathF.Abs(rx) <= 14 && MathF.Abs(ry) <= 10)
                    {
                        row[ox + x] = body;
                    }
                    else if (MathF.Abs(rx) <= 16 && MathF.Abs(ry) <= 14)
                    {
                        row[ox + x] = track;
                    }
                    else if (rx > 8 && rx < 22 && MathF.Abs(ry) <= 3)
                    {
                        row[ox + x] = OffsetColor(body, 30);
                    }
                }
            }
        });
    }

    private static void HighlightBorder(Image<Rgba32> image, int border, Rgba32 color)
    {
        image.ProcessPixelRows(accessor =>
        {
            var width = image.Width;
            var height = image.Height;
            for (var x = border; x < width - border; x++)
            {
                accessor.GetRowSpan(border)[x] = color;
                accessor.GetRowSpan(height - border - 1)[x] = color;
            }

            for (var y = border; y < height - border; y++)
            {
                accessor.GetRowSpan(y)[border] = color;
                accessor.GetRowSpan(y)[width - border - 1] = color;
            }
        });
    }

    private static void Fill(Image<Rgba32> image, Rgba32 color)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < image.Height; y++)
            {
                accessor.GetRowSpan(y).Fill(color);
            }
        });
    }

    private static Rgba32 OffsetColor(Rgba32 color, int delta) =>
        new(
            (byte)Math.Clamp(color.R + delta, 0, 255),
            (byte)Math.Clamp(color.G + delta, 0, 255),
            (byte)Math.Clamp(color.B + delta, 0, 255),
            color.A);
}
