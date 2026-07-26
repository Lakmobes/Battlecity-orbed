namespace BattleCity.Client.Rendering;

/// <summary>
/// Client display configuration. Simulation stays at legacy 48 px tiles; only rendering scales.
/// </summary>
public static class DisplaySettings
{
    /// <summary>Logical design resolution (full-screen world + overlay HUD).</summary>
    public const int LogicalWidth = 1920;

    public const int LogicalHeight = 1080;

    /// <summary>When false, the modern transparent overlay HUD is used instead of the legacy right rail.</summary>
    public const bool UseLegacyUi = false;

    /// <summary>
    /// Texture atlas scale vs legacy 48px cell layout.
    /// To sharpen world art without zooming the camera:
    /// 1) Nearest-neighbor (or redraw) every world sheet at 2× (Tanks, Buildings, Ground, Lava,
    ///    Rocks, Bullets, Items, Turrets, explosions, Population, Health, BlackNumbers, etc.).
    /// 2) Set this to 2. <see cref="WorldSpriteMetrics"/> samples the HD source into the same
    ///    48px world footprint, so framing stays the same and pixels look sharper on 1080p.
    /// </summary>
    public const float WorldSpriteScale = 2f;

    /// <summary>Launch in borderless fullscreen at logical resolution (1920×1080).</summary>
    public static bool LaunchBorderlessFullscreen { get; set; } = true;

    /// <summary>Scale logical frame to back buffer with whole-number steps (crisp pixel art).</summary>
    public static bool UseIntegerScaling { get; set; } = false;

    /// <summary>Default in-game camera zoom at 1080p (how much world fits on screen).</summary>
    public const float DefaultGameplayZoom = 2f;

    /// <summary>Initial window size multiplier when not fullscreen (logical × scale).</summary>
    public static int TargetIntegerScale { get; set; } = 1;

    static DisplaySettings()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("BATTLECITY_SCALE"), out var scale))
        {
            TargetIntegerScale = Math.Clamp(scale, 1, 4);
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("BATTLECITY_WINDOWED"), out var windowed)
            && windowed != 0)
        {
            LaunchBorderlessFullscreen = false;
        }
    }

    public static int PreferredWindowWidth => LogicalWidth * TargetIntegerScale;

    public static int PreferredWindowHeight => LogicalHeight * TargetIntegerScale;
}
