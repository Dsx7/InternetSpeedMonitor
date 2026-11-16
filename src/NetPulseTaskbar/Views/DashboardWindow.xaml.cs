using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using NetPulseTaskbar.Models;
using NetPulseTaskbar.Services;

namespace NetPulseTaskbar.Views
{
    public partial class DashboardWindow : Window
    {
        private readonly NetworkTelemetryService _telemetry;
        private readonly AppConfig _config;
        private readonly UsageLedgerService _usageLedger;

        private double _peakDownloadBps = 0;
        private double _peakUploadBps = 0;

        public event Action? RequestOpenSettings;
        public event Action? RequestOpenAbout;

        public DashboardWindow(NetworkTelemetryService telemetry, AppConfig config, UsageLedgerService usageLedger)
        {
            InitializeComponent();

            _telemetry = telemetry;
            _config = config;
            _usageLedger = usageLedger;

            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _telemetry.TelemetryUpdated += OnTelemetryUpdated;
            _telemetry.ConnectionStateChanged += OnConnectionStateChanged;

            // Load initial history
            DashboardChart.Clear();
            var history = _telemetry.GetHistory();
            foreach (var s in history)
            {
                DashboardChart.AddSample(s.DownloadBytesPerSec, s.UploadBytesPerSec);
            }

            UpdatePauseResumeButtonState();
            UpdateTelemetryUI();
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            _telemetry.TelemetryUpdated -= OnTelemetryUpdated;
            _telemetry.ConnectionStateChanged -= OnConnectionStateChanged;
        }

        private void OnTelemetryUpdated(NetworkSpeedSample sample)
        {
            if (sample.DownloadBytesPerSec > _peakDownloadBps) _peakDownloadBps = sample.DownloadBytesPerSec;
            if (sample.UploadBytesPerSec > _peakUploadBps) _peakUploadBps = sample.UploadBytesPerSec;

            Dispatcher.Invoke(() =>
            {
                DashboardChart.AddSample(sample.DownloadBytesPerSec, sample.UploadBytesPerSec);
                UpdateTelemetryUI();
            });
        }

        private void OnConnectionStateChanged(bool isConnected, string statusText)
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = isConnected ? "Connected" : "Disconnected";
                StatusDot.Fill = isConnected ? Brushes.LimeGreen : Brushes.Red;
            });
        }

        private void UpdateTelemetryUI()
        {
            var dl = SpeedFormatter.FormatSpeed(_telemetry.CurrentDownloadBps, _config, false);
            var ul = SpeedFormatter.FormatSpeed(_telemetry.CurrentUploadBps, _config, true);
            var peakDl = SpeedFormatter.FormatSpeed(_peakDownloadBps, _config, false);
            var peakUl = SpeedFormatter.FormatSpeed(_peakUploadBps, _config, true);

            CardDownloadSpeed.Text = dl.FullDisplayString;
            CardUploadSpeed.Text = ul.FullDisplayString;
            CardPeakDownload.Text = $"Peak: {peakDl.FullDisplayString}";
            CardPeakUpload.Text = $"Peak: {peakUl.FullDisplayString}";

            // Latency & Jitter
            if (_config.EnablePing && _telemetry.CurrentPingMs > 0)
            {
                CardPing.Text = $"{_telemetry.CurrentPingMs:0}";
                CardJitter.Text = $"Jitter: ±{_telemetry.CurrentJitterMs:0} ms";
            }
            else
            {
                CardPing.Text = "--";
                CardJitter.Text = "Ping disabled or inactive";
            }

            // Adapter details
            var nic = _telemetry.CurrentAdapter;
            DetailNicName.Text = string.IsNullOrEmpty(nic.Name) ? "No Active Adapter" : $"{nic.Name} ({nic.Description})";
            DetailIp.Text = $"IP: {nic.IPv4Address}";
            DetailGateway.Text = $"Gateway: {nic.GatewayAddress}";
            DetailLinkSpeed.Text = $"Link Speed: {nic.FormattedSpeed}";
            DetailMac.Text = $"MAC: {nic.MacAddress}";

            // Usage details
            DetailSessionUsage.Text = UsageRecord.FormatBytes(_usageLedger.SessionTotalBytes);
            var today = _usageLedger.GetTodayUsage();
            DetailTodayUsage.Text = today.FormattedTotal;
        }

        private void OnPauseResumeClick(object sender, RoutedEventArgs e)
        {
            _config.PauseMonitoring = !_config.PauseMonitoring;
            UpdatePauseResumeButtonState();
        }

        private void UpdatePauseResumeButtonState()
        {
            if (_config.PauseMonitoring)
            {
                TxtPauseResumeIcon.Text = "▶ ";
                TxtPauseResumeLabel.Text = "Resume";
            }
            else
            {
                TxtPauseResumeIcon.Text = "⏸ ";
                TxtPauseResumeLabel.Text = "Pause";
            }
        }

        private void OnResetSessionClick(object sender, RoutedEventArgs e)
        {
            _usageLedger.ResetSession();
            _peakDownloadBps = 0;
            _peakUploadBps = 0;
            UpdateTelemetryUI();
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
        {
            RequestOpenSettings?.Invoke();
        }

        private void OnOpenAboutClick(object sender, RoutedEventArgs e)
        {
            RequestOpenAbout?.Invoke();
        }
    }
}
