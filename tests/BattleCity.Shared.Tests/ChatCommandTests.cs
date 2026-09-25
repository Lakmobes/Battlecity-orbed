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
    public void Parse_GlobalCommand_DoesNotSwallowOtherSlashCommands()
    {
        var command = ChatCommandParser.Parse("/good morning");

        Assert.Equal(ChatCommandKind.Normal, command.Kind);
        Assert.Equal("/good morning", command.Message);
    }

    [Fact]
    public void Parse_HeirCommand_DoesNotSwallowHeirloom()
    {
        var command = ChatCommandParser.Parse("/heirloom");
        Assert.Equal(ChatCommandKind.Normal, command.Kind);
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
    public void Parse_HeirCommand_ExtractsArgument()
    {
        var clear = ChatCommandParser.Parse("/heir clear");
        Assert.Equal(ChatCommandKind.Heir, clear.Kind);
        Assert.Equal("clear", clear.Message);

        var named = ChatCommandParser.Parse("/heir Alice");
        Assert.Equal(ChatCommandKind.Heir, named.Kind);
        Assert.Equal("Alice", named.Message);

        var bare = ChatCommandParser.Parse("/heir");
        Assert.Equal(ChatCommandKind.Heir, bare.Kind);
        Assert.Equal(string.Empty, bare.Message);
    }

    [Fact]
    public void Parse_KickAndBanCommands_ExtractName()
    {
        var kick = ChatCommandParser.Parse("/kick Alice");
        Assert.Equal(ChatCommandKind.Kick, kick.Kind);
        Assert.Equal("Alice", kick.Message);

        var ban = ChatCommandParser.Parse("/ban Bob");
        Assert.Equal(ChatCommandKind.Ban, ban.Kind);
        Assert.Equal("Bob", ban.Message);
    }

    [Fact]
    public void Parse_AdminMovementCommands()
    {
        var warp = ChatCommandParser.Parse("/warp Alice");
        Assert.Equal(ChatCommandKind.Warp, warp.Kind);
        Assert.Equal("Alice", warp.Message);

        var summon = ChatCommandParser.Parse("/summon Bob");
        Assert.Equal(ChatCommandKind.Summon, summon.Kind);
        Assert.Equal("Bob", summon.Message);

        var city = ChatCommandParser.Parse("/city Buenos Aires");
        Assert.Equal(ChatCommandKind.JoinCity, city.Kind);
        Assert.Equal("Buenos Aires", city.Message);

        var joinCity = ChatCommandParser.Parse("/joincity 27");
        Assert.Equal(ChatCommandKind.JoinCity, joinCity.Kind);
        Assert.Equal("27", joinCity.Message);
    }

    [Fact]
    public void Parse_SpawnAndShutdownCommands()
    {
        var spawn = ChatCommandParser.Parse("/spawn wall");
        Assert.Equal(ChatCommandKind.Spawn, spawn.Kind);
        Assert.Equal("wall", spawn.Message);

        var shutdown = ChatCommandParser.Parse("/shutdown");
        Assert.Equal(ChatCommandKind.Shutdown, shutdown.Kind);
        Assert.Equal(string.Empty, shutdown.Message);
    }

    [Fact]
    public void Parse_BanListAndNewsCommands()
    {
        var bans = ChatCommandParser.Parse("/bans");
        Assert.Equal(ChatCommandKind.Bans, bans.Kind);

        var unban = ChatCommandParser.Parse("/unban Alice");
        Assert.Equal(ChatCommandKind.Unban, unban.Kind);
        Assert.Equal("Alice", unban.Message);

        var news = ChatCommandParser.Parse("/news");
        Assert.Equal(ChatCommandKind.News, news.Kind);

        var setNews = ChatCommandParser.Parse("/setnews Hello world");
        Assert.Equal(ChatCommandKind.SetNews, setNews.Kind);
        Assert.Equal("Hello world", setNews.Message);
    }

    [Fact]
    public void ItemCatalog_TryParse_ResolvesIdNameAndAlias()
    {
        Assert.True(ItemCatalog.TryParse("8", out var byId));
        Assert.Equal(BattleCity.Shared.Data.ItemType.Wall, byId);

        Assert.True(ItemCatalog.TryParse("MedKit", out var byName));
        Assert.Equal(BattleCity.Shared.Data.ItemType.MedKit, byName);

        Assert.True(ItemCatalog.TryParse("cloak", out var byAlias));
        Assert.Equal(BattleCity.Shared.Data.ItemType.Cloak, byAlias);

        Assert.True(ItemCatalog.TryParse("Sleeper", out var sleeper));
        Assert.Equal(BattleCity.Shared.Data.ItemType.Sleeper, sleeper);

        Assert.False(ItemCatalog.TryParse("zzz", out _));
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
