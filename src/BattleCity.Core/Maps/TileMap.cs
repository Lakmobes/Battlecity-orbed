using System.Text.Json;
using System.Text.Json.Serialization;

using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;

namespace BattleCity.Core.Maps;

public sealed class TileMap
{
    public const int Size = GameConstants.MapSize;

    public TerrainTileType[,] Terrain { get; private set; } = CreateEmptyTerrain();
    public short[,] AutotileIndices { get; private set; } = CreateEmptyAutotiles();

    public static TileMap CreateEmpty() => new();

    public static TileMap LoadFromLegacyMapDat(string path)
    {
        using var stream = File.OpenRead(path);
        return LoadFromLegacyMapDat(stream);
    }

    public static TileMap LoadFromLegacyMapDat(Stream stream)
    {
        var expectedBytes = Size * Size;
        var buffer = new byte[expectedBytes];
        var read = stream.Read(buffer, 0, expectedBytes);

        if (read != expectedBytes)
        {
            throw new InvalidDataException(
                $"Legacy map.dat must be {expectedBytes} bytes; found {read}.");
        }

        var map = new TileMap();
        var index = 0;

        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                map.Terrain[x, y] = (TerrainTileType)buffer[index++];
            }
        }

        map.RecalculateAutotiles();
        return map;
    }

    public static TileMap LoadFromJson(string terrainJsonPath, string? autotileJsonPath = null)
    {
        var terrainDto = JsonSerializer.Deserialize(
            File.ReadAllText(terrainJsonPath),
            TileMapJsonContext.Default.TerrainDocument)!;

        if (terrainDto.Size != Size)
        {
            throw new InvalidDataException($"Terrain size {terrainDto.Size} != {Size}.");
        }

        var map = new TileMap();
        var cells = Convert.FromBase64String(terrainDto.Cells);

        if (cells.Length != Size * Size)
        {
            throw new InvalidDataException($"Terrain cell count {cells.Length} != {Size * Size}.");
        }

        var index = 0;
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                map.Terrain[x, y] = (TerrainTileType)cells[index++];
            }
        }

        if (autotileJsonPath is not null && File.Exists(autotileJsonPath))
        {
            var autotileDto = JsonSerializer.Deserialize(
                File.ReadAllText(autotileJsonPath),
                TileMapJsonContext.Default.AutotileDocument)!;

            if (autotileDto.Size != Size)
            {
                throw new InvalidDataException($"Autotile size {autotileDto.Size} != {Size}.");
            }
        }

        map.RecalculateAutotiles();
        return map;
    }

    public void RecalculateAutotiles()
    {
        AutotileIndices = AutotileCalculator.Calculate(Terrain);
    }

    public TerrainDocument ToTerrainDocument()
    {
        var cells = new byte[Size * Size];
        var index = 0;

        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                cells[index++] = (byte)Terrain[x, y];
            }
        }

        return new TerrainDocument
        {
            Size = Size,
            Cells = Convert.ToBase64String(cells),
        };
    }

    public AutotileDocument ToAutotileDocument()
    {
        var entries = new List<AutotileEntry>();

        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                var autotile = AutotileIndices[x, y];
                if (autotile != 0)
                {
                    entries.Add(new AutotileEntry(x, y, autotile));
                }
            }
        }

        return new AutotileDocument
        {
            Size = Size,
            Entries = entries,
        };
    }

    public void WriteJson(string terrainJsonPath, string autotileJsonPath)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };

        File.WriteAllText(
            terrainJsonPath,
            JsonSerializer.Serialize(ToTerrainDocument(), options));

        File.WriteAllText(
            autotileJsonPath,
            JsonSerializer.Serialize(ToAutotileDocument(), options));
    }

    private static TerrainTileType[,] CreateEmptyTerrain()
    {
        return new TerrainTileType[Size, Size];
    }

    private static short[,] CreateEmptyAutotiles()
    {
        return new short[Size, Size];
    }
}

public sealed class TerrainDocument
{
    public int Size { get; set; }
    public string Cells { get; set; } = string.Empty;
}

public sealed class AutotileDocument
{
    public int Size { get; set; }
    public List<AutotileEntry> Entries { get; set; } = [];
}

public readonly record struct AutotileEntry(int X, int Y, short Index);

[JsonSerializable(typeof(TerrainDocument))]
[JsonSerializable(typeof(AutotileDocument))]
internal partial class TileMapJsonContext : JsonSerializerContext;
