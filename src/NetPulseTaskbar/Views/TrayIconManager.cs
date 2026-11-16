using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NetPulseTaskbar.Models;
using NetPulseTaskbar.Services;

namespace NetPulseTaskbar.Views
{
    public class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly AppConfig _config;
        private readonly NetworkTelemetryService _telemetry;
        private readonly UsageLedgerService _usageLedger;

        public event Action? OpenDashboardRequested;
        public event Action? OpenSettingsRequested;
        public event Action? OpenAboutRequested;
        public event Action? ExitRequested;

        public TrayIconManager(AppConfig config, NetworkTelemetryService telemetry, UsageLedgerService usageLedger)
        {
            _config = config;
            _telemetry = telemetry;
            _usageLedger = usageLedger;

            _notifyIcon = new NotifyIcon
            {
                Text = "NetPulse Network Monitor",
                Visible = true,
                Icon = CreateTrayIcon()
            };

            _notifyIcon.MouseClick += OnTrayIconMouseClick;
            _notifyIcon.DoubleClick += (s, e) => OpenDashboardRequested?.Invoke();

            BuildContextMenu();

            _telemetry.TelemetryUpdated += OnTelemetryUpdated;
            _telemetry.ConnectionStateChanged += OnConnectionStateChanged;
            _usageLedger.DataCapExceeded += OnDataCapExceeded;
        }

        private Icon CreateTrayIcon()
        {
            // Dynamically generate a modern high-DPI 32x32 icon with pulse waveform
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Background pill
            using (var bgBrush = new SolidBrush(Color.FromArgb(230, 15, 23, 42)))
            {
                g.FillEllipse(bgBrush, 1, 1, 30, 30);
            }

            using (var borderPen = new Pen(Color.FromArgb(120, 56, 189, 248), 1.8f))
            {
                g.DrawEllipse(borderPen, 1, 1, 30, 30);
            }

            // Pulse / Lightning zigzag in center
            using (var path = new GraphicsPath())
            {
                path.AddLines(new[]
                {
                    new PointF(7, 16),
                    new PointF(12, 16),
                    new PointF(15, 7),
                    new PointF(18, 25),
                    new PointF(21, 16),
                    new PointF(25, 16)
                });

                using var pen = new Pen(Color.FromArgb(255, 0, 230, 118), 2.2f)
                {
                    LineJoin = LineJoin.Round,
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawPath(pen, path);
            }

            return Icon.FromHandle(bmp.GetHicon());
        }

        private void BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            var miDash = new ToolStripMenuItem("Open Dashboard") { Font = new Font(menu.Font, System.Drawing.FontStyle.Bold) };
            miDash.Click += (s, e) => OpenDashboardRequested?.Invoke();
            menu.Items.Add(miDash);

            var miSettings = new ToolStripMenuItem("Settings...");
            miSettings.Click += (s, e) => OpenSettingsRequested?.Invoke();
            menu.Items.Add(miSettings);

            menu.Items.Add(new ToolStripSeparator());

            // Display Mode submenu
            var miDisplay = new ToolStripMenuItem("Display Mode");
            AddDisplayModeItem(miDisplay, "Full (Default)", "Full");
            AddDisplayModeItem(miDisplay, "Compact", "Compact");
            AddDisplayModeItem(miDisplay, "Minimal Only-Speed", "MinimalSpeed");
            AddDisplayModeItem(miDisplay, "Desktop Widget", "DesktopWidget");
            AddDisplayModeItem(miDisplay, "Tray Only (Hide Taskbar)", "TrayOnly");
            menu.Items.Add(miDisplay);

            // Unit presets submenu
            var miUnits = new ToolStripMenuItem("Speed Units");
            AddUnitModeItem(miUnits, "Auto (Bytes/s)", "AutoBytes");
            AddUnitModeItem(miUnits, "Auto (Bits/s)", "AutoBits");
            AddUnitModeItem(miUnits, "mbps (Megabits)", "mbps");
            AddUnitModeItem(miUnits, "Mbps (Megabits)", "Mbps");
            AddUnitModeItem(miUnits, "MBps (Megabytes)", "MBps");
            AddUnitModeItem(miUnits, "MB/s", "MB/s");
            AddUnitModeItem(miUnits, "KB/s", "KB/s");
            AddUnitModeItem(miUnits, "kbps", "kbps");
            menu.Items.Add(miUnits);

            menu.Items.Add(new ToolStripSeparator());

            var miLock = new ToolStripMenuItem("Lock Position") { Checked = _config.LockPosition, CheckOnClick = true };
            miLock.Click += (s, e) => _config.LockPosition = miLock.Checked;
            menu.Items.Add(miLock);

            var miClickThrough = new ToolStripMenuItem("Click-Through Mode") { Checked = _config.ClickThrough, CheckOnClick = true };
            miClickThrough.Click += (s, e) => _config.ClickThrough = miClickThrough.Checked;
            menu.Items.Add(miClickThrough);

            var miPause = new ToolStripMenuItem("Pause Monitoring") { Checked = _config.PauseMonitoring, CheckOnClick = true };
            miPause.Click += (s, e) => _config.PauseMonitoring = miPause.Checked;
            menu.Items.Add(miPause);

            var miStartup = new ToolStripMenuItem("Start with Windows") { Checked = AutoStartManager.IsAutoStartEnabled(), CheckOnClick = true };
            miStartup.Click += (s, e) =>
            {
                _config.StartWithWindows = miStartup.Checked;
                AutoStartManager.SetAutoStart(miStartup.Checked);
            };
            menu.Items.Add(miStartup);

            var miReset = new ToolStripMenuItem("Reset Session Usage");
            miReset.Click += (s, e) => _usageLedger.ResetSession();
            menu.Items.Add(miReset);

            menu.Items.Add(new ToolStripSeparator());

            var miAbout = new ToolStripMenuItem("About NetPulse...");
            miAbout.Click += (s, e) => OpenAboutRequested?.Invoke();
            menu.Items.Add(miAbout);

            var miExit = new ToolStripMenuItem("Exit NetPulse");
            miExit.Click += (s, e) => ExitRequested?.Invoke();
            menu.Items.Add(miExit);

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void AddDisplayModeItem(ToolStripMenuItem parent, string label, string modeKey)
        {
            var item = new ToolStripMenuItem(label)
            {
                Checked = _config.DisplayMode == modeKey
            };
            item.Click += (s, e) =>
            {
                _config.DisplayMode = modeKey;
                foreach (ToolStripMenuItem sibling in parent.DropDownItems)
                {
                    sibling.Checked = (sibling == item);
                }
            };
            parent.DropDownItems.Add(item);
        }

        private void AddUnitModeItem(ToolStripMenuItem parent, string label, string unitKey)
        {
            var item = new ToolStripMenuItem(label)
            {
                Checked = _config.UnitMode == unitKey
            };
            item.Click += (s, e) =>
            {
                _config.UnitMode = unitKey;
                foreach (ToolStripMenuItem sibling in parent.DropDownItems)
                {
                    sibling.Checked = (sibling == item);
                }
            };
            parent.DropDownItems.Add(item);
        }

        private void OnTrayIconMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenDashboardRequested?.Invoke();
            }
        }

        private void OnTelemetryUpdated(NetworkSpeedSample sample)
        {
            var dl = SpeedFormatter.FormatSpeed(sample.DownloadBytesPerSec, _config, false);
            var ul = SpeedFormatter.FormatSpeed(sample.UploadBytesPerSec, _config, true);

            string pingText = _config.EnablePing && sample.PingMs > 0 ? $" ({sample.PingMs:0}ms)" : "";
            string tooltip = $"NetPulse: {dl.FullDisplayString} | {ul.FullDisplayString}{pingText}";

            // NotifyIcon Text is capped at 63 characters by Windows Shell API
            if (tooltip.Length > 63)
            {
                tooltip = tooltip.Substring(0, 63);
            }

            try
            {
                _notifyIcon.Text = tooltip;
            }
            catch { }
        }

        private void OnConnectionStateChanged(bool isConnected, string statusText)
        {
            if (!_config.NotifyOnDisconnect) return;

            if (isConnected)
            {
                if (_config.EnableSoundAlerts) SoundAlertService.PlayReconnectAlert();
                _notifyIcon.ShowBalloonTip(3000, "Network Reconnected", statusText, ToolTipIcon.Info);
            }
            else
            {
                if (_config.EnableSoundAlerts) SoundAlertService.PlayDisconnectAlert();
                _notifyIcon.ShowBalloonTip(4000, "Network Disconnected", statusText, ToolTipIcon.Warning);
            }
        }

        private void OnDataCapExceeded(double currentGb, double capGb)
        {
            if (_config.EnableSoundAlerts) SoundAlertService.PlayDataCapAlert();
            _notifyIcon.ShowBalloonTip(5000, "Daily Data Cap Reached",
                $"You have used {currentGb:0.##} GB today, exceeding your limit of {capGb:0.##} GB.", ToolTipIcon.Warning);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
