namespace BattleCity.Shared.Chat;

public enum ChatCommandKind
{
    Normal,
    Global,
    Whisper,
    Heir,
    Kick,
    Ban,
    Warp,
    Summon,
    JoinCity,
    Spawn,
    Shutdown,
    Bans,
    Unban,
    News,
    SetNews,
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

        if (line.StartsWith("/g ", StringComparison.OrdinalIgnoreCase)
            || line.Equals("/g", StringComparison.OrdinalIgnoreCase))
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

        if (line.Equals("/heir", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("/heir ", StringComparison.OrdinalIgnoreCase))
        {
            // "/heir" / "/heir clear" / "/heir NamePrefix"
            var arg = line.Length <= 5 ? string.Empty : line[5..].Trim();
            return new ParsedChatCommand(ChatCommandKind.Heir, arg);
        }

        if (line.StartsWith("/kick ", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.Kick, line[6..].Trim());
        }

        if (line.StartsWith("/ban ", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.Ban, line[5..].Trim());
        }

        if (line.StartsWith("/warp ", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.Warp, line[6..].Trim());
        }

        if (line.StartsWith("/summon ", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.Summon, line[8..].Trim());
        }

        if (line.StartsWith("/city ", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.JoinCity, line[6..].Trim());
        }

        if (line.StartsWith("/joincity ", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.JoinCity, line[10..].Trim());
        }

        if (line.StartsWith("/spawn ", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.Spawn, line[7..].Trim());
        }

        if (line.Equals("/shutdown", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.Shutdown, string.Empty);
        }

        if (line.Equals("/bans", StringComparison.OrdinalIgnoreCase)
            || line.Equals("/banlist", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.Bans, string.Empty);
        }

        if (line.StartsWith("/unban ", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.Unban, line[7..].Trim());
        }

        if (line.Equals("/news", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.News, string.Empty);
        }

        if (line.StartsWith("/setnews ", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.SetNews, line[9..].Trim());
        }

        if (line.Equals("/setnews", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedChatCommand(ChatCommandKind.SetNews, string.Empty);
        }

        return new ParsedChatCommand(ChatCommandKind.Normal, line);
    }
}
