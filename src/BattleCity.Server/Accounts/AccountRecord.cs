namespace BattleCity.Server.Accounts;

public sealed class AccountRecord
{
    public long Id { get; init; }

    public required string Username { get; init; }

    public required string DisplayName { get; init; }

    public required string Town { get; init; }

    public string Email { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public int Points { get; init; }

    public int MonthlyPoints { get; init; }

    public int SeasonPoints { get; init; }

    public int Deaths { get; init; }

    public bool IsAdmin { get; init; }
}
