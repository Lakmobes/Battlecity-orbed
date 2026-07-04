using BattleCity.Server;
using BattleCity.Server.Accounts;
using BattleCity.Shared.Constants;

const string DefaultHost = "127.0.0.1";
var port = NetworkConstants.TcpPort;
var host = DefaultHost;
var databasePath = Path.Combine(AppContext.BaseDirectory, "accounts.db");

for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedPort))
    {
        port = parsedPort;
    }

    if (args[i] == "--host" && i + 1 < args.Length)
    {
        host = args[i + 1];
    }

    if (args[i] == "--db" && i + 1 < args.Length)
    {
        databasePath = args[i + 1];
    }
}

if (args.Length >= 1 && args[0] == "create-account")
{
    if (args.Length < 3)
    {
        Console.WriteLine("Usage: create-account <username> <password> [town]");
        return;
    }

    using var accounts = new AccountDatabase(databasePath);
    var result = accounts.TryCreateAccount(
        args[1],
        args[2],
        town: args.Length > 3 ? args[3] : "Buenos Aires",
        email: string.Empty,
        fullName: args[1],
        state: string.Empty);

    Console.WriteLine(result switch
    {
        AccountCreateResult.Created => $"Account '{args[1]}' created in {databasePath}",
        AccountCreateResult.UsernameTaken => "Username already taken.",
        _ => "Invalid username or password.",
    });
    return;
}

using var server = new GameServer(databasePath);
server.Start(host, port);

var tickSeconds = GameServerTickRate.Seconds;
while (true)
{
    server.Update(tickSeconds);
    Thread.Sleep(GameServerTickRate.SleepMilliseconds);
}

internal static class GameServerTickRate
{
    public const float Seconds = 1f / 60f;
    public const int SleepMilliseconds = 16;
}
