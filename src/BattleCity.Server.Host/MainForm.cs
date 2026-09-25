using System.ComponentModel;
using BattleCity.Server;
using BattleCity.Server.Accounts;
using BattleCity.Shared.Constants;

namespace BattleCity.Server.Host;

internal sealed class MainForm : Form
{
    private readonly string _databasePath =
        Path.Combine(AppContext.BaseDirectory, "accounts.db");

    private readonly NumericUpDown _portInput = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _copyInviteButton = new();
    private readonly Label _statusLabel = new();
    private readonly TextBox _shareBox = new();
    private readonly Label _hostingTips = new();
    private readonly ListBox _lanAddresses = new();
    private readonly ListView _players = new();
    private readonly CheckedListBox _accounts = new();
    private readonly ListView _bans = new();
    private readonly Button _unbanButton = new();
    private readonly Button _refreshBansButton = new();
    private readonly Label _adminHint = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();

    private GameServer? _server;
    private Thread? _tickThread;
    private volatile bool _tickRunning;
    private bool _suppressAccountToggle;
    private int _banRefreshTicks;

    public MainForm()
    {
        Text = "Battle City Server";
        Width = 960;
        Height = 720;
        MinimumSize = new Size(820, 600);
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        WireEvents();
        RefreshLanAddresses();
        ReloadAccounts();
        ReloadBans();
        UpdateUiState(running: false);
        RefreshShareBox();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        Controls.Add(root);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
        };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        root.Controls.Add(left, 0, 0);

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 8),
        };

        controls.Controls.Add(new Label
        {
            Text = "Port",
            AutoSize = true,
            Margin = new Padding(0, 8, 6, 0),
        });

        _portInput.Minimum = 1;
        _portInput.Maximum = 65535;
        _portInput.Value = NetworkConstants.TcpPort;
        _portInput.Width = 80;
        controls.Controls.Add(_portInput);

        _startButton.Text = "Start";
        _startButton.Width = 90;
        controls.Controls.Add(_startButton);

        _stopButton.Text = "Stop";
        _stopButton.Width = 90;
        controls.Controls.Add(_stopButton);

        _copyInviteButton.Text = "Copy Invite";
        _copyInviteButton.Width = 110;
        controls.Controls.Add(_copyInviteButton);
        left.Controls.Add(controls, 0, 0);

        _statusLabel.AutoSize = true;
        _statusLabel.Margin = new Padding(0, 0, 0, 6);
        left.Controls.Add(_statusLabel, 0, 1);

        var sharePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        sharePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sharePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sharePanel.Controls.Add(new Label
        {
            Text = "Share this (paste into Client login → Server)",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        }, 0, 0);
        _shareBox.Dock = DockStyle.Fill;
        _shareBox.ReadOnly = true;
        _shareBox.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
        _shareBox.TextAlign = HorizontalAlignment.Center;
        sharePanel.Controls.Add(_shareBox, 0, 1);
        left.Controls.Add(sharePanel, 0, 2);

        _hostingTips.Text =
            "Local: 127.0.0.1 on this PC." + Environment.NewLine +
            "LAN: share a 192.168.x.x address. Allow TCP 5643 in Windows Firewall." + Environment.NewLine +
            "Internet (easiest): Tailscale — share your Tailscale IP:port instead of LAN." + Environment.NewLine +
            "Details: HOSTING.md in the release zip.";
        _hostingTips.AutoSize = true;
        _hostingTips.ForeColor = Color.FromArgb(45, 55, 75);
        _hostingTips.Margin = new Padding(0, 4, 0, 8);
        left.Controls.Add(_hostingTips, 0, 3);

        var lanGroup = new GroupBox
        {
            Text = "Detected LAN addresses",
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
        };
        _lanAddresses.Dock = DockStyle.Fill;
        lanGroup.Controls.Add(_lanAddresses);
        left.Controls.Add(lanGroup, 0, 4);

        var playersPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        playersPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        playersPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        playersPanel.Controls.Add(new Label
        {
            Text = "Connected players",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 4),
        }, 0, 0);
        _players.Dock = DockStyle.Fill;
        _players.View = View.Details;
        _players.FullRowSelect = true;
        _players.GridLines = true;
        _players.Columns.Add("Id", 40);
        _players.Columns.Add("Name", 140);
        _players.Columns.Add("State", 90);
        _players.Columns.Add("City", 50);
        _players.Columns.Add("Flags", 120);
        playersPanel.Controls.Add(_players, 0, 1);
        left.Controls.Add(playersPanel, 0, 5);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(8, 0, 0, 0),
        };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(right, 1, 0);

        right.Controls.Add(new Label
        {
            Text = "Accounts — checked = admin",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        }, 0, 0);

        _accounts.Dock = DockStyle.Fill;
        _accounts.CheckOnClick = true;
        right.Controls.Add(_accounts, 0, 1);

        var bansHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
        };
        bansHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bansHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bansHeader.Controls.Add(new Label
        {
            Text = "Banned accounts",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        }, 0, 0);

        var banButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 4),
        };
        _unbanButton.Text = "Unban selected";
        _unbanButton.Width = 120;
        _unbanButton.Enabled = false;
        banButtons.Controls.Add(_unbanButton);

        _refreshBansButton.Text = "Refresh";
        _refreshBansButton.Width = 80;
        banButtons.Controls.Add(_refreshBansButton);
        bansHeader.Controls.Add(banButtons, 0, 1);
        right.Controls.Add(bansHeader, 0, 2);

        _bans.Dock = DockStyle.Fill;
        _bans.View = View.Details;
        _bans.FullRowSelect = true;
        _bans.GridLines = true;
        _bans.MultiSelect = false;
        _bans.HideSelection = false;
        _bans.Columns.Add("Username", 110);
        _bans.Columns.Add("By", 90);
        _bans.Columns.Add("Reason", 70);
        _bans.Columns.Add("When (UTC)", 140);
        right.Controls.Add(_bans, 0, 3);

        _adminHint.Text =
            "Toggle admin takes effect immediately for online players.\n" +
            "Username \"admin\" is always treated as admin.\n" +
            "Bans block login (registered name or guest display name).";
        _adminHint.AutoSize = true;
        _adminHint.Margin = new Padding(0, 8, 0, 0);
        right.Controls.Add(_adminHint, 0, 4);
    }

    private void WireEvents()
    {
        _startButton.Click += (_, _) => StartServer();
        _stopButton.Click += (_, _) => StopServer();
        _copyInviteButton.Click += (_, _) => CopyInvite();
        _lanAddresses.SelectedIndexChanged += (_, _) => RefreshShareBox();
        _accounts.ItemCheck += OnAccountItemCheck;
        _unbanButton.Click += (_, _) => UnbanSelected();
        _refreshBansButton.Click += (_, _) => ReloadBans();
        _bans.SelectedIndexChanged += (_, _) => UpdateUnbanButtonState();
        FormClosing += (_, _) => StopServer();

        _refreshTimer.Interval = 500;
        _refreshTimer.Tick += (_, _) =>
        {
            if (_server is not null && !_server.IsRunning)
            {
                var status = "Server shut down (admin /shutdown).";
                StopServer();
                _statusLabel.Text = status;
                return;
            }

            RefreshLanAddresses();
            RefreshPlayers();
            RefreshShareBox();
            _banRefreshTicks++;
            if (_banRefreshTicks >= 10)
            {
                _banRefreshTicks = 0;
                ReloadBans();
            }
        };
    }

    private void StartServer()
    {
        if (_server is not null)
        {
            return;
        }

        try
        {
            var port = (int)_portInput.Value;
            _server = new GameServer(_databasePath);
            _server.Start("0.0.0.0", port);

            _tickRunning = true;
            _tickThread = new Thread(TickLoop)
            {
                IsBackground = true,
                Name = "BattleCity.Server.Tick",
            };
            _tickThread.Start();

            _refreshTimer.Start();
            UpdateUiState(running: true);
            ReloadAccounts();
            ReloadBans();
            RefreshPlayers();
            RefreshShareBox();
        }
        catch (Exception ex)
        {
            _server?.Dispose();
            _server = null;
            MessageBox.Show(this, ex.Message, "Could not start server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateUiState(running: false);
        }
    }

    private void StopServer()
    {
        _refreshTimer.Stop();
        _tickRunning = false;

        var thread = _tickThread;
        _tickThread = null;
        if (thread is { IsAlive: true })
        {
            thread.Join(TimeSpan.FromSeconds(2));
        }

        _server?.Dispose();
        _server = null;
        _players.Items.Clear();
        UpdateUiState(running: false);
        RefreshShareBox();
    }

    private void TickLoop()
    {
        const float tickSeconds = 1f / 60f;
        while (_tickRunning)
        {
            try
            {
                _server?.Update(tickSeconds);
            }
            catch
            {
                // Keep the host UI alive if a tick fails.
            }

            Thread.Sleep(16);
        }
    }

    private void CopyInvite()
    {
        var invite = BuildInviteMessage();
        Clipboard.SetText(invite);
        _statusLabel.Text = "Invite copied to clipboard.";
        RefreshShareBox();
    }

    private string GetPrimaryEndpoint()
    {
        var port = _server?.Port ?? (int)_portInput.Value;
        if (_lanAddresses.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
        {
            return selected;
        }

        var addresses = LanAddressHelper.GetLanIPv4Addresses();
        return addresses.Count > 0 ? $"{addresses[0]}:{port}" : $"127.0.0.1:{port}";
    }

    private string BuildInviteMessage()
    {
        var endpoint = GetPrimaryEndpoint();
        var port = _server?.Port ?? (int)_portInput.Value;
        var others = LanAddressHelper.GetLanIPv4Addresses()
            .Select(ip => $"{ip}:{port}")
            .Where(entry => !string.Equals(entry, endpoint, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var lines = new List<string>
        {
            "Battle City — join my server",
            $"Connect to: {endpoint}",
            string.Empty,
            "In the game client: Play Online → paste that address into Server → login.",
            "Guest: leave username blank (or password = guest).",
            string.Empty,
            "Same PC: 127.0.0.1:" + port,
            "LAN: allow TCP " + port + " inbound in Windows Firewall.",
            "Over the internet: use Tailscale and share your Tailscale IP instead of 192.168.x.x.",
        };

        if (others.Length > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Other LAN addresses:");
            lines.AddRange(others);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void RefreshShareBox() => _shareBox.Text = GetPrimaryEndpoint();

    private void RefreshLanAddresses()
    {
        var port = _server?.Port ?? (int)_portInput.Value;
        var desired = LanAddressHelper.GetLanIPv4Addresses()
            .Select(ip => $"{ip}:{port}")
            .ToArray();

        if (_lanAddresses.Items.Count == desired.Length
            && desired.SequenceEqual(_lanAddresses.Items.Cast<object>().Select(x => x?.ToString() ?? string.Empty)))
        {
            return;
        }

        var previous = _lanAddresses.SelectedItem?.ToString();
        _lanAddresses.BeginUpdate();
        _lanAddresses.Items.Clear();
        foreach (var entry in desired)
        {
            _lanAddresses.Items.Add(entry);
        }

        _lanAddresses.EndUpdate();
        if (previous is not null)
        {
            var index = _lanAddresses.Items.IndexOf(previous);
            _lanAddresses.SelectedIndex = index >= 0 ? index : (desired.Length > 0 ? 0 : -1);
        }
        else if (_lanAddresses.Items.Count > 0)
        {
            _lanAddresses.SelectedIndex = 0;
        }
    }

    private void RefreshPlayers()
    {
        if (_server is null)
        {
            _players.Items.Clear();
            return;
        }

        IReadOnlyList<ConnectedPlayerInfo> players;
        try
        {
            players = _server.GetConnectedPlayers();
        }
        catch
        {
            return;
        }

        _players.BeginUpdate();
        _players.Items.Clear();
        foreach (var player in players)
        {
            var flags = new List<string>();
            if (player.IsAdmin)
            {
                flags.Add("admin");
            }

            if (player.IsMayor)
            {
                flags.Add("mayor");
            }

            if (player.IsGuest)
            {
                flags.Add("guest");
            }

            var item = new ListViewItem(player.PlayerId.ToString());
            item.SubItems.Add(player.DisplayName);
            item.SubItems.Add(player.State);
            item.SubItems.Add(player.CityId.ToString());
            item.SubItems.Add(string.Join(", ", flags));
            _players.Items.Add(item);
        }

        _players.EndUpdate();
    }

    private void ReloadAccounts()
    {
        IReadOnlyList<AccountRecord> accounts;
        try
        {
            if (_server is not null)
            {
                accounts = _server.Accounts.ListAccounts();
            }
            else
            {
                using var db = new AccountDatabase(_databasePath);
                accounts = db.ListAccounts();
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Account DB: {ex.Message}";
            return;
        }

        _suppressAccountToggle = true;
        _accounts.BeginUpdate();
        _accounts.Items.Clear();
        foreach (var account in accounts)
        {
            var label = $"{account.Username}  ({account.Points} pts / {account.Deaths} deaths)";
            var item = new AccountListItem(account.Username, label);
            _accounts.Items.Add(item, account.IsAdmin);
        }

        _accounts.EndUpdate();
        _suppressAccountToggle = false;
    }

    private void ReloadBans()
    {
        IReadOnlyList<BanRecord> bans;
        try
        {
            if (_server is not null)
            {
                bans = _server.ListBans();
            }
            else
            {
                using var db = new AccountDatabase(_databasePath);
                bans = db.ListBans();
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Ban DB: {ex.Message}";
            return;
        }

        var selected = _bans.SelectedItems.Count > 0
            ? _bans.SelectedItems[0].Text
            : null;

        _bans.BeginUpdate();
        _bans.Items.Clear();
        foreach (var ban in bans)
        {
            var when = FormatBanTimestamp(ban.CreatedUtc);
            var item = new ListViewItem(ban.Username)
            {
                Tag = ban.Username,
            };
            item.SubItems.Add(ban.BannedBy);
            item.SubItems.Add(ban.Reason);
            item.SubItems.Add(when);
            _bans.Items.Add(item);
        }

        _bans.EndUpdate();

        if (selected is not null)
        {
            foreach (ListViewItem item in _bans.Items)
            {
                if (string.Equals(item.Text, selected, StringComparison.OrdinalIgnoreCase))
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        UpdateUnbanButtonState();
    }

    private static string FormatBanTimestamp(string createdUtc)
    {
        if (DateTime.TryParse(
                createdUtc,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return parsed.ToUniversalTime().ToString("yyyy-MM-dd HH:mm");
        }

        return createdUtc;
    }

    private void UpdateUnbanButtonState() =>
        _unbanButton.Enabled = _bans.SelectedItems.Count > 0;

    private void UnbanSelected()
    {
        if (_bans.SelectedItems.Count == 0)
        {
            return;
        }

        var username = _bans.SelectedItems[0].Tag as string ?? _bans.SelectedItems[0].Text;
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Remove ban for '{username}'?",
            "Unban",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            bool removed;
            if (_server is not null)
            {
                removed = _server.TryUnbanAccount(username);
            }
            else
            {
                using var db = new AccountDatabase(_databasePath);
                removed = db.TryRemoveBan(username);
            }

            if (!removed)
            {
                MessageBox.Show(
                    this,
                    $"No ban found for '{username}'.",
                    "Unban",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                _statusLabel.Text = $"Unbanned: {username}";
            }

            ReloadBans();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Unban", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ReloadBans();
        }
    }

    private void OnAccountItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_suppressAccountToggle)
        {
            return;
        }

        if (_accounts.Items[e.Index] is not AccountListItem item)
        {
            return;
        }

        var makeAdmin = e.NewValue == CheckState.Checked;
        BeginInvoke(() => ApplyAdminToggle(item.Username, makeAdmin));
    }

    private void ApplyAdminToggle(string username, bool isAdmin)
    {
        try
        {
            if (_server is not null)
            {
                if (!_server.TrySetAccountAdmin(username, isAdmin))
                {
                    MessageBox.Show(this, $"Could not update admin for '{username}'.", "Admin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ReloadAccounts();
                    return;
                }
            }
            else
            {
                using var db = new AccountDatabase(_databasePath);
                if (!db.TrySetAdmin(username, isAdmin))
                {
                    MessageBox.Show(this, $"Could not update admin for '{username}'.", "Admin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ReloadAccounts();
                    return;
                }
            }

            _statusLabel.Text = isAdmin
                ? $"Granted admin: {username}"
                : $"Removed admin: {username}";
            RefreshPlayers();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Admin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ReloadAccounts();
        }
    }

    private void UpdateUiState(bool running)
    {
        _portInput.Enabled = !running;
        _startButton.Enabled = !running;
        _stopButton.Enabled = running;
        _statusLabel.Text = running
            ? $"Listening on 0.0.0.0:{_server?.Port ?? (int)_portInput.Value}  |  DB: {_databasePath}"
            : $"Stopped  |  DB: {_databasePath}";
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        StopServer();
        base.OnClosing(e);
    }

    private sealed class AccountListItem
    {
        public AccountListItem(string username, string display)
        {
            Username = username;
            Display = display;
        }

        public string Username { get; }

        public string Display { get; }

        public override string ToString() => Display;
    }
}
