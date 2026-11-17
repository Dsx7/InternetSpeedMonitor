using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using NetPulseTaskbar.Models;
using NetPulseTaskbar.Native;
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
        private bool _isMonthFilterInitializing = false;

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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.EnableImmersiveDarkMode(hwnd);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _telemetry.TelemetryUpdated += OnTelemetryUpdated;
            _telemetry.ConnectionStateChanged += OnConnectionStateChanged;

            // Load initial history for chart
            DashboardChart.Clear();
            var history = _telemetry.GetHistory();
            foreach (var s in history)
            {
                DashboardChart.AddSample(s.DownloadBytesPerSec, s.UploadBytesPerSec);
            }

            UpdatePauseResumeButtonState();
            PopulateMonthFilter();
            UpdateTelemetryUI();
            RefreshHistoryView();
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

            // Live usage snapshot
            var today = _usageLedger.GetTodayUsage();
            var thisMonth = _usageLedger.GetThisMonthUsage();
            var allTime = _usageLedger.GetAllTimeUsage();

            DetailTodayUsage.Text = today.FormattedTotal;
            DetailMonthUsage.Text = thisMonth.FormattedTotal;
            DetailAllTimeUsage.Text = allTime.FormattedTotal;
        }

        #region Navigation Tabs

        private void OnNavTabChanged(object sender, RoutedEventArgs e)
        {
            if (ViewLiveMonitor == null || ViewDataHistory == null) return;

            if (TabBtnLive?.IsChecked == true)
            {
                ViewLiveMonitor.Visibility = Visibility.Visible;
                ViewDataHistory.Visibility = Visibility.Collapsed;
            }
            else if (TabBtnHistory?.IsChecked == true)
            {
                ViewLiveMonitor.Visibility = Visibility.Collapsed;
                ViewDataHistory.Visibility = Visibility.Visible;
                RefreshHistoryView();
            }
        }

        private void OnSwitchToHistoryTabClick(object sender, RoutedEventArgs e)
        {
            if (TabBtnHistory != null)
            {
                TabBtnHistory.IsChecked = true;
            }
        }

        #endregion

        #region Data Usage & Daily History Logic

        private void PopulateMonthFilter()
        {
            _isMonthFilterInitializing = true;

            CmbHistoryMonthFilter.Items.Clear();
            CmbHistoryMonthFilter.Items.Add(new ComboBoxItem
            {
                Content = "All Recorded Months",
                Tag = "All"
            });

            var availableMonths = _usageLedger.GetAvailableMonths();
            string currentMonthTag = DateTime.Now.ToString("yyyy-MM");
            int selectedIndex = 0;

            for (int i = 0; i < availableMonths.Count; i++)
            {
                string m = availableMonths[i];
                string label = m;
                if (DateTime.TryParseExact(m, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    label = dt.ToString("MMMM yyyy");
                }

                var item = new ComboBoxItem
                {
                    Content = label,
                    Tag = m
                };
                CmbHistoryMonthFilter.Items.Add(item);

                if (m.Equals(currentMonthTag, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i + 1; // +1 because item 0 is "All Recorded Months"
                }
            }

            CmbHistoryMonthFilter.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            _isMonthFilterInitializing = false;
        }

        private void RefreshHistoryView()
        {
            var today = _usageLedger.GetTodayUsage();
            var thisMonth = _usageLedger.GetThisMonthUsage();
            var allTime = _usageLedger.GetAllTimeUsage();

            HistoryCardTodayTotal.Text = today.FormattedTotal;
            HistoryCardTodayBreakdown.Text = $"↓ {today.FormattedDownload}  •  ↑ {today.FormattedUpload}";

            HistoryCardMonthTotal.Text = thisMonth.FormattedTotal;
            HistoryCardMonthBreakdown.Text = $"↓ {thisMonth.FormattedDownload}  •  ↑ {thisMonth.FormattedUpload}";

            HistoryCardAllTimeTotal.Text = allTime.FormattedTotal;
            HistoryCardAllTimeBreakdown.Text = $"↓ {allTime.FormattedDownload}  •  ↑ {allTime.FormattedUpload}";

            ApplyMonthFilter();
        }

        private void OnHistoryMonthFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isMonthFilterInitializing) return;
            ApplyMonthFilter();
        }

        private void ApplyMonthFilter()
        {
            if (GridDailyUsage == null || CmbHistoryMonthFilter == null) return;

            string? filterTag = null;
            if (CmbHistoryMonthFilter.SelectedItem is ComboBoxItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag) && tag != "All")
                {
                    filterTag = tag;
                }
            }

            var records = _usageLedger.GetDailyRecordsForMonth(filterTag);
            GridDailyUsage.ItemsSource = records;

            if (TxtHistoryDaysCount != null)
            {
                if (string.IsNullOrEmpty(filterTag))
                {
                    TxtHistoryDaysCount.Text = $"Showing all {records.Count} recorded day(s)";
                }
                else
                {
                    TxtHistoryDaysCount.Text = $"Showing {records.Count} recorded day(s) for selected month";
                }
            }
        }

        private void OnExportHistoryCsvClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Title = "Export NetPulse Data Usage History",
                    Filter = "CSV (Comma Delimited) (*.csv)|*.csv",
                    FileName = $"NetPulse_Data_Usage_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (sfd.ShowDialog() == true)
                {
                    _usageLedger.ExportToCsv(sfd.FileName);
                    MessageBox.Show(
                        $"Usage history successfully exported to:\n{sfd.FileName}",
                        "Export Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export CSV: {ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Global Header Actions

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
            RefreshHistoryView();
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
        {
            RequestOpenSettings?.Invoke();
        }

        private void OnOpenAboutClick(object sender, RoutedEventArgs e)
        {
            RequestOpenAbout?.Invoke();
        }

        #endregion
    }
}
