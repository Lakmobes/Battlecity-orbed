using BattleCity.Client.Network;
using BattleCity.Server;
using BattleCity.Shared.Catalogs;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Network.Packets;

/// <summary>
/// Headless smoke: start server, join 1 mayor + 3 soldiers, send movement, verify InGame.
/// </summary>
var cityName = "Buenos Aires";
if (!CityCatalog.TryGetId(cityName, out var cityId))
{
    Fail($"City '{cityName}' not found in catalog.");
}

var port = 15643;
var dbPath = Path.Combine(Path.GetTempPath(), $"battlecity-smoke-{Guid.NewGuid():N}.db");
var timeout = TimeSpan.FromSeconds(8);

Console.WriteLine("Battle City online smoke");
Console.WriteLine($"  City: {cityName} (id {cityId})");
Console.WriteLine($"  Port: {port}");
Console.WriteLine($"  DB:   {dbPath}");

using var server = new GameServer(dbPath);
server.Start("127.0.0.1", port);

using var cts = new CancellationTokenSource();
var tickTask = Task.Run(() =>
{
    const float dt = 1f / 60f;
    while (!cts.IsCancellationRequested)
    {
        server.Update(dt);
        Thread.Sleep(16);
    }
}, cts.Token);

var clients = new List<GameClient>();
try
{
    // 1 mayor + 3 soldiers
    var mayor = ConnectGuest($"Mayor{Random.Shared.Next(100, 999)}");
    clients.Add(mayor);
    JoinAsMayor(mayor, (byte)cityId);
    AssertTrue(mayor.IsInGame, "Mayor should be InGame");
    AssertTrue(mayor.IsMayor, "First joiner should be mayor");
    Console.WriteLine($"  Mayor OK  playerId={mayor.PlayerId} spawn=({mayor.SpawnState?.X},{mayor.SpawnState?.Y})");

    for (var i = 0; i < 3; i++)
    {
        var soldier = ConnectGuest($"Sold{Random.Shared.Next(100, 999)}");
        clients.Add(soldier);
        HireSoldier(mayor, soldier, (byte)cityId);
        AssertTrue(soldier.IsInGame, $"Soldier {i + 1} should be InGame");
        AssertTrue(!soldier.IsMayor, $"Soldier {i + 1} should not be mayor");
        Console.WriteLine($"  Soldier {i + 1} OK  playerId={soldier.PlayerId}");
    }

    // Drive tanks a bit so update packets exercise the session loop.
    Drive(clients, steps: 45);
    var connected = server.GetConnectedPlayers();
    AssertTrue(connected.Count >= 4, $"Expected >= 4 connected, got {connected.Count}");
    AssertTrue(connected.Count(p => p.IsMayor) == 1, "Expected exactly one mayor");
    AssertTrue(connected.Count(p => p.State == "InGame") == 4, "Expected 4 InGame players");

    Console.WriteLine();
    Console.WriteLine("SMOKE PASS — 1 mayor + 3 soldiers joined and moved.");
    Environment.ExitCode = 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("SMOKE FAIL: " + ex.Message);
    Environment.ExitCode = 1;
}
finally
{
    foreach (var client in clients)
    {
        client.Dispose();
    }

    cts.Cancel();
    try
    {
        tickTask.Wait(TimeSpan.FromSeconds(2));
    }
    catch
    {
        // ignored
    }

    server.Stop();
    try
    {
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }
    catch
    {
        // ignored
    }
}

GameClient ConnectGuest(string name)
{
    var client = new GameClient();
    if (!client.ConnectAndLogin("127.0.0.1", port, name, "guest", timeout))
    {
        client.Dispose();
        throw new InvalidOperationException($"Login failed for {name}: {client.LastError}");
    }

    client.EnterMeetingRoom();
    client.PollAvailable();
    return client;
}

void JoinAsMayor(GameClient mayor, byte city)
{
    mayor.ApplyToCity(city);
    if (!mayor.TryWaitForGameStart(timeout))
    {
        throw new InvalidOperationException($"Mayor join failed: {mayor.LastError}");
    }

    // Drain MayorUpdate / settle IsMayor.
    for (var i = 0; i < 30; i++)
    {
        mayor.Poll();
        if (mayor.IsMayor)
        {
            break;
        }

        Thread.Sleep(20);
    }
}

void HireSoldier(GameClient mayor, GameClient soldier, byte city)
{
    soldier.ApplyToCity(city);
    var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
    while (Environment.TickCount64 < deadline)
    {
        mayor.Poll();
        soldier.Poll();

        foreach (var ev in mayor.DrainEvents())
        {
            if (ev.Kind == GameClientEventKind.MayorHire)
            {
                mayor.AcceptApplicant();
            }
        }

        if (soldier.IsInGame)
        {
            return;
        }

        Thread.Sleep(20);
    }

    throw new InvalidOperationException($"Soldier hire/join failed: {soldier.LastError ?? "timeout"}");
}

void Drive(List<GameClient> group, int steps)
{
    for (var step = 0; step < steps; step++)
    {
        foreach (var client in group)
        {
            if (!client.IsInGame || client.SpawnState is not { } spawn)
            {
                continue;
            }

            var x = (ushort)Math.Clamp(spawn.X + step, 0, (int)ushort.MaxValue);
            var y = (ushort)Math.Clamp((int)spawn.Y, 0, (int)ushort.MaxValue);
            client.SendUpdate(new ClientUpdatePacket(x, y, turn: 0, move: 1, direction: 0));
            client.Poll();
        }

        Thread.Sleep(16);
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void Fail(string message) => throw new InvalidOperationException(message);
