using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using DBTracker.Tray.Core;

namespace DBTracker.Tray
{
    internal sealed class TrayContext : ApplicationContext
    {
        private static readonly TimeSpan HealthInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan SummaryInterval = TimeSpan.FromMinutes(2);

        private readonly TraySettings _settings;
        private readonly DbTrackerClient _client;
        private readonly TrayIcons _icons = new TrayIcons();
        private readonly NotifyIcon _notifyIcon;
        private readonly CloseListener _closeListener;
        private readonly Timer _healthTimer = new Timer();
        private readonly Timer _summaryTimer = new Timer();
        private readonly LightTransitionDetector _lights = new LightTransitionDetector();
        private readonly AtRiskTracker _atRisk = new AtRiskTracker();

        private readonly ToolStripMenuItem _statusItem = new ToolStripMenuItem { Enabled = false };
        private readonly ToolStripMenuItem _openItem = new ToolStripMenuItem("&Open DBTracker");
        private readonly ToolStripMenuItem _backupsItem = new ToolStripMenuItem { Enabled = false };
        private readonly ToolStripMenuItem _dashboardItem = new ToolStripMenuItem("Open the &dashboard");
        private readonly ToolStripMenuItem _connectItem = new ToolStripMenuItem("&Connect to DBTracker...");
        private readonly ToolStripMenuItem _disconnectItem = new ToolStripMenuItem("Dis&connect");
        private readonly ToolStripMenuItem _copyKeyItem;
        private readonly ToolStripMenuItem _startupItem = new ToolStripMenuItem("Start with &Windows") { CheckOnClick = true };

        private HealthState _health = new HealthState(StatusLight.Unknown, "checking", false);
        private TraySummary _summary;
        private string _token;
        private bool _tokenRejected;
        private bool _healthBusy;
        private bool _summaryBusy;
        private Action _balloonClick;

        public TrayContext(TraySettings settings)
        {
            _settings = settings;
            _client = new DbTrackerClient(settings);
            _token = TokenStore.Load();

            _copyKeyItem = AdminItem("Copy the setup &key", AdminAction.CopySetupKey);
            _notifyIcon = new NotifyIcon
            {
                Icon = _icons.For(StatusLight.Unknown),
                Text = TrayText.Tooltip(_health),
                ContextMenuStrip = BuildMenu(),
                Visible = true,
            };
            _notifyIcon.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) OpenDbTracker(); };
            _notifyIcon.BalloonTipClicked += (s, e) => _balloonClick?.Invoke();
            _notifyIcon.ContextMenuStrip.Opening += (s, e) => RefreshMenu();
            _closeListener = new CloseListener(ExitThread);

            _healthTimer.Interval = (int)HealthInterval.TotalMilliseconds;
            _healthTimer.Tick += async (s, e) => await PollHealthAsync();
            _summaryTimer.Interval = (int)SummaryInterval.TotalMilliseconds;
            _summaryTimer.Tick += async (s, e) => await PollSummaryAsync();
            _healthTimer.Start();
            _summaryTimer.Start();

            RefreshMenu();
            _ = PollHealthAsync();
            _ = PollSummaryAsync();
        }

        private ContextMenuStrip BuildMenu()
        {
            _openItem.Click += (s, e) => OpenDbTracker();
            _dashboardItem.Click += (s, e) => OpenUrl(_settings.DashboardUrl(_summary));
            _connectItem.Click += (s, e) => Connect();
            _disconnectItem.Click += (s, e) => Disconnect();
            _startupItem.Click += (s, e) => UserPreferences.StartWithWindows = _startupItem.Checked;

            var admin = new ToolStripMenuItem("&Admin");
            admin.DropDownItems.AddRange(new ToolStripItem[]
            {
                AdminItem("&Start the app pool", AdminAction.StartAppPool),
                AdminItem("S&top the app pool", AdminAction.StopAppPool),
                AdminItem("&Restart the app pool", AdminAction.RestartAppPool),
                new ToolStripSeparator(),
                AdminItem("Start the s&ite", AdminAction.StartSite),
                AdminItem("Stop the sit&e", AdminAction.StopSite),
                AdminItem("Restart the site", AdminAction.RestartSite),
                new ToolStripSeparator(),
                _copyKeyItem,
            });

            var folders = new ToolStripMenuItem("&Folders");
            folders.DropDownItems.AddRange(new ToolStripItem[]
            {
                Item("&Install folder", () => OpenFolder(_settings.InstallFolder)),
                Item("&App_Data (settings and keys)", () => OpenFolder(_settings.AppDataFolder(RegistrySettings.DataDirOverride()))),
                Item("&IIS logs for this site", () => OpenFolder(IisLogs())),
                Item("&Deployment guide", OpenDeploymentGuide),
            });

            var menu = new ContextMenuStrip();
            menu.Items.AddRange(new ToolStripItem[]
            {
                _statusItem,
                _openItem,
                new ToolStripSeparator(),
                _backupsItem,
                _dashboardItem,
                _connectItem,
                _disconnectItem,
                new ToolStripSeparator(),
                admin,
                folders,
                Item("&Recent errors...", () => new RecentErrorsForm(_settings).Show()),
                new ToolStripSeparator(),
                _startupItem,
                Item("E&xit", ExitThread),
            });
            return menu;
        }

        private ToolStripMenuItem AdminItem(string text, AdminAction action) => Item(text + " (needs admin)", async () =>
        {
            if (action == AdminAction.CopySetupKey && TryCopySetupKeyWithoutElevation())
            {
                return;
            }
            var result = await Elevation.RunAsync(action);
            ShowBalloon("DBTracker", result, ToolTipIcon.Info, null);
            await PollHealthAsync();
        });

        private static ToolStripMenuItem Item(string text, Action onClick)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += (s, e) => onClick();
            return item;
        }

        private void RefreshMenu()
        {
            _statusItem.Text = "DBTracker: " + _health.Text;
            _openItem.Text = _health.SetupRequired ? "&Open the setup wizard" : "&Open DBTracker";
            _copyKeyItem.Visible = _health.SetupRequired;

            var connected = !string.IsNullOrEmpty(_token) && !_tokenRejected;
            _backupsItem.Text = connected
                ? (_summary != null ? TrayText.Backups(_summary) : "Backups: checking...")
                : _tokenRejected ? "Backups: DBTracker no longer accepts the saved token" : "Backups: not connected";
            _dashboardItem.Visible = connected && _summary != null;
            _connectItem.Visible = !connected;
            _disconnectItem.Visible = !string.IsNullOrEmpty(_token);
            _disconnectItem.Text = _summary?.User != null ? "Dis&connect (" + _summary.User + ")" : "Dis&connect";
            _startupItem.Checked = UserPreferences.StartWithWindows;
        }

        private async System.Threading.Tasks.Task PollHealthAsync()
        {
            if (_healthBusy)
            {
                return;
            }
            _healthBusy = true;
            try
            {
                var probe = await _client.GetHealthAsync();
                _health = HealthClassifier.Classify(probe, _settings.Port);
                _notifyIcon.Icon = _icons.For(_health.Light);
                _notifyIcon.Text = TrayText.Tooltip(_health);

                var transition = _lights.Observe(_health.Light);
                if (transition != null)
                {
                    var icon = transition.To == StatusLight.Red ? ToolTipIcon.Error : transition.To == StatusLight.Amber ? ToolTipIcon.Warning : ToolTipIcon.Info;
                    ShowBalloon(TrayText.TransitionTitle(transition), _health.Text, icon, OpenDbTracker);
                }
                RefreshMenu();
            }
            finally
            {
                _healthBusy = false;
            }
        }

        private async System.Threading.Tasks.Task PollSummaryAsync()
        {
            if (_summaryBusy || string.IsNullOrEmpty(_token) || _tokenRejected)
            {
                return;
            }
            _summaryBusy = true;
            try
            {
                var result = await _client.GetSummaryAsync(_token);
                switch (result.Item1)
                {
                    case SummaryOutcome.Ok:
                        _summary = result.Item2;
                        var message = TrayText.NewlyAtRisk(_atRisk.Update(_summary));
                        if (message != null)
                        {
                            ShowBalloon("Backup risk", message, ToolTipIcon.Warning, () => OpenUrl(_settings.DashboardUrl(_summary)));
                        }
                        break;
                    case SummaryOutcome.Rejected:
                        _tokenRejected = true;
                        _summary = null;
                        ShowBalloon("DBTracker", "DBTracker no longer accepts the saved token. Connect again with a new one.", ToolTipIcon.Warning, Connect);
                        break;
                }
                RefreshMenu();
            }
            finally
            {
                _summaryBusy = false;
            }
        }

        private void Connect()
        {
            using (var form = new ConnectForm(_client, _settings))
            {
                if (form.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                TokenStore.Save(form.Token);
                _token = form.Token;
                _tokenRejected = false;
                _summary = form.Summary;
                _atRisk.Reset();
                _atRisk.Update(_summary);
                RefreshMenu();
                ShowBalloon("Connected to DBTracker", TrayText.Backups(_summary), ToolTipIcon.Info, null);
            }
        }

        private void Disconnect()
        {
            TokenStore.Clear();
            _token = null;
            _tokenRejected = false;
            _summary = null;
            _atRisk.Reset();
            RefreshMenu();
        }

        private void OpenDbTracker() => OpenUrl(_settings.OpenUrl(_health.SetupRequired));

        private bool TryCopySetupKeyWithoutElevation()
        {
            var keyFile = Path.Combine(_settings.AppDataFolder(RegistrySettings.DataDirOverride()), "setup-key.txt");
            try
            {
                if (!File.Exists(keyFile))
                {
                    return false;
                }
                Clipboard.SetText(File.ReadAllText(keyFile).Trim());
                ShowBalloon("DBTracker", "Setup key copied to the clipboard.", ToolTipIcon.Info, null);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private string IisLogs()
        {
            var fallback = Environment.ExpandEnvironmentVariables(IisLogFolder.IisDefault);
            try
            {
                var config = Path.Combine(Environment.SystemDirectory, "inetsrv", "config", "applicationHost.config");
                var folder = IisLogFolder.Find(File.ReadAllText(config), _settings.SiteName);
                return folder == null ? fallback : Environment.ExpandEnvironmentVariables(folder);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                // Only administrators can read applicationHost.config; the parent folder still helps.
                return fallback;
            }
        }

        private void OpenDeploymentGuide()
        {
            if (File.Exists(_settings.DeploymentGuide))
            {
                Process.Start(new ProcessStartInfo("notepad.exe", "\"" + _settings.DeploymentGuide + "\"") { UseShellExecute = true });
            }
            else
            {
                OpenUrl(TraySettings.RepositoryUrl + "/blob/main/DEPLOYMENT.md");
            }
        }

        private void OpenFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                ShowBalloon("DBTracker", "Folder not found: " + path, ToolTipIcon.Warning, null);
                return;
            }
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                ShowBalloon("DBTracker", "Couldn't open a browser: " + ex.Message, ToolTipIcon.Warning, null);
            }
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon, Action onClick)
        {
            _balloonClick = onClick;
            _notifyIcon.ShowBalloonTip(8000, title, text, icon);
        }

        protected override void ExitThreadCore()
        {
            _healthTimer.Stop();
            _summaryTimer.Stop();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _icons.Dispose();
            _client.Dispose();
            _closeListener.DestroyHandle();
            base.ExitThreadCore();
        }

        // A hidden top-level window, so an installer sending WM_CLOSE (util:CloseApplication) or Restart
        // Manager ending the session gets a clean exit that removes the tray icon instead of a forced kill.
        private sealed class CloseListener : NativeWindow
        {
            private const int WmClose = 0x0010;
            private const int WmEndSession = 0x0016;
            private readonly Action _onClose;

            public CloseListener(Action onClose)
            {
                _onClose = onClose;
                CreateHandle(new CreateParams { Caption = "DBTracker Tray" });
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WmClose || (m.Msg == WmEndSession && m.WParam != IntPtr.Zero))
                {
                    _onClose();
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
