namespace BattleCity.Server;

/// <summary>Persists server news text (legacy <c>news.txt</c>).</summary>
public sealed class ServerNewsStore
{
    public const int MaxNewsLength = 240;

    private readonly string _path;
    private readonly object _sync = new();
    private string _news = string.Empty;

    public ServerNewsStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        _path = string.IsNullOrEmpty(directory)
            ? "news.txt"
            : Path.Combine(directory, "news.txt");
        Reload();
    }

    public string News
    {
        get
        {
            lock (_sync)
            {
                return _news;
            }
        }
    }

    public void Reload()
    {
        lock (_sync)
        {
            try
            {
                _news = File.Exists(_path)
                    ? File.ReadAllText(_path)
                    : string.Empty;
            }
            catch (IOException)
            {
                _news = string.Empty;
            }
        }
    }

    public bool TrySetNews(string news)
    {
        news ??= string.Empty;
        if (news.Length > MaxNewsLength)
        {
            return false;
        }

        // Legacy appends CRLF when saving from admin panel.
        if (news.Length > 0 && !news.EndsWith("\r\n", StringComparison.Ordinal))
        {
            news += "\r\n";
        }

        lock (_sync)
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_path, news);
                _news = news;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
