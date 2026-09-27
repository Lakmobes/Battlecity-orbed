using Arch.Core;

using BattleCity.Client.Assets;
using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Name + (City) under tanks (legacy drawPlayerName style). Drawn in world space.</summary>
public sealed class TankNameplateRenderer
{
    private static readonly QueryDescription TankQuery =
        new QueryDescription().WithAll<Transform2D, NetworkIdentity, CityAffiliation, TankLifeState>();

    private readonly AssetService _assets;
    private SpriteFont? _font;

    public TankNameplateRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void LoadContent() => _font = _assets.LoadFont(LegacySpriteNames.UiFont);

    public void Draw(SpriteBatch spriteBatch, in RenderContext context)
    {
        if (_font is null)
        {
            return;
        }

        var world = context.World;
        var localPlayerId = context.LocalPlayerId;
        var observerCityId = context.ObserverCityId;
        var localName = context.PlayerDisplayName;
        var resolveName = context.ResolvePlayerDisplayName;

        world.Query(
            in TankQuery,
            (Entity entity, ref Transform2D transform, ref NetworkIdentity identity, ref CityAffiliation city, ref TankLifeState life) =>
            {
                if (life.IsDead)
                {
                    return;
                }

                if (world.Has<Health>(entity) && world.Get<Health>(entity).Current <= 0)
                {
                    return;
                }

                if (world.Has<TankStatus>(entity)
                    && world.Get<TankStatus>(entity).IsCloaked
                    && identity.PlayerId != localPlayerId
                    && city.CityId != observerCityId)
                {
                    return;
                }

                var name = identity.PlayerId == localPlayerId
                    ? localName
                    : resolveName?.Invoke(identity.PlayerId);
                name ??= $"Player{identity.PlayerId}";

                var cityName = CityCatalog.IsValidCityId(city.CityId)
                    ? CityCatalog.GetName(city.CityId)
                    : "Unknown";
                var cityLine = world.Has<MayorStatus>(entity)
                    && world.Get<MayorStatus>(entity).IsMayor
                    ? $"(Mayor of {cityName})"
                    : $"({cityName})";

                var centerX = transform.Position.X + GameConstants.TileSize / 2f;
                var y = transform.Position.Y + GameConstants.TileSize + 2f;

                DrawCentered(spriteBatch, name, centerX, y, MenuTheme.TextPrimary, 0.55f);
                var lineHeight = _font.MeasureString(cityLine).Y * 0.45f;
                DrawCentered(spriteBatch, cityLine, centerX, y + lineHeight, MenuTheme.TextMuted, 0.45f);
            });
    }

    private void DrawCentered(
        SpriteBatch spriteBatch,
        string text,
        float centerX,
        float y,
        Color color,
        float scale)
    {
        var size = _font!.MeasureString(text) * scale;
        var position = new Vector2(centerX - size.X / 2f, y);
        spriteBatch.DrawString(
            _font,
            text,
            position + new Vector2(1f, 1f),
            new Color(0, 0, 0, 200),
            0f,
            Vector2.Zero,
            new Vector2(scale, scale),
            SpriteEffects.None,
            0f);
        spriteBatch.DrawString(
            _font,
            text,
            position,
            color,
            0f,
            Vector2.Zero,
            new Vector2(scale, scale),
            SpriteEffects.None,
            0f);
    }
}
