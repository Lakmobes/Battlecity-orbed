using Microsoft.Xna.Framework;

namespace BattleCity.Client.Chat;

public readonly struct ChatLine
{
    public ChatLine(string text, Color color)
    {
        Text = text;
        Color = color;
    }

    public string Text { get; }

    public Color Color { get; }
}

public sealed class InGameChatLog
{
    private readonly int _maxLines;

    public InGameChatLog(int maxLines = 8)
    {
        _maxLines = Math.Clamp(maxLines, 1, 40);
    }
    private const int WrapWidth = 75;

    private readonly Queue<ChatLine> _lines = new();

    public IReadOnlyCollection<ChatLine> Lines => _lines;

    public void Append(string text, Color color)
    {
        foreach (var wrapped in Wrap(text))
        {
            if (_lines.Count >= _maxLines)
            {
                _lines.Dequeue();
            }

            _lines.Enqueue(new ChatLine(wrapped, color));
        }
    }

    private static IEnumerable<string> Wrap(string text)
    {
        if (text.Length <= WrapWidth)
        {
            yield return text;
            yield break;
        }

        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(WrapWidth, text.Length - start);
            yield return text.Substring(start, length);
            start += length;
        }
    }
}
