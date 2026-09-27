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
        new QueryDescription().WithAll<Transform2D, NetworkIdentity, CityAffiliation, TankLifeState>();

    private static readonly Color AllyColor = new(80, 220, 255);
    private static readonly Color EnemyColor = new(255, 90, 70);
    private static readonly Color AdminColor = new(255, 220, 80);
    private static readonly Color DeadColor = new(120, 120, 120);
    private static readonly Color PanelFill = new(8, 10, 18, 200);
    private static readonly Color CrosshairColor = new(255, 255, 255, 40);

    private readonly AssetService _assets;

    public RadarRenderer(AssetService assets)
    {
        _assets = assets;
    }

    public void Draw(SpriteBatch spriteBatch, in RenderContext context)
    {
        var bounds = ModernHudLayout.RadarBounds;
        HudOverlayHelper.DrawPanel(spriteBatch, _assets, bounds, PanelFill);

        var center = new Vector2(bounds.Center.X, bounds.Center.Y);
        spriteBatch.Draw(_assets.Pixel, new Rectangle(bounds.Center.X, bounds.Y + 4, 1, bounds.Height - 8), CrosshairColor);
        spriteBatch.Draw(_assets.Pixel, new Rectangle(bounds.X + 4, bounds.Center.Y, bounds.Width - 8, 1), CrosshairColor);
        spriteBatch.Draw(_assets.Pixel, new Rectangle(bounds.Center.X - 2, bounds.Center.Y - 2, 4, 4), AllyColor);

        var world = context.World;
        var focus = context.FocusWorldPosition;
        var localPlayerId = context.LocalPlayerId;
        var observerCityId = context.ObserverCityId;
        var resolveName = context.ResolvePlayerDisplayName;
        var range = ClientConstants.RadarSize;
        var scale = (bounds.Width / 2f - 6f) / range;

        world.Query(
            in TankQuery,
            (Entity entity, ref Transform2D transform, ref NetworkIdentity identity, ref CityAffiliation city, ref TankLifeState life) =>
            {
                if (identity.PlayerId == localPlayerId)
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
                if (screenX < bounds.Left + 2 || screenX > bounds.Right - 4
                    || screenY < bounds.Top + 2 || screenY > bounds.Bottom - 4)
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
                    var name = resolveName?.Invoke(identity.PlayerId);
                    color = TankSpriteSelector.IsAdminAccount(name) ? AdminColor : EnemyColor;
                }

                spriteBatch.Draw(_assets.Pixel, new Rectangle(screenX, screenY, 3, 3), color);
            });
    }
}
