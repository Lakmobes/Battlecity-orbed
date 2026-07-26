using BattleCity.Client.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Scenes;

public sealed class BootScene : IScene
{
    private readonly TitleScreenRenderer _title;
    private float _elapsedSeconds;

    public BootScene(SceneContext context)
    {
        _title = new TitleScreenRenderer(context.Assets);
    }

    public bool DrawsWorld => false;

    public Matrix WorldViewMatrix => Matrix.Identity;

    public void LoadContent() => _title.LoadContent();

    public SceneTransition Update(GameTime gameTime, int screenWidth, int screenHeight)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _elapsedSeconds += dt;
        _title.Update(dt);
        return _elapsedSeconds >= 1.4f ? SceneTransition.BootComplete : SceneTransition.None;
    }

    public void DrawWorld(SpriteBatch spriteBatch)
    {
    }

    public void DrawScreen(SpriteBatch spriteBatch)
    {
        _title.DrawBoot(spriteBatch, UiLayout.LogicalWidth, UiLayout.LogicalHeight);
    }

    public void Dispose()
    {
    }
}
