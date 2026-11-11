using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DBTracker.Tray.Core;

namespace DBTracker.Tray
{
    // The last 20 Application event log errors and warnings that mention this DBTracker site.
    internal sealed class RecentErrorsForm : Form
    {
        private const int MaxEntries = 20;
        // Reading backwards through a large log is slow; this bounds the scan.
        private const int ScanLimit = 3000;

        private readonly TraySettings _settings;
        private readonly ListView _list;
        private readonly TextBox _details;
        private readonly Label _status;
        private List<EventLogEntry> _entries = new List<EventLogEntry>();

        public RecentErrorsForm(TraySettings settings)
        {
            _settings = settings;
            Text = "DBTracker: recent errors";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(760, 460);
            MinimumSize = new Size(520, 320);

            _status = new Label { Dock = DockStyle.Top, Height = 24, Padding = new Padding(8, 6, 8, 0), Text = "Reading the Application event log..." };
            _list = new ListView
            {
                Dock = DockStyle.Top,
                Height = 220,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                AccessibleName = "Recent errors",
            };
            _list.Columns.Add("Time", 150);
            _list.Columns.Add("Level", 80);
            _list.Columns.Add("Source", 170);
            _list.Columns.Add("Message", 340);
            _list.SelectedIndexChanged += (s, e) => ShowDetails();

            _details = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                AccessibleName = "Selected error details",
            };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var close = new Button { Text = "Close", Width = 85, DialogResult = DialogResult.Cancel };
            var refresh = new Button { Text = "&Refresh", Width = 85 };
            var viewer = new Button { Text = "Open &Event Viewer", Width = 140 };
            refresh.Click += async (s, e) => await LoadAsync();
            viewer.Click += (s, e) => Process.Start(new ProcessStartInfo("eventvwr.msc") { UseShellExecute = true });
            buttons.Controls.AddRange(new Control[] { close, refresh, viewer });
            CancelButton = close;

            Controls.Add(_details);
            Controls.Add(_list);
            Controls.Add(_status);
            Controls.Add(buttons);
            Shown += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            _status.Text = "Reading the Application event log...";
            _list.Items.Clear();
            _details.Clear();
            try
            {
                _entries = await Task.Run(() => Read(_settings));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.Security.SecurityException || ex is UnauthorizedAccessException)
            {
                _status.Text = "Couldn't read the Application event log: " + ex.Message;
                return;
            }

            foreach (var entry in _entries)
            {
                var item = new ListViewItem(entry.TimeGenerated.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(entry.EntryType == EventLogEntryType.Error ? "Error" : "Warning");
                item.SubItems.Add(entry.Source);
                item.SubItems.Add(EventLogFilter.FirstLine(entry.Message));
                _list.Items.Add(item);
            }
            _status.Text = _entries.Count == 0
                ? "No recent errors or warnings mention this DBTracker site."
                : "Showing the " + _entries.Count + " most recent errors and warnings that mention this site.";
            if (_list.Items.Count > 0)
            {
                _list.Items[0].Selected = true;
                _list.Focus();
            }
        }

        private static List<EventLogEntry> Read(TraySettings settings)
        {
            var found = new List<EventLogEntry>();
            using (var log = new EventLog("Application"))
            {
                var entries = log.Entries;
                var stop = Math.Max(0, entries.Count - ScanLimit);
                for (var i = entries.Count - 1; i >= stop && found.Count < MaxEntries; i--)
                {
                    var entry = entries[i];
                    if ((entry.EntryType == EventLogEntryType.Error || entry.EntryType == EventLogEntryType.Warning)
                        && EventLogFilter.IsAboutThisApp(entry.Source, entry.Message, settings))
                    {
                        found.Add(entry);
                    }
                }
            }
            return found;
        }

        private void ShowDetails()
        {
            if (_list.SelectedIndices.Count == 0)
            {
                return;
            }
            var entry = _entries[_list.SelectedIndices[0]];
            _details.Text = entry.TimeGenerated + "  " + entry.Source + Environment.NewLine + Environment.NewLine + entry.Message;
        }
    }
}
