using BattleCity.Client.Assets;
using BattleCity.Client.Audio;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Scenes;

public sealed class SceneManager : IDisposable
{
    private readonly AssetService _assets;
    private IScene? _current;

    public SceneManager(AssetService assets, AudioService audio)
    {
        _assets = assets;
        Context = new SceneContext(assets, audio);
    }

    public SceneContext Context { get; }

    public bool DrawsWorld => _current?.DrawsWorld ?? false;

    public Matrix WorldViewMatrix => _current?.WorldViewMatrix ?? Matrix.Identity;

    public bool QuitRequested { get; private set; }

    public void Start()
    {
        ChangeScene(new BootScene(Context));
    }

    public void Update(GameTime gameTime, int screenWidth, int screenHeight)
    {
        if (_current is null)
        {
            return;
        }

        var transition = _current.Update(gameTime, screenWidth, screenHeight);
        if (transition != SceneTransition.None)
        {
            ApplyTransition(transition);
        }
    }

    public void DrawWorld(SpriteBatch spriteBatch) => _current?.DrawWorld(spriteBatch);

    public void DrawScreen(SpriteBatch spriteBatch) => _current?.DrawScreen(spriteBatch);

    public void Dispose()
    {
        _current?.Dispose();
        _current = null;
    }

    private void ApplyTransition(SceneTransition transition)
    {
        if (transition == SceneTransition.Quit)
        {
            QuitRequested = true;
            return;
        }

        switch (transition)
        {
            case SceneTransition.BootComplete:
            case SceneTransition.MainMenu:
                ChangeScene(new MainMenuScene(Context));
                break;
            case SceneTransition.Login:
                ChangeScene(new LoginScene(Context));
                break;
            case SceneTransition.CreateAccount:
                ChangeScene(new CreateAccountScene(Context));
                break;
            case SceneTransition.InGameOffline:
                ChangeScene(new InGameScene(Context));
                break;
            case SceneTransition.InGameOnline:
                if (Context.NetworkClient is not null)
                {
                    ChangeScene(new InGameOnlineScene(Context, Context.NetworkClient));
                }

                break;
            case SceneTransition.Meeting:
                if (Context.NetworkClient is not null)
                {
                    ChangeScene(new MeetingScene(Context, Context.NetworkClient));
                }

                break;
            case SceneTransition.Interview:
                if (Context.NetworkClient is not null)
                {
                    ChangeScene(new InterviewScene(Context, Context.NetworkClient));
                }

                break;
            case SceneTransition.None:
                break;
        }
    }

    internal void ChangeScene(IScene scene)
    {
        _current?.Dispose();
        _current = scene;
        _current.LoadContent();
    }
}
