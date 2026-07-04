namespace BattleCity.Client.Assets;

/// <summary>
/// Maps legacy img*.bmp filenames to MonoGame content paths.
/// </summary>
public static class LegacySpriteNames
{
    public const string Ground = "Sprites/Ground";
    public const string Lava = "Sprites/Lava";
    public const string Rocks = "Sprites/Rocks";
    public const string Tanks = "Sprites/Tanks";
    public const string MiniMapColors = "Sprites/MiniMapColors";
    public const string Buildings = "Sprites/Buildings";
    public const string Bullets = "Sprites/Bullets";
    public const string Items = "Sprites/Items";
    public const string Interface = "Sprites/Interface";
    public const string InterfaceBottom = "Sprites/InterfaceBottom";
    public const string SExplosion = "Sprites/SExplosion";
    public const string LExplosion = "Sprites/LExplosion";
    public const string MuzzleFlash = "Sprites/MuzzleFlash";
    public const string Population = "Sprites/Population";
    public const string InventorySelection = "Sprites/InventorySelection";
    public const string Health = "Sprites/Health";
    public const string BlackNumbers = "Sprites/BlackNumbers";
    public const string MenuFont = "Fonts/MenuFont";

    public static string ToContentPath(string legacyBmpFileName)
    {
        var name = Path.GetFileNameWithoutExtension(legacyBmpFileName);
        if (name.StartsWith("img", StringComparison.OrdinalIgnoreCase))
        {
            name = name[3..];
        }

        return $"Sprites/{name}";
    }
}
