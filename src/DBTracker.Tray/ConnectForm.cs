using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using DBTracker.Tray.Core;

namespace DBTracker.Tray
{
    // Paste a personal access token; it is checked against /api/tray/summary before being saved.
    internal sealed class ConnectForm : Form
    {
        private readonly DbTrackerClient _client;
        private readonly TextBox _token;
        private readonly CheckBox _show;
        private readonly AnnouncingLabel _status;
        private readonly Button _connect;

        public ConnectForm(DbTrackerClient client, TraySettings settings)
        {
            _client = client;
            Text = "Connect to DBTracker";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(460, 210);
            Padding = new Padding(12);

            var intro = new Label
            {
                Text = "The tray app shows your failed and overdue backup counts. It signs in with a personal access token that you create in DBTracker.",
                AutoSize = false,
                Location = new Point(12, 12),
                Size = new Size(436, 36),
            };
            var create = new LinkLabel
            {
                Text = "Create a token in DBTracker (opens " + settings.TokensUrl + ")",
                AutoSize = true,
                Location = new Point(12, 52),
                TabIndex = 0,
            };
            create.LinkClicked += (s, e) => Process.Start(new ProcessStartInfo(settings.TokensUrl) { UseShellExecute = true });

            var label = new Label { Text = "&Access token:", AutoSize = true, Location = new Point(12, 84) };
            _token = new TextBox
            {
                Location = new Point(12, 104),
                Width = 436,
                UseSystemPasswordChar = true,
                AccessibleName = "Access token",
                TabIndex = 1,
            };
            _show = new CheckBox { Text = "&Show token", AutoSize = true, Location = new Point(12, 132), TabIndex = 2 };
            _show.CheckedChanged += (s, e) => _token.UseSystemPasswordChar = !_show.Checked;

            _status = new AnnouncingLabel { AutoSize = false, Location = new Point(12, 156), Size = new Size(436, 18) };

            _connect = new Button { Text = "&Connect", Location = new Point(292, 176), Width = 75, TabIndex = 3 };
            var cancel = new Button { Text = "Cancel", Location = new Point(373, 176), Width = 75, DialogResult = DialogResult.Cancel, TabIndex = 4 };
            _connect.Click += OnConnect;

            AcceptButton = _connect;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { intro, create, label, _token, _show, _status, _connect, cancel });
        }

        public string Token { get; private set; }
        public TraySummary Summary { get; private set; }

        private async void OnConnect(object sender, EventArgs e)
        {
            var token = TokenFormat.Normalize(_token.Text);
            if (!TokenFormat.LooksValid(token))
            {
                ShowStatus("That doesn't look like a DBTracker token. Tokens start with dbt_.", true);
                return;
            }

            _connect.Enabled = false;
            ShowStatus("Checking the token with DBTracker...", false);
            var result = await _client.GetSummaryAsync(token);
            _connect.Enabled = true;

            switch (result.Item1)
            {
                case SummaryOutcome.Ok:
                    Token = token;
                    Summary = result.Item2;
                    DialogResult = DialogResult.OK;
                    Close();
                    break;
                case SummaryOutcome.Rejected:
                    ShowStatus("DBTracker rejected this token. It may be mistyped, expired or revoked.", true);
                    break;
                case SummaryOutcome.Unreadable:
                    ShowStatus("DBTracker answered, but this version doesn't support the tray summary.", true);
                    break;
                default:
                    ShowStatus("Couldn't reach DBTracker. Check that the site is running.", true);
                    break;
            }
        }

        private void ShowStatus(string text, bool error)
        {
            _status.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
            _status.Announce(text);
        }
    }

    // A label that tells screen readers when its text changes, so connection results are announced.
    internal sealed class AnnouncingLabel : Label
    {
        public void Announce(string text)
        {
            Text = text;
            AccessibleName = text;
            AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        }
    }
}
