using BattleCity.Client.Assets;
using BattleCity.Client.Audio;
using BattleCity.Client.Network;
using BattleCity.Client.Rendering;

namespace BattleCity.Client.Scenes;

public sealed class SceneContext : IDisposable
{
    public SceneContext(AssetService assets, AudioService audio)
    {
        Assets = assets;
        Audio = audio;
        EmbeddedLocalServer = new EmbeddedLocalServer();
    }

    public AssetService Assets { get; }

    public AudioService Audio { get; }

    public EmbeddedLocalServer EmbeddedLocalServer { get; }

    public string PlayerName { get; set; } = "Guest";

    public string PlayerPassword { get; set; } = "guest";

    public string SelectedCity { get; set; } = "Buenos Aires";

    public string CityDesign { get; set; } = "demo";

    public string ServerHost { get; set; } = "127.0.0.1";

    public int ServerPort { get; set; } = BattleCity.Shared.Constants.NetworkConstants.TcpPort;

    public string? LoginStatusMessage { get; set; }

    public GameClient? NetworkClient { get; set; }

    /// <summary>Updated each frame before scene update (back buffer → logical coordinate mapping).</summary>
    public DisplayPresentation Presentation { get; set; } = DisplayPresentation.Create(
        DisplaySettings.LogicalWidth,
        DisplaySettings.LogicalHeight);

    public void Dispose()
    {
        NetworkClient?.Dispose();
        NetworkClient = null;
        EmbeddedLocalServer.Dispose();
    }
}
