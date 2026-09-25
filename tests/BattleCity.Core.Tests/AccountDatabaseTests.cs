using BattleCity.Server.Accounts;

using Xunit;

namespace BattleCity.Core.Tests;

public sealed class AccountDatabaseTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"bc-test-{Guid.NewGuid():N}.db");
    private readonly AccountDatabase _accounts;

    public AccountDatabaseTests()
    {
        _accounts = new AccountDatabase(_databasePath);
    }

    public void Dispose()
    {
        _accounts.Dispose();
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void TryCreateAccount_StoresHashedPassword()
    {
        Assert.Equal(AccountCreateResult.Created, _accounts.TryCreateAccount(
            "Tester",
            "secret123",
            town: "Buenos Aires",
            email: string.Empty,
            fullName: "Tester",
            state: string.Empty));

        var result = _accounts.TryLogin("Tester", "secret123", _ => false, out var account);
        Assert.Equal(AccountLoginResult.Success, result);
        Assert.Equal("Tester", account!.Username);
    }

    [Fact]
    public void TryLogin_RejectsWrongPassword()
    {
        _accounts.TryCreateAccount("Tester", "secret123", "Buenos Aires", string.Empty, "Tester", string.Empty);

        var result = _accounts.TryLogin("Tester", "wrong", _ => false, out _);
        Assert.Equal(AccountLoginResult.WrongPassword, result);
    }

    [Fact]
    public void IsGuestLogin_AcceptsGuestPassword()
    {
        Assert.True(AccountDatabase.IsGuestLogin("Guest123", "guest"));
    }

    [Fact]
    public void Ban_ListAndUnban_RoundTrip()
    {
        Assert.True(_accounts.TryAddBan("BadActor", "HostAdmin", "griefing"));
        Assert.True(_accounts.IsBanned("BadActor"));
        Assert.True(_accounts.IsBanned("badactor")); // case-insensitive

        var bans = _accounts.ListBans();
        Assert.Contains(bans, ban =>
            string.Equals(ban.Username, "BadActor", StringComparison.OrdinalIgnoreCase)
            && ban.BannedBy == "HostAdmin"
            && ban.Reason == "griefing");

        Assert.True(_accounts.TryRemoveBan("BadActor"));
        Assert.False(_accounts.IsBanned("BadActor"));
        Assert.Empty(_accounts.ListBans());
        Assert.False(_accounts.TryRemoveBan("BadActor"));
    }
}
