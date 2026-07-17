using BattleCity.Client.Assets;
using BattleCity.Client.Audio;
using BattleCity.Client.Rendering;
using BattleCity.Client.Scenes;
using BattleCity.Shared;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client;

public sealed class BattleCityGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private AssetService _assets = null!;
    private AudioService _audio = null!;
    private SceneManager _scenes = null!;
    private DisplayPresentation _presentation = null!;
    private KeyboardState _previousKeyboard;

    public BattleCityGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        Window.Title = GameInfo.Title;
        ApplyDisplayMode(startup: true);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _assets = new AssetService(Content);
        _assets.Initialize(GraphicsDevice);
        _audio = new AudioService(Content);
        _audio.LoadContent();
        _scenes = new SceneManager(_assets, _audio);
        _scenes.Start();
        RefreshPresentation();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (WasPressed(keyboard, Keys.F11)
            || (keyboard.IsKeyDown(Keys.LeftAlt) && WasPressed(keyboard, Keys.Enter)))
        {
            ToggleFullScreen();
        }

        _previousKeyboard = keyboard;
        RefreshPresentation();

        _scenes.Context.Presentation = _presentation;
        _scenes.Update(
            gameTime,
            DisplaySettings.LogicalWidth,
            DisplaySettings.LogicalHeight);

        if (_scenes.QuitRequested)
        {
            Exit();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        if (_scenes.DrawsWorld)
        {
            var worldMatrix = Matrix.Multiply(_scenes.WorldViewMatrix, _presentation.TransformMatrix);
            _spriteBatch.Begin(
                transformMatrix: worldMatrix,
                samplerState: SamplerState.PointClamp,
                blendState: BlendState.AlphaBlend,
                rasterizerState: RasterizerState.CullNone);

            _scenes.DrawWorld(_spriteBatch);
            _spriteBatch.End();
        }

        _spriteBatch.Begin(
            transformMatrix: _presentation.TransformMatrix,
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend);

        _scenes.DrawScreen(_spriteBatch);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _audio?.StopAll();
            _scenes?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ToggleFullScreen()
    {
        ApplyDisplayMode(startup: false, toggleFullscreen: true);
        RefreshPresentation();
    }

    private void ApplyDisplayMode(bool startup, bool toggleFullscreen = false)
    {
        if (toggleFullscreen)
        {
            _graphics.IsFullScreen = !_graphics.IsFullScreen;
        }
        else if (startup)
        {
            _graphics.IsFullScreen = DisplaySettings.LaunchBorderlessFullscreen;
        }

        // Logical design is always 1920×1080. In borderless/fullscreen, size the back buffer
        // to the monitor so the logical frame scales to fill the screen.
        if (_graphics.IsFullScreen)
        {
            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = displayMode.Width;
            _graphics.PreferredBackBufferHeight = displayMode.Height;
        }
        else
        {
            _graphics.PreferredBackBufferWidth = DisplaySettings.PreferredWindowWidth;
            _graphics.PreferredBackBufferHeight = DisplaySettings.PreferredWindowHeight;
        }

        _graphics.HardwareModeSwitch = false;
        _graphics.ApplyChanges();
    }

    private void RefreshPresentation()
    {
        var backBuffer = GraphicsDevice.PresentationParameters;
        _presentation = DisplayPresentation.Create(
            backBuffer.BackBufferWidth,
            backBuffer.BackBufferHeight);
    }

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}
