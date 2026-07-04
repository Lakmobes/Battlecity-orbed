using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Scenes;

public interface IScene : IDisposable
{
    bool DrawsWorld { get; }

    Matrix WorldViewMatrix { get; }

    void LoadContent();

    SceneTransition Update(GameTime gameTime, int screenWidth, int screenHeight);

    void DrawWorld(SpriteBatch spriteBatch);

    void DrawScreen(SpriteBatch spriteBatch);
}
