namespace BattleCity.Shared.Chat;

public enum ChatCommandKind
{
    Normal,
    Global,
    Whisper,
}

public readonly struct ParsedChatCommand
{
    public ParsedChatCommand(ChatCommandKind kind, string message, string whisperRecipient = "")
    {
        Kind = kind;
        Message = message;
        WhisperRecipient = whisperRecipient;
    }

    public ChatCommandKind Kind { get; }

    public string Message { get; }

    public string WhisperRecipient { get; }
}

public static class ChatCommandParser
{
    public static ParsedChatCommand Parse(string line)
    {
        line = line.Trim();
        if (line.Length == 0)
        {
            return new ParsedChatCommand(ChatCommandKind.Normal, string.Empty);
        }

        if (line.StartsWith("/g", StringComparison.OrdinalIgnoreCase))
        {
            var message = line.Length <= 2 ? string.Empty : line[2..].TrimStart();
            return new ParsedChatCommand(ChatCommandKind.Global, message);
        }

        if (line.StartsWith("/pm ", StringComparison.OrdinalIgnoreCase))
        {
            var afterCommand = line[4..];
            var spaceIndex = afterCommand.IndexOf(' ');
            if (spaceIndex <= 0)
            {
                return new ParsedChatCommand(ChatCommandKind.Whisper, string.Empty);
            }

            var recipient = afterCommand[..spaceIndex].Trim();
            var message = afterCommand[(spaceIndex + 1)..].TrimStart();
            return new ParsedChatCommand(ChatCommandKind.Whisper, message, recipient);
        }

        return new ParsedChatCommand(ChatCommandKind.Normal, line);
    }
}
