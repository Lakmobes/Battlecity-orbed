using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace BattleCity.Client.Assets;

public sealed class AssetService
{
    private readonly ContentManager _content;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpriteFont> _fonts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingFonts = new(StringComparer.OrdinalIgnoreCase);
    private Texture2D _pixel = null!;

    public AssetService(ContentManager content)
    {
        _content = content;
    }

    public Texture2D Pixel => _pixel;

    public void Initialize(GraphicsDevice graphicsDevice)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public bool IsTextureLoaded(string contentPath) => _textures.ContainsKey(contentPath);

    public Texture2D LoadTexture(string contentPath)
    {
        if (_textures.TryGetValue(contentPath, out var cached))
        {
            return cached;
        }

        if (_missingTextures.Contains(contentPath))
        {
            return _pixel;
        }

        try
        {
            var texture = _content.Load<Texture2D>(contentPath);
            _textures[contentPath] = texture;
            return texture;
        }
        catch (ContentLoadException)
        {
            _missingTextures.Add(contentPath);
            return _pixel;
        }
    }

    public Texture2D Ground => LoadTexture(LegacySpriteNames.Ground);
    public Texture2D Lava => LoadTexture(LegacySpriteNames.Lava);
    public Texture2D Rocks => LoadTexture(LegacySpriteNames.Rocks);
    public Texture2D Tanks => LoadTexture(LegacySpriteNames.Tanks);
    public Texture2D MiniMapColors => LoadTexture(LegacySpriteNames.MiniMapColors);
    public Texture2D Population => LoadTexture(LegacySpriteNames.Population);
    public Texture2D InventorySelection => LoadTexture(LegacySpriteNames.InventorySelection);
    public Texture2D Health => LoadTexture(LegacySpriteNames.Health);
    public Texture2D BlackNumbers => LoadTexture(LegacySpriteNames.BlackNumbers);
    public Texture2D Items => LoadTexture(LegacySpriteNames.Items);

    public SpriteFont LoadFont(string contentPath)
    {
        if (_fonts.TryGetValue(contentPath, out var cached))
        {
            return cached;
        }

        if (_missingFonts.Contains(contentPath))
        {
            throw new InvalidOperationException($"Font not loaded: {contentPath}");
        }

        try
        {
            var font = _content.Load<SpriteFont>(contentPath);
            _fonts[contentPath] = font;
            return font;
        }
        catch (ContentLoadException)
        {
            _missingFonts.Add(contentPath);
            throw;
        }
    }
}
