using System.Net.Sockets;

using BattleCity.Server;
using BattleCity.Shared.Constants;

namespace BattleCity.Client.Network;

/// <summary>
/// In-process game server for the "Play Online (Local Server)" menu path.
/// If port 5643 is already taken (e.g. Server.Host), treats that as success and does not take ownership.
/// </summary>
public sealed class EmbeddedLocalServer : IDisposable
{
    private readonly object _sync = new();
    private GameServer? _server;
    private CancellationTokenSource? _cts;
    private Task? _tickTask;
    private bool _ownsServer;

    public enum EnsureResult
    {
        Started,
        AlreadyOwned,
        ExternalListener,
        Failed,
    }

    public bool IsOwnedRunning
    {
        get
        {
            lock (_sync)
            {
                return _ownsServer && _server?.IsRunning == true;
            }
        }
    }

    public EnsureResult EnsureRunning(out string? message)
    {
        lock (_sync)
        {
            if (_ownsServer && _server?.IsRunning == true)
            {
                message = $"Local server already running on 127.0.0.1:{NetworkConstants.TcpPort}";
                return EnsureResult.AlreadyOwned;
            }
        }

        GameServer? server = null;
        try
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BattleCity",
                "local-accounts.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            server = new GameServer(dbPath);
            server.Start("127.0.0.1", NetworkConstants.TcpPort);

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(() => TickLoop(server, cts.Token), cts.Token);

            lock (_sync)
            {
                DisposeOwnedUnlocked();
                _server = server;
                _cts = cts;
                _tickTask = tickTask;
                _ownsServer = true;
                server = null; // ownership transferred
            }

            message = $"Local server started on 127.0.0.1:{NetworkConstants.TcpPort}";
            return EnsureResult.Started;
        }
        catch (SocketException ex) when (IsAddressInUse(ex))
        {
            server?.Dispose();
            message = $"Using existing server on port {NetworkConstants.TcpPort}";
            return EnsureResult.ExternalListener;
        }
        catch (Exception ex)
        {
            server?.Dispose();
            message = $"Could not start local server: {ex.Message}. Start Server.Host manually, then connect.";
            return EnsureResult.Failed;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            DisposeOwnedUnlocked();
        }
    }

    private void DisposeOwnedUnlocked()
    {
        if (!_ownsServer)
        {
            return;
        }

        _ownsServer = false;
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // Ignore cancel races during shutdown.
        }

        try
        {
            _server?.Stop();
            _server?.Dispose();
        }
        catch
        {
            // Best-effort shutdown.
        }

        _server = null;
        _cts?.Dispose();
        _cts = null;
        _tickTask = null;
    }

    private static void TickLoop(GameServer server, CancellationToken token)
    {
        const float dt = 1f / 60f;
        while (!token.IsCancellationRequested)
        {
            try
            {
                server.Update(dt);
            }
            catch
            {
                // Keep the listen loop alive; individual session errors are handled inside Update.
            }

            Thread.Sleep(16);
        }
    }

    private static bool IsAddressInUse(SocketException ex) =>
        ex.SocketErrorCode == SocketError.AddressAlreadyInUse
        || ex.ErrorCode == 10048; // WSAEADDRINUSE
}
