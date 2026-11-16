using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NetPulseTaskbar.Models;
using NetPulseTaskbar.Native;
using NetPulseTaskbar.Services;

namespace NetPulseTaskbar.Views
{
    public partial class TelemetryFlyoutWindow : Window
    {
        private readonly NetworkTelemetryService _telemetry;
        private readonly AppConfig _config;
        private readonly DispatcherTimer _closeGraceTimer;
        private bool _isMouseInside = false;

        public TelemetryFlyoutWindow(NetworkTelemetryService telemetry, AppConfig config)
        {
            InitializeComponent();

            _telemetry = telemetry;
            _config = config;

            _closeGraceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _closeGraceTimer.Tick += (s, e) =>
            {
                _closeGraceTimer.Stop();
                if (!_isMouseInside)
                {
                    Hide();
                }
            };

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowStyle(hwnd, false);
            _telemetry.TelemetryUpdated += OnTelemetryUpdated;
        }

        public void ShowAboveWidget(Window widgetWindow)
        {
            UpdateContent();

            // Populate recent rolling history
            FlyoutChart.Clear();
            var samples = _telemetry.GetHistory();
            foreach (var s in samples)
            {
                FlyoutChart.AddSample(s.DownloadBytesPerSec, s.UploadBytesPerSec);
            }

            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            // Center horizontally relative to widget
            double targetLeft = widgetWindow.Left + (widgetWindow.ActualWidth - Width) / 2.0;
            // Screen clamp
            if (targetLeft < 10) targetLeft = 10;
            if (targetLeft + Width > screenW - 10) targetLeft = screenW - Width - 10;

            // Position vertically above widget; if taskbar is at top, position below
            double targetTop = widgetWindow.Top - Height;
            if (targetTop < 10)
            {
                targetTop = widgetWindow.Top + widgetWindow.ActualHeight;
            }

            Left = targetLeft;
            Top = targetTop;

            _isMouseInside = false;
            Show();
        }

        private void OnTelemetryUpdated(NetworkSpeedSample sample)
        {
            if (Visibility != Visibility.Visible) return;

            Dispatcher.Invoke(() =>
            {
                UpdateContent();
                FlyoutChart.AddSample(sample.DownloadBytesPerSec, sample.UploadBytesPerSec);
            });
        }

        private void UpdateContent()
        {
            var dl = SpeedFormatter.FormatSpeed(_telemetry.CurrentDownloadBps, _config, false);
            var ul = SpeedFormatter.FormatSpeed(_telemetry.CurrentUploadBps, _config, true);

            TxtDownloadSpeed.Text = dl.FullDisplayString;
            TxtUploadSpeed.Text = ul.FullDisplayString;

            // Ping & Jitter
            if (_config.EnablePing && _telemetry.CurrentPingMs > 0)
            {
                TxtPing.Text = $"{_telemetry.CurrentPingMs:0} ms";
                TxtJitter.Text = $"±{_telemetry.CurrentJitterMs:0} ms";
                PingIndicator.Fill = _telemetry.CurrentPingMs < 60 ? Brushes.LimeGreen :
                                     _telemetry.CurrentPingMs < 150 ? Brushes.Orange : Brushes.Red;
            }
            else
            {
                TxtPing.Text = "N/A";
                TxtJitter.Text = "";
                PingIndicator.Fill = Brushes.Gray;
            }

            // Adapter info
            var nic = _telemetry.CurrentAdapter;
            TxtNicName.Text = string.IsNullOrEmpty(nic.Name) ? "No Active Adapter" : $"{nic.Name} ({nic.Description})";
            TxtLinkSpeed.Text = $"Link: {nic.FormattedSpeed}";
            TxtIpAddress.Text = $"IP: {nic.IPv4Address}";
            TxtGateway.Text = $"GW: {nic.GatewayAddress}";

            // Session & Today totals
            var app = (App)Application.Current;
            if (app?.UsageLedger != null)
            {
                TxtSessionUsage.Text = UsageRecord.FormatBytes(app.UsageLedger.SessionTotalBytes);
                var today = app.UsageLedger.GetTodayUsage();
                TxtTodayUsage.Text = today.FormattedTotal;
            }
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseInside = true;
            _closeGraceTimer.Stop();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseInside = false;
            _closeGraceTimer.Start();
        }
    }
}
