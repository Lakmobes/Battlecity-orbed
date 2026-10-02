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

    [Fact]
    public void AdminEdit_UpdatesPointsDeathsAndProfile()
    {
        Assert.Equal(AccountCreateResult.Created, _accounts.TryCreateAccount(
            "Editor",
            "secret123",
            town: "Buenos Aires",
            email: "old@x.com",
            fullName: "Old Name",
            state: "BA"));

        Assert.True(_accounts.TryApplyAdminEdit(
            "Editor",
            newPassword: null,
            displayName: "New Name",
            town: "Berlin",
            email: "new@x.com",
            state: "DE",
            points: 99,
            deaths: 4,
            isAdmin: true));

        Assert.True(_accounts.TryGetAccountForAdminEdit("Editor", out var account));
        Assert.Equal("New Name", account!.DisplayName);
        Assert.Equal("Berlin", account.Town);
        Assert.Equal("new@x.com", account.Email);
        Assert.Equal("DE", account.State);
        Assert.Equal(99, account.Points);
        Assert.Equal(4, account.Deaths);
        Assert.True(account.IsAdmin);
    }

    [Fact]
    public void AdjustPoints_RollsMonthAndKeepsSeasonUntilReset()
    {
        Assert.Equal(AccountCreateResult.Created, _accounts.TryCreateAccount(
            "Ace", "secret123", "Buenos Aires", string.Empty, "Ace", string.Empty));
        Assert.Equal(AccountCreateResult.Created, _accounts.TryCreateAccount(
            "Bee", "secret123", "Buenos Aires", string.Empty, "Bee", string.Empty));

        _accounts.AdjustPoints("Ace", 10, "2026-09");
        _accounts.AdjustPoints("Ace", 5, "2026-09");
        _accounts.AdjustPoints("Bee", 4, "2026-09");

        var september = _accounts.ListTopByPoints("monthly_points", 10);
        Assert.Equal(["Ace", "Bee"], september.Select(account => account.Username).ToArray());
        Assert.Equal(15, september[0].MonthlyPoints);
        Assert.Equal(15, september[0].Points);
        Assert.Equal(15, september[0].SeasonPoints);

        _accounts.AdjustPoints("Ace", 3, "2026-10");
        var october = _accounts.ListTopByPoints("points", 10).Single(account => account.Username == "Ace");
        Assert.Equal(18, october.Points);
        Assert.Equal(3, october.MonthlyPoints);
        Assert.Equal(18, october.SeasonPoints);

        _accounts.StartSeason("  Spring Cup  ");
        Assert.Equal("Spring Cup", _accounts.SeasonName);
        var reset = _accounts.ListTopByPoints("points", 10).Single(account => account.Username == "Ace");
        Assert.Equal(0, reset.SeasonPoints);
        Assert.Equal(18, reset.Points);
        Assert.Equal(3, reset.MonthlyPoints);
        Assert.Empty(_accounts.ListTopByPoints("season_points", 10));

        _accounts.AdjustPoints("Ace", -1000, "2026-10");
        Assert.DoesNotContain(
            _accounts.ListTopByPoints("points", 10),
            account => account.Username == "Ace");
    }
}
