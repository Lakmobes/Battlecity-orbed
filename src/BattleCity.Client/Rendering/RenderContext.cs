using Arch.Core;

using BattleCity.Client.Chat;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Maps;

using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

public sealed class RenderContext
{
    public required Camera2D Camera { get; init; }
    public required TileMap TileMap { get; init; }
    public required World World { get; init; }
    public required Vector2 FocusWorldPosition { get; init; }
    public required int ScreenWidth { get; init; }
    public required int ScreenHeight { get; init; }
    public bool ShowMiniMap { get; init; }
    public bool ShowStatusPanel { get; init; } = true;
    public bool ShowSettingsMenu { get; init; }
    public int SettingsSelectedIndex { get; init; }
    public string? LoadedCityName { get; init; }
    public int BuildingCount { get; init; }
    public int? PlayerHealth { get; init; }
    public int? PlayerMaxHealth { get; init; }
    public string? PlayerDisplayName { get; init; }
    public PlayerInventory? PlayerInventory { get; init; }
    public float CloakRechargeSeconds { get; init; }
    public float FlareRechargeSeconds { get; init; }
    public bool CloakRechargeUnlocked { get; init; }
    public bool FlareRechargeUnlocked { get; init; }
    public float? PlayerRespawnSeconds { get; init; }
    public Vector2 CityCenterWorldPosition { get; init; }
    public Vector2? NearestOrbableCityWorldPosition { get; init; }
    public int HomeCommandCenterGridX { get; init; }
    public int HomeCommandCenterGridY { get; init; }
    public bool IsUnderAttack { get; init; }
    public bool UnderAttackFlashVisible { get; init; }
    public bool ShowBuildMenu { get; init; }
    public Vector2 BuildMenuAnchor { get; init; }
    public CityBuildState? CityBuild { get; init; }
    public int BuildModeSlot { get; init; }
    public float AnimationTime { get; init; }
    public bool ShowOrbedOverlay { get; init; }
    public bool OrbedOverlayIsVictim { get; init; }
    public string? OrbedOverlayMessage { get; init; }
    public bool ShowBuildPreview { get; init; }
    public int BuildPreviewGridX { get; init; }
    public int BuildPreviewGridY { get; init; }
    public int BuildPreviewTypeCode { get; init; }
    public bool BuildPreviewIsValid { get; init; }
    public bool BuildPreviewIsDemolish { get; init; }
    public bool ShowResearchCompleteOverlay { get; init; }
    public string? ResearchCompleteOverlayMessage { get; init; }
    public IReadOnlyCollection<ChatLine>? ChatLines { get; init; }
    public bool IsChatting { get; init; }
    public string? ChatDraft { get; init; }

    /// <summary>Local observer city for fog-of-war style item visibility (sleepers).</summary>
    public int ObserverCityId { get; init; }
}
