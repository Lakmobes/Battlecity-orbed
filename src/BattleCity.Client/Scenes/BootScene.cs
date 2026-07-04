using BattleCity.Client.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Scenes;

public sealed class BootScene : IScene
{
    private readonly SceneContext _context;
    private readonly ScreenUiRenderer _ui;
    private float _elapsedSeconds;

    public BootScene(SceneContext context)
    {
        _context = context;
        _ui = new ScreenUiRenderer(context.Assets);
    }

    public bool DrawsWorld => false;

    public Matrix WorldViewMatrix => Matrix.Identity;

    public void LoadContent() => _ui.LoadContent();

    public SceneTransition Update(GameTime gameTime, int screenWidth, int screenHeight)
    {
        _elapsedSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
        return _elapsedSeconds >= 0.75f ? SceneTransition.BootComplete : SceneTransition.None;
    }

    public void DrawWorld(SpriteBatch spriteBatch)
    {
    }

    public void DrawScreen(SpriteBatch spriteBatch)
    {
        _ui.DrawBackdrop(spriteBatch, RenderConstants.DefaultWindowWidth, RenderConstants.DefaultWindowHeight);
        _ui.DrawTitle(spriteBatch, RenderConstants.DefaultWindowWidth);
        _ui.DrawCenteredText(
            spriteBatch,
            "Loading...",
            RenderConstants.DefaultWindowWidth / 2,
            RenderConstants.DefaultWindowHeight / 2,
            Color.LightGray);
    }

    public void Dispose()
    {
    }
}
