namespace BattleCity.Server.Accounts;

public sealed class AccountRecord
{
    public long Id { get; init; }

    public required string Username { get; init; }

    public required string DisplayName { get; init; }

    public required string Town { get; init; }

    public int Points { get; init; }

    public int Deaths { get; init; }

    public bool IsAdmin { get; init; }
}
