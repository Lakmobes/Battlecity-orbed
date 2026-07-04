using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Chat;

using Xunit;

namespace BattleCity.Shared.Tests;

public class ChatCommandTests
{
    [Fact]
    public void Parse_GlobalCommand_ExtractsMessage()
    {
        var command = ChatCommandParser.Parse("/g hello everyone");

        Assert.Equal(ChatCommandKind.Global, command.Kind);
        Assert.Equal("hello everyone", command.Message);
    }

    [Fact]
    public void Parse_WhisperCommand_ExtractsRecipientAndMessage()
    {
        var command = ChatCommandParser.Parse("/pm alice secret msg");

        Assert.Equal(ChatCommandKind.Whisper, command.Kind);
        Assert.Equal("alice", command.WhisperRecipient);
        Assert.Equal("secret msg", command.Message);
    }

    [Fact]
    public void Parse_NormalMessage_PreservesText()
    {
        var command = ChatCommandParser.Parse("team chat");

        Assert.Equal(ChatCommandKind.Normal, command.Kind);
        Assert.Equal("team chat", command.Message);
    }

    [Fact]
    public void DeathChatMessages_AppendsFriendlyFireSuffix()
    {
        var message = DeathChatMessages.Format("Tanker", victimCityId: 2, killerCity: 2, playerId: 4);

        Assert.Contains("Tanker", message, StringComparison.Ordinal);
        Assert.Contains("(Friendly Fire!)", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeathChatMessages_AppendsKillerCityName()
    {
        var killerCity = (byte)CityCatalog.Names.ToList().IndexOf("Buenos Aires");
        var message = DeathChatMessages.Format("Tanker", victimCityId: 0, killerCity, playerId: 4);

        Assert.Contains("(Buenos Aires)", message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeathChatMessages_OmitsCitySuffixWhenKillerUnknown()
    {
        var message = DeathChatMessages.Format("Tanker", victimCityId: 0, killerCity: byte.MaxValue, playerId: 4);

        Assert.DoesNotContain('(', message);
    }

    [Fact]
    public void WhisperRecipientMatcher_RequiresUniquePrefixMatch()
    {
        var players = new (byte, string)[]
        {
            (1, "Alice"),
            (2, "Alex"),
        };

        Assert.False(WhisperRecipientMatcher.TryMatch("Al", 3, players, out _, out _));
        Assert.True(WhisperRecipientMatcher.TryMatch("Ali", 3, players, out var id, out var name));
        Assert.Equal((byte)1, id);
        Assert.Equal("Alice", name);
    }
}
