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
                SELECT id, username, password_hash, password_salt, display_name, town, points, deaths, is_admin, monthly_points, orbs, assists
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
                MonthlyPoints = reader.GetInt32(9),
                Orbs = reader.GetInt32(10),
                Assists = reader.GetInt32(11),
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

    public bool TryGetAccountForAdminEdit(string username, out AccountRecord? account)
    {
        account = null;
        username = NormalizeUsername(username);
        if (string.IsNullOrEmpty(username))
        {
            return false;
        }

        lock (_sync)
        {
            using var query = _connection.CreateCommand();
            query.CommandText = """
                SELECT id, username, display_name, town, email, state, points, monthly_points, deaths, is_admin
                FROM accounts
                WHERE username = $username
                LIMIT 1;
                """;
            query.Parameters.AddWithValue("$username", username);
            using var reader = query.ExecuteReader();
            if (!reader.Read())
            {
                return false;
            }

            account = new AccountRecord
            {
                Id = reader.GetInt64(0),
                Username = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Town = reader.GetString(3),
                Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                State = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Points = reader.GetInt32(6),
                MonthlyPoints = reader.GetInt32(7),
                Deaths = reader.GetInt32(8),
                IsAdmin = reader.GetInt32(9) != 0,
            };
            return true;
        }
    }

    /// <summary>
    /// Applies admin account edits. Empty <paramref name="newPassword"/> keeps the current hash.
    /// </summary>
    public bool TryApplyAdminEdit(
        string username,
        string? newPassword,
        string displayName,
        string town,
        string email,
        string state,
        int points,
        int deaths,
        bool isAdmin)
    {
        username = NormalizeUsername(username);
        if (string.IsNullOrEmpty(username) || !IsValidUsername(username))
        {
            return false;
        }

        points = Math.Max(0, points);
        deaths = Math.Max(0, deaths);
        displayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName.Trim();
        town = string.IsNullOrWhiteSpace(town) ? "Buenos Aires" : town.Trim();
        email = (email ?? string.Empty).Trim();
        state = (state ?? string.Empty).Trim();
        // Username "admin" stays admin unless explicitly demoted via Host (legacy convenience).
        if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            isAdmin = true;
        }

        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                var (hash, salt) = PasswordHasher.HashPassword(newPassword.Trim());
                using var updatePass = _connection.CreateCommand();
                updatePass.CommandText = """
                    UPDATE accounts SET
                        password_hash = $hash,
                        password_salt = $salt,
                        display_name = $displayName,
                        town = $town,
                        email = $email,
                        state = $state,
                        points = $points,
                        deaths = $deaths,
                        is_admin = $isAdmin
                    WHERE username = $username;
                    """;
                updatePass.Parameters.AddWithValue("$hash", hash);
                updatePass.Parameters.AddWithValue("$salt", salt);
                updatePass.Parameters.AddWithValue("$displayName", displayName);
                updatePass.Parameters.AddWithValue("$town", town);
                updatePass.Parameters.AddWithValue("$email", email);
                updatePass.Parameters.AddWithValue("$state", state);
                updatePass.Parameters.AddWithValue("$points", points);
                updatePass.Parameters.AddWithValue("$deaths", deaths);
                updatePass.Parameters.AddWithValue("$isAdmin", isAdmin ? 1 : 0);
                updatePass.Parameters.AddWithValue("$username", username);
                return updatePass.ExecuteNonQuery() > 0;
            }

            using var update = _connection.CreateCommand();
            update.CommandText = """
                UPDATE accounts SET
                    display_name = $displayName,
                    town = $town,
                    email = $email,
                    state = $state,
                    points = $points,
                    deaths = $deaths,
                    is_admin = $isAdmin
                WHERE username = $username;
                """;
            update.Parameters.AddWithValue("$displayName", displayName);
            update.Parameters.AddWithValue("$town", town);
            update.Parameters.AddWithValue("$email", email);
            update.Parameters.AddWithValue("$state", state);
            update.Parameters.AddWithValue("$points", points);
            update.Parameters.AddWithValue("$deaths", deaths);
            update.Parameters.AddWithValue("$isAdmin", isAdmin ? 1 : 0);
            update.Parameters.AddWithValue("$username", username);
            return update.ExecuteNonQuery() > 0;
        }
    }

    public void IncrementOrbs(string username) => IncrementColumn(username, "orbs");

    public void IncrementAssists(string username) => IncrementColumn(username, "assists");

    private void IncrementColumn(string username, string column)
    {
        username = NormalizeUsername(username);
        if (column is not ("orbs" or "assists"))
        {
            return;
        }

        lock (_sync)
        {
            using var update = _connection.CreateCommand();
            update.CommandText = $"UPDATE accounts SET {column} = {column} + 1 WHERE username = $username;";
            update.Parameters.AddWithValue("$username", username);
            update.ExecuteNonQuery();
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

    public void AdjustPoints(string username, int delta, string? monthKey = null)
    {
        if (delta == 0)
        {
            return;
        }

        username = NormalizeUsername(username);
        var month = string.IsNullOrWhiteSpace(monthKey)
            ? DateTime.Now.ToString("yyyy-MM")
            : monthKey.Trim();
        lock (_sync)
        {
            using var update = _connection.CreateCommand();
            update.CommandText = """
                UPDATE accounts
                SET points = MAX(0, points + $delta),
                    monthly_points = CASE
                        WHEN monthly_period = $month THEN MAX(0, monthly_points + $delta)
                        ELSE MAX(0, $delta)
                    END,
                    monthly_period = $month,
                    season_points = MAX(0, season_points + $delta)
                WHERE username = $username;
                """;
            update.Parameters.AddWithValue("$delta", delta);
            update.Parameters.AddWithValue("$month", month);
            update.Parameters.AddWithValue("$username", username);
            update.ExecuteNonQuery();
        }
    }

    public string SeasonName
    {
        get
        {
            lock (_sync)
            {
                return ReadSetting("season_name") ?? "Season";
            }
        }
    }

    public void StartSeason(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Season" : name.Trim();
        name = new string(name.Where(static ch => ch is >= (char)32 and <= (char)126).ToArray());
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Season";
        }

        if (name.Length > 24)
        {
            name = name[..24];
        }

        lock (_sync)
        {
            using var reset = _connection.CreateCommand();
            reset.CommandText = "UPDATE accounts SET season_points = 0;";
            reset.ExecuteNonQuery();
            WriteSetting("season_name", name);
        }
    }

    public IReadOnlyList<AccountRecord> ListTopByPoints(string column, int limit)
    {
        column = column switch
        {
            "monthly_points" => "monthly_points",
            "season_points" => "season_points",
            _ => "points",
        };
        limit = Math.Clamp(limit, 1, 20);

        lock (_sync)
        {
            using var query = _connection.CreateCommand();
            query.CommandText = $"""
                SELECT username, display_name, points, monthly_points, season_points
                FROM accounts
                WHERE {column} > 0
                ORDER BY {column} DESC, username COLLATE NOCASE
                LIMIT {limit};
                """;

            var results = new List<AccountRecord>();
            using var reader = query.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new AccountRecord
                {
                    Username = reader.GetString(0),
                    DisplayName = reader.GetString(1),
                    Town = string.Empty,
                    Points = reader.GetInt32(2),
                    MonthlyPoints = reader.GetInt32(3),
                    SeasonPoints = reader.GetInt32(4),
                });
            }

            return results;
        }
    }

    /// <summary>
    /// Legacy monthly-style board: <c>(Points * 10000) / Deaths</c> for accounts with more than 100 deaths.
    /// </summary>
    public List<(string Name, int Points)> ListTopByPointsPerDeath(int limit)
    {
        limit = Math.Clamp(limit, 1, 20);
        lock (_sync)
        {
            using var query = _connection.CreateCommand();
            query.CommandText = $"""
                SELECT username, display_name, (points * 10000) / deaths AS ratio
                FROM accounts
                WHERE deaths > 100 AND points > 0
                ORDER BY ratio DESC, username COLLATE NOCASE
                LIMIT {limit};
                """;

            var results = new List<(string Name, int Points)>();
            using var reader = query.ExecuteReader();
            while (reader.Read())
            {
                var display = reader.GetString(1);
                var name = string.IsNullOrWhiteSpace(display) ? reader.GetString(0) : display;
                var ratio = reader.GetInt64(2);
                results.Add((name, (int)Math.Clamp(ratio, 0, int.MaxValue)));
            }

            return results;
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

        EnsureColumn("accounts", "monthly_points", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn("accounts", "monthly_period", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn("accounts", "season_points", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn("accounts", "orbs", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn("accounts", "assists", "INTEGER NOT NULL DEFAULT 0");

        using var settings = _connection.CreateCommand();
        settings.CommandText = """
            CREATE TABLE IF NOT EXISTS server_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        settings.ExecuteNonQuery();

        using var bans = _connection.CreateCommand();
        bans.CommandText = """
            CREATE TABLE IF NOT EXISTS bans (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL COLLATE NOCASE,
                reason TEXT NOT NULL,
                banned_by TEXT NOT NULL,
                created_utc TEXT NOT NULL
            );
            """;
        bans.ExecuteNonQuery();

        using var bansIndex = _connection.CreateCommand();
        bansIndex.CommandText = "CREATE INDEX IF NOT EXISTS idx_bans_username ON bans(username);";
        bansIndex.ExecuteNonQuery();
    }

    public bool IsBanned(string username)
    {
        username = NormalizeUsername(username);
        if (string.IsNullOrEmpty(username))
        {
            return false;
        }

        lock (_sync)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM bans WHERE username = $username LIMIT 1;";
            command.Parameters.AddWithValue("$username", username);
            return command.ExecuteScalar() is not null;
        }
    }

    public bool TryAddBan(string username, string bannedBy, string reason)
    {
        username = NormalizeUsername(username);
        if (string.IsNullOrEmpty(username))
        {
            return false;
        }

        bannedBy = string.IsNullOrWhiteSpace(bannedBy) ? "admin" : bannedBy.Trim();
        reason = string.IsNullOrWhiteSpace(reason) ? "banned" : reason.Trim();

        lock (_sync)
        {
            if (IsBannedUnlocked(username))
            {
                return true;
            }

            using var insert = _connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO bans (username, reason, banned_by, created_utc)
                VALUES ($username, $reason, $bannedBy, $createdUtc);
                """;
            insert.Parameters.AddWithValue("$username", username);
            insert.Parameters.AddWithValue("$reason", reason);
            insert.Parameters.AddWithValue("$bannedBy", bannedBy);
            insert.Parameters.AddWithValue("$createdUtc", DateTime.UtcNow.ToString("O"));
            insert.ExecuteNonQuery();
            return true;
        }
    }

    public IReadOnlyList<BanRecord> ListBans()
    {
        lock (_sync)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT id, username, reason, banned_by, created_utc
                FROM bans
                ORDER BY datetime(created_utc) DESC, id DESC;
                """;

            var results = new List<BanRecord>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new BanRecord
                {
                    Id = reader.GetInt64(0),
                    Username = reader.GetString(1),
                    Reason = reader.GetString(2),
                    BannedBy = reader.GetString(3),
                    CreatedUtc = reader.GetString(4),
                });
            }

            return results;
        }
    }

    public bool TryRemoveBan(string username)
    {
        username = NormalizeUsername(username);
        if (string.IsNullOrEmpty(username))
        {
            return false;
        }

        lock (_sync)
        {
            using var delete = _connection.CreateCommand();
            delete.CommandText = "DELETE FROM bans WHERE username = $username;";
            delete.Parameters.AddWithValue("$username", username);
            return delete.ExecuteNonQuery() > 0;
        }
    }

    private void EnsureColumn(string table, string column, string definition)
    {
        using var info = _connection.CreateCommand();
        info.CommandText = $"PRAGMA table_info({table});";
        var exists = false;
        using (var reader = info.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
        {
            return;
        }

        using var alter = _connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    private string? ReadSetting(string key)
    {
        using var query = _connection.CreateCommand();
        query.CommandText = "SELECT value FROM server_settings WHERE key = $key LIMIT 1;";
        query.Parameters.AddWithValue("$key", key);
        return query.ExecuteScalar() as string;
    }

    private void WriteSetting(string key, string value)
    {
        using var upsert = _connection.CreateCommand();
        upsert.CommandText = """
            INSERT INTO server_settings (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        upsert.Parameters.AddWithValue("$key", key);
        upsert.Parameters.AddWithValue("$value", value);
        upsert.ExecuteNonQuery();
    }

    private bool IsBannedUnlocked(string username)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM bans WHERE username = $username LIMIT 1;";
        command.Parameters.AddWithValue("$username", username);
        return command.ExecuteScalar() is not null;
    }

    private static string NormalizeUsername(string username) => username.Trim();

    private static bool IsValidUsername(string username) =>
        username.Length is >= 1 and <= 15
        && username.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
}
