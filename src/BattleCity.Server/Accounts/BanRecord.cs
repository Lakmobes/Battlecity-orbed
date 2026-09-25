namespace BattleCity.Server.Accounts;

public sealed class BanRecord
{
    public long Id { get; init; }

    public required string Username { get; init; }

    public required string Reason { get; init; }

    public required string BannedBy { get; init; }

    public required string CreatedUtc { get; init; }
}
