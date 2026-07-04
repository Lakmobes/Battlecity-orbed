namespace BattleCity.Client.Rendering;

/// <summary>
/// Client display configuration. Simulation stays at legacy 48 px tiles; only rendering scales.
/// </summary>
public static class DisplaySettings
{
    /// <summary>Legacy design resolution (800×600 including the 200 px interface rail).</summary>
    public const int LogicalWidth = 800;

    public const int LogicalHeight = 600;

    /// <summary>When true, keep drawing the legacy BMP interface rail until a modern HUD replaces it.</summary>
    public const bool UseLegacyUi = true;

    /// <summary>Scale logical frame to back buffer with whole-number steps (crisp pixel art).</summary>
    public static bool UseIntegerScaling { get; set; } = true;

    /// <summary>Initial window size multiplier when not fullscreen (logical × scale).</summary>
    public static int TargetIntegerScale { get; set; } = 1;

    static DisplaySettings()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("BATTLECITY_SCALE"), out var scale))
        {
            TargetIntegerScale = Math.Clamp(scale, 1, 4);
        }
    }

    public static int PreferredWindowWidth => LogicalWidth * TargetIntegerScale;

    public static int PreferredWindowHeight => LogicalHeight * TargetIntegerScale;
}
