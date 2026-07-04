using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace BattleCity.Tools.BmpToPng;

internal static class Program
{
    private static readonly PngEncoder PngEncoder = new()
    {
        ColorType = PngColorType.RgbWithAlpha,
    };

    public static int Main(string[] args)
    {
        var options = ParseArgs(args);
        if (options is null)
        {
            PrintUsage();
            return 1;
        }

        if (!Directory.Exists(options.InputDirectory))
        {
            Console.Error.WriteLine($"Input directory not found: {options.InputDirectory}");
            return 1;
        }

        Directory.CreateDirectory(options.OutputDirectory);

        var files = options.AllSprites
            ? Directory.GetFiles(options.InputDirectory, "img*.bmp", SearchOption.TopDirectoryOnly)
            : new[]
            {
                "imgGround.bmp",
                "imgLava.bmp",
                "imgRocks.bmp",
                "imgTanks.bmp",
                "imgMiniMapColors.bmp",
                "imgBuildings.bmp",
                "imgBullets.bmp",
                "imgItems.bmp",
                "imgTurretBase.bmp",
                "imgTurretHead.bmp",
                "imgInterface.bmp",
                "imgInterfaceBottom.bmp",
                "imgSExplosion.bmp",
                "imgLExplosion.bmp",
                "imgMuzzleFlash.bmp",
                "imgPopulation.bmp",
                "imgInventorySelection.bmp",
                "imgHealth.bmp",
                "imgBlackNumbers.bmp",
            }
                .Select(name => Path.Combine(options.InputDirectory, name))
                .Where(File.Exists)
                .ToArray();

        if (files.Length == 0)
        {
            Console.Error.WriteLine($"No BMP files found in {options.InputDirectory}");
            return 1;
        }

        foreach (var inputPath in files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var outputName = LegacySpritePaths.ToContentFileName(Path.GetFileName(inputPath));
            var outputPath = Path.Combine(options.OutputDirectory, outputName);

            using var image = Image.Load(inputPath);
            image.Save(outputPath, PngEncoder);
            Console.WriteLine($"{Path.GetFileName(inputPath)} -> {outputName}");
        }

        return 0;
    }

    private static Options? ParseArgs(string[] args)
    {
        var input = FindPath(args, "--input");
        var output = FindPath(args, "--output");
        var allSprites = args.Contains("--all", StringComparer.OrdinalIgnoreCase);

        if (input is null || output is null)
        {
            return null;
        }

        return new Options(input, output, allSprites);
    }

    private static string? FindPath(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: BmpToPng --input <bmp-dir> --output <png-dir> [--all]");
        Console.WriteLine("  Default converts imgGround/imgLava/imgRocks; --all converts every img*.bmp.");
    }

    private sealed record Options(string InputDirectory, string OutputDirectory, bool AllSprites);
}

internal static class LegacySpritePaths
{
    public static string ToContentFileName(string legacyBmpFileName)
    {
        var name = Path.GetFileNameWithoutExtension(legacyBmpFileName);
        if (name.StartsWith("img", StringComparison.OrdinalIgnoreCase))
        {
            name = name[3..];
        }

        return $"{name}.png";
    }
}
