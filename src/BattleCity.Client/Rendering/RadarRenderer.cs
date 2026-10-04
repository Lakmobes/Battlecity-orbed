using Arch.Core;

using BattleCity.Client.Assets;
using BattleCity.Core.Ecs.Components;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Gameplay;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Rendering;

/// <summary>Legacy proximity radar — dots for nearby tanks (ally / enemy / admin).</summary>
public sealed class RadarRenderer
{
    private static readonly QueryDescription TankQuery =
        new QueryDescription().WithAll<Transform2D, CityAffiliation, TankLifeState, TankFacing>();

    private static readonly Color AllyColor = HudTheme.Ally;
    private static readonly Color EnemyColor = HudTheme.Enemy;
    private static readonly Color AdminColor = HudTheme.Objective;
    private static readonly Color DeadColor = new(120, 120, 120);

    private readonly AssetService _assets;

    public RadarRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void DrawBackdrop(SpriteBatch spriteBatch)
    {
        var bounds = ModernHudLayout.RadarBounds;
        var center = new Vector2(bounds.Center.X, bounds.Center.Y);
        var radius = bounds.Width / 2 - 2;
        var radarArt = _assets.LoadTexture(HudSpriteNames.Radar);
        if (radarArt != _assets.Pixel)
        {
            spriteBatch.Draw(radarArt, bounds, Color.White);
        }
        else
        {
            FillCircle(spriteBatch, center, radius, new Color(10, 18, 28, 210));
            DrawCircleOutline(spriteBatch, center, radius, HudTheme.Accent);
        }
    }

    public void Draw(SpriteBatch spriteBatch, in RenderContext context)
    {
        var bounds = ModernHudLayout.RadarBounds;
        var center = new Vector2(bounds.Center.X, bounds.Center.Y);
        var radius = bounds.Width / 2 - 2;
        spriteBatch.Draw(_assets.Pixel, new Rectangle(bounds.Center.X - 2, bounds.Center.Y - 2, 5, 5), AllyColor);

        var world = context.World;
        var focus = context.FocusWorldPosition;
        var localPlayerId = context.LocalPlayerId;
        var observerCityId = context.ObserverCityId;
        var resolveName = context.ResolvePlayerDisplayName;
        var range = ClientConstants.RadarSize;
        var scale = (bounds.Width / 2f - 6f) / range;

        world.Query(
            in TankQuery,
            (Entity entity, ref Transform2D transform, ref CityAffiliation city, ref TankLifeState life) =>
            {
                if (world.Has<InputControlled>(entity))
                {
                    return;
                }

                if (world.Has<NetworkIdentity>(entity)
                    && world.Get<NetworkIdentity>(entity).PlayerId == localPlayerId)
                {
                    return;
                }

                if (world.Has<TankStatus>(entity)
                    && world.Get<TankStatus>(entity).IsCloaked
                    && city.CityId != observerCityId)
                {
                    return;
                }

                var dx = transform.Position.X - focus.X;
                var dy = transform.Position.Y - focus.Y;
                if (Math.Abs(dx) > range || Math.Abs(dy) > range)
                {
                    return;
                }

                var screenX = (int)(center.X + dx * scale);
                var screenY = (int)(center.Y + dy * scale);
                var offsetX = screenX - center.X;
                var offsetY = screenY - center.Y;
                if ((offsetX * offsetX) + (offsetY * offsetY) > (radius - 8) * (radius - 8))
                {
                    return;
                }

                Color color;
                if (life.IsDead || (world.Has<Health>(entity) && world.Get<Health>(entity).Current <= 0))
                {
                    color = DeadColor;
                }
                else if (city.CityId == observerCityId)
                {
                    color = AllyColor;
                }
                else
                {
                    string? name = null;
                    if (world.Has<NetworkIdentity>(entity))
                    {
                        name = resolveName?.Invoke(world.Get<NetworkIdentity>(entity).PlayerId);
                    }

                    color = TankSpriteSelector.IsAdminAccount(name) ? AdminColor : EnemyColor;
                }

                const int dot = 4;
                spriteBatch.Draw(_assets.Pixel, new Rectangle(screenX, screenY, dot, dot), color);
            });

        DrawObjective(spriteBatch, center, radius, scale, context.FocusWorldPosition, context.CityCenterWorldPosition);
        if (context.NearestOtherCityWorldPosition is { } other)
        {
            DrawObjective(spriteBatch, center, radius, scale, context.FocusWorldPosition, other);
        }
    }

    private void DrawObjective(
        SpriteBatch spriteBatch,
        Vector2 center,
        int radius,
        float scale,
        Vector2 focus,
        Vector2 target)
    {
        var dx = target.X - focus.X;
        var dy = target.Y - focus.Y;
        var screenX = center.X + dx * scale;
        var screenY = center.Y + dy * scale;
        var ox = screenX - center.X;
        var oy = screenY - center.Y;
        if ((ox * ox) + (oy * oy) > (radius - 10) * (radius - 10))
        {
            return;
        }

        spriteBatch.Draw(_assets.Pixel, new Rectangle((int)screenX - 2, (int)screenY - 2, 5, 5), HudTheme.Objective);
    }

    private void FillCircle(SpriteBatch spriteBatch, Vector2 center, int radius, Color color)
    {
        var pixel = _assets.Pixel;
        var cx = (int)center.X;
        var cy = (int)center.Y;
        for (var y = -radius; y <= radius; y++)
        {
            var dx = (int)Math.Sqrt((radius * radius) - (y * y));
            spriteBatch.Draw(pixel, new Rectangle(cx - dx, cy + y, dx * 2, 1), color);
        }
    }

    private void DrawCircleOutline(SpriteBatch spriteBatch, Vector2 center, int radius, Color color)
    {
        var pixel = _assets.Pixel;
        var cx = (int)center.X;
        var cy = (int)center.Y;
        var segments = 48;
        for (var i = 0; i < segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var x = cx + (int)(MathF.Cos(angle) * radius);
            var y = cy + (int)(MathF.Sin(angle) * radius);
            spriteBatch.Draw(pixel, new Rectangle(x, y, 2, 2), color);
        }
    }
}
