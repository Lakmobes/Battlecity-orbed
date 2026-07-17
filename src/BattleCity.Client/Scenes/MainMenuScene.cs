using BattleCity.Client.Input;
using BattleCity.Client.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using BattleCity.Shared.Data;

namespace BattleCity.Client.Scenes;

public sealed class MainMenuScene : IScene
{
    private static readonly string[] MenuItems =
    [
        "Play Offline (Buenos Aires)",
        "Play Online (Local Server)",
        "Quit",
    ];

    private readonly SceneContext _context;
    private readonly ScreenUiRenderer _ui;
    private readonly MenuInputReader _input = new();
    private int _selectedIndex;

    public MainMenuScene(SceneContext context)
    {
        _context = context;
        _ui = new ScreenUiRenderer(context.Assets);
    }

    public bool DrawsWorld => false;

    public Matrix WorldViewMatrix => Matrix.Identity;

    public void LoadContent()
    {
        _ui.LoadContent();
        _input.Reset();
    }

    public SceneTransition Update(GameTime gameTime, int screenWidth, int screenHeight)
    {
        var menuInput = _input.Poll();
        if (menuInput.MoveUpPressed)
        {
            _selectedIndex = (_selectedIndex + MenuItems.Length - 1) % MenuItems.Length;
        }

        if (menuInput.MoveDownPressed)
        {
            _selectedIndex = (_selectedIndex + 1) % MenuItems.Length;
        }

        if (menuInput.ConfirmPressed)
        {
            _context.Audio.Play(SoundId.Click);
            if (_selectedIndex == 0)
            {
                _context.PlayerName = "Player";
                _context.SelectedCity = "Buenos Aires";
                _context.CityDesign = "demo";
            }

            if (_selectedIndex == 1)
            {
                _context.PlayerName = $"Guest{Random.Shared.Next(100, 999)}";
                _context.PlayerPassword = "guest";
                _context.ServerHost = "127.0.0.1";
            }

            return _selectedIndex switch
            {
                0 => SceneTransition.InGameOffline,
                1 => SceneTransition.Login,
                _ => SceneTransition.Quit,
            };
        }

        if (menuInput.CancelPressed)
        {
            // Esc is used in-game to return here; ignore it on the menu so a held Esc
            // does not immediately quit. Use the Quit menu item to exit.
            return SceneTransition.None;
        }

        return SceneTransition.None;
    }

    public void DrawWorld(SpriteBatch spriteBatch)
    {
    }

    public void DrawScreen(SpriteBatch spriteBatch)
    {
        _ui.DrawBackdrop(spriteBatch, RenderConstants.DefaultWindowWidth, RenderConstants.DefaultWindowHeight);
        _ui.DrawTitle(spriteBatch, RenderConstants.DefaultWindowWidth);
        _ui.DrawMenu(
            spriteBatch,
            RenderConstants.DefaultWindowWidth,
            RenderConstants.DefaultWindowHeight,
            MenuItems,
            _selectedIndex,
            "Up/Down - select   Enter - confirm");
    }

    public void Dispose()
    {
    }
}
