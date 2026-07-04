using BattleCity.Shared.Constants;

using Microsoft.Xna.Framework;

namespace BattleCity.Client.Rendering;

public sealed class Camera2D
{
    private float _zoom = 1f;

    public Vector2 Position { get; set; }
    public int ViewportWidth { get; private set; }
    public int ViewportHeight { get; private set; }
    public int WorldViewportWidth { get; set; } = UiLayout.WorldViewportWidth;
    public int WorldViewportHeight { get; set; } = UiLayout.WorldViewportHeight;

    public float Zoom
    {
        get => _zoom;
        set => _zoom = Math.Clamp(value, RenderConstants.MinZoom, RenderConstants.MaxZoom);
    }

    public void SetViewport(int width, int height)
    {
        ViewportWidth = width;
        ViewportHeight = height;
    }

    public Matrix ViewMatrix
    {
        get
        {
            return Matrix.CreateScale(Zoom, Zoom, 1f)
                * Matrix.CreateTranslation(-Position.X, -Position.Y, 0f);
        }
    }

    public void CenterOn(Vector2 worldPosition)
    {
        var visibleWidth = WorldViewportWidth / Zoom;
        var visibleHeight = WorldViewportHeight / Zoom;

        Position = new Vector2(
            worldPosition.X - visibleWidth / 2f,
            worldPosition.Y - visibleHeight / 2f);

        ClampToWorld(visibleWidth, visibleHeight);
    }

    public void Pan(Vector2 delta)
    {
        Position += delta / Zoom;
        ClampToWorld(WorldViewportWidth / Zoom, WorldViewportHeight / Zoom);
    }

    public void AdjustZoom(float delta)
    {
        var focus = Position + new Vector2(
            WorldViewportWidth / Zoom / 2f,
            WorldViewportHeight / Zoom / 2f);

        Zoom += delta;
        CenterOn(focus);
    }

    public Rectangle VisibleWorldRect
    {
        get
        {
            var visibleWidth = (int)MathF.Ceiling(WorldViewportWidth / Zoom);
            var visibleHeight = (int)MathF.Ceiling(WorldViewportHeight / Zoom);

            return new Rectangle(
                (int)MathF.Floor(Position.X),
                (int)MathF.Floor(Position.Y),
                visibleWidth,
                visibleHeight);
        }
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition) =>
        screenPosition / Zoom + Position;

    public Vector2 WorldToScreen(Vector2 worldPosition) =>
        (worldPosition - Position) * Zoom;

    private void ClampToWorld(float visibleWidth, float visibleHeight)
    {
        var maxX = Math.Max(0, GameConstants.WorldSizePixels - visibleWidth);
        var maxY = Math.Max(0, GameConstants.WorldSizePixels - visibleHeight);
        Position = new Vector2(
            Math.Clamp(Position.X, 0, maxX),
            Math.Clamp(Position.Y, 0, maxY));
    }
}
