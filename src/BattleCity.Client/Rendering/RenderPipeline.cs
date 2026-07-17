using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using BattleCity.Client.Chat;

namespace BattleCity.Client.Rendering;

public sealed class RenderPipeline
{
    private readonly TerrainRenderer _terrain;
    private readonly EntityRenderer _entities;
    private readonly BuildingOverlayRenderer _buildingOverlays;
    private readonly BuildPreviewRenderer _buildPreview;
    private readonly MiniMapRenderer _miniMap;
    private readonly UiRenderer _ui;
    private readonly DeathOverlayRenderer _deathOverlay;
    private readonly OrbedOverlayRenderer _orbedOverlay;
    private readonly ResearchCompleteOverlayRenderer _researchCompleteOverlay;
    private readonly ChatOverlayRenderer _chatOverlay;

    public RenderPipeline(
        TerrainRenderer terrain,
        EntityRenderer entities,
        BuildingOverlayRenderer buildingOverlays,
        BuildPreviewRenderer buildPreview,
        MiniMapRenderer miniMap,
        UiRenderer ui,
        DeathOverlayRenderer deathOverlay,
        OrbedOverlayRenderer orbedOverlay,
        ResearchCompleteOverlayRenderer researchCompleteOverlay,
        ChatOverlayRenderer chatOverlay)
    {
        _terrain = terrain;
        _entities = entities;
        _buildingOverlays = buildingOverlays;
        _buildPreview = buildPreview;
        _miniMap = miniMap;
        _ui = ui;
        _deathOverlay = deathOverlay;
        _orbedOverlay = orbedOverlay;
        _researchCompleteOverlay = researchCompleteOverlay;
        _chatOverlay = chatOverlay;
    }

    public void DrawWorld(SpriteBatch spriteBatch, in RenderContext context)
    {
        var visible = context.Camera.VisibleWorldRect;
        _terrain.Draw(spriteBatch, context.TileMap, visible);
        _entities.CollectDrawables(context.World, context.CityBuild, context.AnimationTime, context.ObserverCityId);
        _entities.DrawBuildings(spriteBatch);
        _buildingOverlays.Draw(spriteBatch, context.World, context.CityBuild);
        _buildPreview.Draw(spriteBatch, in context);
        _entities.DrawActors(spriteBatch);
    }

    public void DrawScreen(SpriteBatch spriteBatch, in RenderContext context)
    {
        if (context.ShowMiniMap)
        {
            _miniMap.Draw(
                spriteBatch,
                context.TileMap,
                context.World,
                context.FocusWorldPosition,
                context.CityCenterWorldPosition);
        }

        if (context.PlayerRespawnSeconds.HasValue)
        {
            _deathOverlay.Draw(spriteBatch, context.PlayerRespawnSeconds.Value);
        }

        if (context.ShowResearchCompleteOverlay && !string.IsNullOrWhiteSpace(context.ResearchCompleteOverlayMessage))
        {
            _researchCompleteOverlay.Draw(spriteBatch, context.ResearchCompleteOverlayMessage);
        }

        if (context.ShowOrbedOverlay && !string.IsNullOrWhiteSpace(context.OrbedOverlayMessage))
        {
            _orbedOverlay.Draw(spriteBatch, context.OrbedOverlayMessage, context.OrbedOverlayIsVictim);
        }

        if (context.ChatLines is { Count: > 0 } || context.IsChatting)
        {
            _chatOverlay.Draw(
                spriteBatch,
                UiLayout.WorldViewportWidth,
                UiLayout.WorldViewportHeight,
                context.ChatLines ?? Array.Empty<ChatLine>(),
                context.IsChatting,
                context.ChatDraft);
        }

        _ui.Draw(spriteBatch, in context);
    }
}
