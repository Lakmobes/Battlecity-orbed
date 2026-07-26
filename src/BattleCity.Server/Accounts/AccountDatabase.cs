using Microsoft.Data.Sqlite;

namespace BattleCity.Server.Accounts;

public enum AccountCreateResult
{
    Created,
    UsernameTaken,
    InvalidInput,
}

public enum AccountLoginResult
{
    Success,
    NotFound,
    WrongPassword,
    AlreadyLoggedIn,
}

public sealed class AccountDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly object _sync = new();

    public AccountDatabase(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
        EnsureSchema();
    }

    public static bool IsGuestLogin(string username, string password) =>
        string.Equals(password.Trim(), "guest", StringComparison.OrdinalIgnoreCase);

    public AccountCreateResult TryCreateAccount(
        string username,
        string password,
        string town,
        string email,
        string fullName,
        string state,
        bool isAdmin = false)
    {
        username = NormalizeUsername(username);
        if (!IsValidUsername(username) || string.IsNullOrWhiteSpace(password))
        {
            return AccountCreateResult.InvalidInput;
        }

        var (hash, salt) = PasswordHasher.HashPassword(password.Trim());
        var displayName = string.IsNullOrWhiteSpace(fullName) ? username : fullName.Trim();
        var homeTown = string.IsNullOrWhiteSpace(town) ? "Buenos Aires" : town.Trim();
        // Legacy convenience: username "admin" starts as admin unless explicitly cleared later.
        var admin = isAdmin || string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase);

        lock (_sync)
        {
            using var exists = _connection.CreateCommand();
            exists.CommandText = "SELECT 1 FROM accounts WHERE username = $username LIMIT 1;";
            exists.Parameters.AddWithValue("$username", username);
            if (exists.ExecuteScalar() is not null)
            {
                return AccountCreateResult.UsernameTaken;
            }

            using var insert = _connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO accounts (
                    username, password_hash, password_salt, display_name, town, email, state, is_admin, created_utc)
                VALUES ($username, $hash, $salt, $displayName, $town, $email, $state, $isAdmin, $createdUtc);
                """;
            insert.Parameters.AddWithValue("$username", username);
            insert.Parameters.AddWithValue("$hash", hash);
            insert.Parameters.AddWithValue("$salt", salt);
            insert.Parameters.AddWithValue("$displayName", displayName);
            insert.Parameters.AddWithValue("$town", homeTown);
            insert.Parameters.AddWithValue("$email", email.Trim());
            insert.Parameters.AddWithValue("$state", state.Trim());
            insert.Parameters.AddWithValue("$isAdmin", admin ? 1 : 0);
            insert.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("O"));
            insert.ExecuteNonQuery();
        }

        return AccountCreateResult.Created;
    }

    public AccountLoginResult TryLogin(
        string username,
        string password,
        Func<string, bool> isUsernameAlreadyOnline,
        out AccountRecord? account)
    {
        account = null;
        username = NormalizeUsername(username);

        if (IsGuestLogin(username, password) || string.IsNullOrWhiteSpace(username))
        {
            return AccountLoginResult.NotFound;
        }

        lock (_sync)
        {
            using var query = _connection.CreateCommand();
            query.CommandText = """
                SELECT id, username, password_hash, password_salt, display_name, town, points, deaths, is_admin
                FROM accounts
                WHERE username = $username
                LIMIT 1;
                """;
            query.Parameters.AddWithValue("$username", username);

            using var reader = query.ExecuteReader();
            if (!reader.Read())
            {
                return AccountLoginResult.NotFound;
            }

            var hash = reader.GetString(2);
            var salt = reader.GetString(3);
            if (!PasswordHasher.VerifyPassword(password.Trim(), hash, salt))
            {
                return AccountLoginResult.WrongPassword;
            }

            account = new AccountRecord
            {
                Id = reader.GetInt64(0),
                Username = reader.GetString(1),
                DisplayName = reader.GetString(4),
                Town = reader.GetString(5),
                Points = reader.GetInt32(6),
                Deaths = reader.GetInt32(7),
                IsAdmin = reader.GetInt32(8) != 0,
            };
        }

        if (isUsernameAlreadyOnline(account.Username))
        {
            account = null;
            return AccountLoginResult.AlreadyLoggedIn;
        }

        return AccountLoginResult.Success;
    }

    public IReadOnlyList<AccountRecord> ListAccounts()
    {
        lock (_sync)
        {
            using var query = _connection.CreateCommand();
            query.CommandText = """
                SELECT id, username, display_name, town, points, deaths, is_admin
                FROM accounts
                ORDER BY username COLLATE NOCASE;
                """;

            var results = new List<AccountRecord>();
            using var reader = query.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new AccountRecord
                {
                    Id = reader.GetInt64(0),
                    Username = reader.GetString(1),
                    DisplayName = reader.GetString(2),
                    Town = reader.GetString(3),
                    Points = reader.GetInt32(4),
                    Deaths = reader.GetInt32(5),
                    IsAdmin = reader.GetInt32(6) != 0,
                });
            }

            return results;
        }
    }

    public bool TrySetAdmin(string username, bool isAdmin)
    {
        username = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        lock (_sync)
        {
            using var update = _connection.CreateCommand();
            update.CommandText = "UPDATE accounts SET is_admin = $isAdmin WHERE username = $username;";
            update.Parameters.AddWithValue("$isAdmin", isAdmin ? 1 : 0);
            update.Parameters.AddWithValue("$username", username);
            return update.ExecuteNonQuery() > 0;
        }
    }

    public void IncrementDeaths(string username)
    {
        username = NormalizeUsername(username);
        lock (_sync)
        {
            using var update = _connection.CreateCommand();
            update.CommandText = "UPDATE accounts SET deaths = deaths + 1 WHERE username = $username;";
            update.Parameters.AddWithValue("$username", username);
            update.ExecuteNonQuery();
        }
    }

    public void AdjustPoints(string username, int delta)
    {
        if (delta == 0)
        {
            return;
        }

        username = NormalizeUsername(username);
        lock (_sync)
        {
            using var update = _connection.CreateCommand();
            update.CommandText = """
                UPDATE accounts
                SET points = MAX(0, points + $delta)
                WHERE username = $username;
                """;
            update.Parameters.AddWithValue("$delta", delta);
            update.Parameters.AddWithValue("$username", username);
            update.ExecuteNonQuery();
        }
    }

    public void Dispose() => _connection.Dispose();

    private void EnsureSchema()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS accounts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL COLLATE NOCASE UNIQUE,
                password_hash TEXT NOT NULL,
                password_salt TEXT NOT NULL,
                display_name TEXT NOT NULL DEFAULT '',
                town TEXT NOT NULL DEFAULT 'Buenos Aires',
                email TEXT NOT NULL DEFAULT '',
                state TEXT NOT NULL DEFAULT '',
                points INTEGER NOT NULL DEFAULT 0,
                deaths INTEGER NOT NULL DEFAULT 0,
                is_admin INTEGER NOT NULL DEFAULT 0,
                created_utc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();

        using var migrate = _connection.CreateCommand();
        migrate.CommandText = "PRAGMA table_info(accounts);";
        var hasAdmin = false;
        using (var reader = migrate.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "is_admin", StringComparison.OrdinalIgnoreCase))
                {
                    hasAdmin = true;
                    break;
                }
            }
        }

        if (!hasAdmin)
        {
            using var alter = _connection.CreateCommand();
            alter.CommandText = "ALTER TABLE accounts ADD COLUMN is_admin INTEGER NOT NULL DEFAULT 0;";
            alter.ExecuteNonQuery();

            using var seedAdmin = _connection.CreateCommand();
            seedAdmin.CommandText = "UPDATE accounts SET is_admin = 1 WHERE username = 'admin';";
            seedAdmin.ExecuteNonQuery();
        }
    }

    private static string NormalizeUsername(string username) => username.Trim();

    private static bool IsValidUsername(string username) =>
        username.Length is >= 1 and <= 15
        && username.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
}
