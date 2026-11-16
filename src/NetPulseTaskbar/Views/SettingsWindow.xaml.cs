using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using NetPulseTaskbar.Models;
using NetPulseTaskbar.Native;
using NetPulseTaskbar.Services;

namespace NetPulseTaskbar.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly ConfigService _configService;
        private readonly NetworkTelemetryService _telemetry;
        private readonly UsageLedgerService _usageLedger;
        private readonly AppConfig _config;

        private bool _isInitializing = true;

        public event Action? RequestOpenAbout;

        public SettingsWindow(ConfigService configService, NetworkTelemetryService telemetry, UsageLedgerService usageLedger)
        {
            InitializeComponent();

            _configService = configService;
            _telemetry = telemetry;
            _usageLedger = usageLedger;
            _config = configService.Current;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;

            PopulateAdapters();
            PopulateFonts();
            LoadConfigToUi();
            RefreshUsageHistory();

            _isInitializing = false;
        }

        private void PopulateAdapters()
        {
            CmbAdapters.Items.Clear();
            CmbAdapters.Items.Add(new ComboBoxItem { Content = "Auto (All Active Adapters Aggregated)", Tag = "Auto" });

            var adapters = _telemetry.GetAvailableAdapters();
            int selectedIndex = 0;
            int currentIndex = 1;

            foreach (var nic in adapters)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{nic.Name} - {nic.Description} [{nic.Status}]",
                    Tag = nic.Id
                };
                CmbAdapters.Items.Add(item);

                if (nic.Id == _config.SelectedAdapterId)
                {
                    selectedIndex = currentIndex;
                }
                currentIndex++;
            }

            CmbAdapters.SelectedIndex = selectedIndex;
        }

        private void PopulateFonts()
        {
            CmbFontFamily.Items.Clear();
            var systemFonts = Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();

            int selectedIndex = 0;
            for (int i = 0; i < systemFonts.Count; i++)
            {
                var font = systemFonts[i];
                CmbFontFamily.Items.Add(font.Source);
                if (string.Equals(font.Source, _config.FontFamily, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            if (CmbFontFamily.Items.Count > 0)
            {
                CmbFontFamily.SelectedIndex = selectedIndex;
            }
        }

        private void LoadConfigToUi()
        {
            // Tab 1: General & Network
            CmbPollingInterval.SelectedIndex = _config.PollingIntervalMs switch
            {
                250 => 0,
                500 => 1,
                2000 => 3,
                _ => 2
            };

            ChkSmoothing.IsChecked = _config.EnableSmoothing;
            SliderSmoothing.Value = _config.SmoothingFactor;
            TxtSmoothingVal.Text = _config.SmoothingFactor.ToString("0.00");

            CmbPositionMode.SelectedIndex = _config.PositionMode == "FreeFloating" ? 1 : 0;
            ChkLockPosition.IsChecked = _config.LockPosition;
            ChkClickThrough.IsChecked = _config.ClickThrough;
            ChkHideFullscreen.IsChecked = _config.HideInFullscreen;
            ChkStartWithWindows.IsChecked = AutoStartManager.IsAutoStartEnabled();

            ChkEnablePing.IsChecked = _config.EnablePing;
            TxtPingHost.Text = _config.PingHost;

            // Tab 2: Units & Formatting
            CmbUnitMode.SelectedIndex = _config.UnitMode switch
            {
                "AutoBits" or "Auto (Bits/s)" => 1,
                "kbps" => 2,
                "mbps" => 3,
                "Mbps" => 4,
                "MBPS" => 5,
                "MBps" => 6,
                "KB/s" => 7,
                "MB/s" => 8,
                _ => 0
            };

            CmbDecimals.SelectedIndex = _config.DecimalPrecisionMode switch
            {
                "0" => 1,
                "1" => 2,
                "2" => 3,
                _ => 0
            };

            TxtDlPrefix.Text = _config.DownloadPrefix;
            TxtUlPrefix.Text = _config.UploadPrefix;
            ChkHideSuffix.IsChecked = _config.HideUnitSuffix;
            ChkCompactSpaceless.IsChecked = _config.CompactSpaceless;
            ChkFixedWidthPadding.IsChecked = _config.FixedWidthPadding;

            CmbLayout.SelectedIndex = _config.LayoutOrientation switch
            {
                "Horizontal" => 1,
                "DownloadOnly" => 2,
                "UploadOnly" => 3,
                _ => 0
            };

            CmbDisplayMode.SelectedIndex = _config.DisplayMode switch
            {
                "Compact" => 1,
                "MinimalSpeed" => 2,
                "DesktopWidget" => 3,
                "TrayOnly" => 4,
                _ => 0
            };

            ChkSwapOrder.IsChecked = _config.SwapUploadDownloadOrder;

            // Tab 3: Typography & Colors
            SliderFontSize.Value = _config.FontSize;
            TxtFontSizeVal.Text = _config.FontSize.ToString("0.0");

            CmbFontWeight.SelectedIndex = _config.FontWeight switch
            {
                "Medium" => 1,
                "SemiBold" => 2,
                "Bold" => 3,
                "ExtraBold" => 4,
                _ => 0
            };

            CmbFontStyle.SelectedIndex = _config.FontStyle?.Equals("Italic", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
            ChkTabularFigures.IsChecked = _config.EnableTabularFigures;
            ChkTextShadow.IsChecked = _config.EnableTextShadow;

            TxtDlColor.Text = _config.DownloadColorHex;
            TxtUlColor.Text = _config.UploadColorHex;
            TxtBgColor.Text = _config.BackgroundColorHex;
            TxtBorderColor.Text = _config.BorderColorHex;
            UpdateSwatches();

            ChkDynamicColors.IsChecked = _config.EnableDynamicThresholdColoring;
            SliderThresh1.Value = _config.Threshold1SpeedMbps;
            TxtThresh1Val.Text = $"{_config.Threshold1SpeedMbps:0} Mbps";
            SliderThresh2.Value = _config.Threshold2SpeedMbps;
            TxtThresh2Val.Text = $"{_config.Threshold2SpeedMbps:0} Mbps";

            ChkSparkline.IsChecked = _config.ShowMiniSparkline;
            SliderWidth.Value = _config.WidgetWidth;
            TxtWidthVal.Text = $"{_config.WidgetWidth:0}px";
            SliderCornerRadius.Value = _config.CornerRadius;
            TxtCornerRadiusVal.Text = $"{_config.CornerRadius:0}px";

            // Tab 4: Data Usage & Themes
            TxtDataCap.Text = _config.DailyDataCapGb.ToString("0.##");
            ChkSoundAlerts.IsChecked = _config.EnableSoundAlerts;
            ChkDisconnectNotify.IsChecked = _config.NotifyOnDisconnect;
        }

        private void RefreshUsageHistory()
        {
            var history = _usageLedger.GetHistoryList();
            GridUsageHistory.ItemsSource = history;
        }

        private void UpdateSwatches()
        {
            UpdateSingleSwatch(SwatchDl, TxtDlColor.Text, "#00E676");
            UpdateSingleSwatch(SwatchUl, TxtUlColor.Text, "#38BDF8");
            UpdateSingleSwatch(SwatchBg, TxtBgColor.Text, "#111827");
            UpdateSingleSwatch(SwatchBorder, TxtBorderColor.Text, "#475569");
        }

        private void UpdateSingleSwatch(Border border, string hex, string defaultHex)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    border.Background = new SolidColorBrush(color);
                    return;
                }
            }
            catch { }

            var defColor = (Color)ColorConverter.ConvertFromString(defaultHex);
            border.Background = new SolidColorBrush(defColor);
        }

        private void CommitSave()
        {
            if (_isInitializing) return;
            _configService.Save();
        }

        #region Event Handlers - Tab 1

        private void OnAdapterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (CmbAdapters.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _config.SelectedAdapterId = tag;
                CommitSave();
            }
        }

        private void OnPollingIntervalChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _config.PollingIntervalMs = CmbPollingInterval.SelectedIndex switch
            {
                0 => 250,
                1 => 500,
                3 => 2000,
                _ => 1000
            };
            CommitSave();
        }

        private void OnSmoothingChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.EnableSmoothing = ChkSmoothing.IsChecked == true;
            CommitSave();
        }

        private void OnSmoothingFactorChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            _config.SmoothingFactor = Math.Round(SliderSmoothing.Value, 2);
            TxtSmoothingVal.Text = _config.SmoothingFactor.ToString("0.00");
            CommitSave();
        }

        private void OnPositionModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _config.PositionMode = CmbPositionMode.SelectedIndex == 1 ? "FreeFloating" : "TaskbarDock";
            CommitSave();
        }

        private void OnLockPositionChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.LockPosition = ChkLockPosition.IsChecked == true;
            CommitSave();
        }

        private void OnClickThroughChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.ClickThrough = ChkClickThrough.IsChecked == true;
            CommitSave();
        }

        private void OnHideFullscreenChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.HideInFullscreen = ChkHideFullscreen.IsChecked == true;
            CommitSave();
        }

        private void OnStartWithWindowsChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool enabled = ChkStartWithWindows.IsChecked == true;
            _config.StartWithWindows = enabled;
            AutoStartManager.SetAutoStart(enabled);
            CommitSave();
        }

        private void OnEnablePingChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.EnablePing = ChkEnablePing.IsChecked == true;
            CommitSave();
        }

        private void OnPingHostChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            _config.PingHost = TxtPingHost.Text.Trim();
            CommitSave();
        }

        #endregion

        #region Event Handlers - Tab 2

        private void OnUnitModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _config.UnitMode = CmbUnitMode.SelectedIndex switch
            {
                1 => "AutoBits",
                2 => "kbps",
                3 => "mbps",
                4 => "Mbps",
                5 => "MBPS",
                6 => "MBps",
                7 => "KB/s",
                8 => "MB/s",
                _ => "AutoBytes"
            };
            CommitSave();
        }

        private void OnDecimalsChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _config.DecimalPrecisionMode = CmbDecimals.SelectedIndex switch
            {
                1 => "0",
                2 => "1",
                3 => "2",
                _ => "Auto"
            };
            CommitSave();
        }

        private void OnPrefixesChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            _config.DownloadPrefix = TxtDlPrefix.Text;
            _config.UploadPrefix = TxtUlPrefix.Text;
            CommitSave();
        }

        private void OnFormattingCheckChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.HideUnitSuffix = ChkHideSuffix.IsChecked == true;
            _config.CompactSpaceless = ChkCompactSpaceless.IsChecked == true;
            _config.FixedWidthPadding = ChkFixedWidthPadding.IsChecked == true;
            CommitSave();
        }

        private void OnLayoutChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _config.LayoutOrientation = CmbLayout.SelectedIndex switch
            {
                1 => "Horizontal",
                2 => "DownloadOnly",
                3 => "UploadOnly",
                _ => "Vertical"
            };
            CommitSave();
        }

        private void OnDisplayModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _config.DisplayMode = CmbDisplayMode.SelectedIndex switch
            {
                1 => "Compact",
                2 => "MinimalSpeed",
                3 => "DesktopWidget",
                4 => "TrayOnly",
                _ => "Full"
            };
            CommitSave();
        }

        private void OnSwapOrderChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.SwapUploadDownloadOrder = ChkSwapOrder.IsChecked == true;
            CommitSave();
        }

        #endregion

        #region Event Handlers - Tab 3

        private void OnFontFamilyChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (CmbFontFamily.SelectedItem is string fontName)
            {
                _config.FontFamily = fontName;
                CommitSave();
            }
        }

        private void OnFontSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            _config.FontSize = Math.Round(SliderFontSize.Value, 1);
            TxtFontSizeVal.Text = _config.FontSize.ToString("0.0");
            CommitSave();
        }

        private void OnFontWeightChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _config.FontWeight = (CmbFontWeight.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "SemiBold";
            CommitSave();
        }

        private void OnFontStyleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            _config.FontStyle = (CmbFontStyle.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Normal";
            CommitSave();
        }

        private void OnTypographyCheckChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.EnableTabularFigures = ChkTabularFigures.IsChecked == true;
            _config.EnableTextShadow = ChkTextShadow.IsChecked == true;
            CommitSave();
        }

        private void OnColorsChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            _config.DownloadColorHex = TxtDlColor.Text.Trim();
            _config.UploadColorHex = TxtUlColor.Text.Trim();
            _config.BackgroundColorHex = TxtBgColor.Text.Trim();
            _config.BorderColorHex = TxtBorderColor.Text.Trim();
            UpdateSwatches();
            CommitSave();
        }

        private void OnDynamicColorsChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.EnableDynamicThresholdColoring = ChkDynamicColors.IsChecked == true;
            CommitSave();
        }

        private void OnThresh1Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            _config.Threshold1SpeedMbps = Math.Round(SliderThresh1.Value);
            TxtThresh1Val.Text = $"{_config.Threshold1SpeedMbps:0} Mbps";
            CommitSave();
        }

        private void OnThresh2Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            _config.Threshold2SpeedMbps = Math.Round(SliderThresh2.Value);
            TxtThresh2Val.Text = $"{_config.Threshold2SpeedMbps:0} Mbps";
            CommitSave();
        }

        private void OnSparklineChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.ShowMiniSparkline = ChkSparkline.IsChecked == true;
            CommitSave();
        }

        private void OnDimensionsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            _config.WidgetWidth = Math.Round(SliderWidth.Value);
            TxtWidthVal.Text = $"{_config.WidgetWidth:0}px";
            CommitSave();
        }

        private void OnCornerRadiusChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            _config.CornerRadius = Math.Round(SliderCornerRadius.Value);
            TxtCornerRadiusVal.Text = $"{_config.CornerRadius:0}px";
            CommitSave();
        }

        #endregion

        #region Event Handlers - Tab 4

        private void OnThemePresetClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string themeName)
            {
                _configService.ApplyThemePreset(themeName);
                _isInitializing = true;
                LoadConfigToUi();
                _isInitializing = false;
            }
        }

        private void OnImportThemeClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Import NetPulse Theme Preset"
            };

            if (dlg.ShowDialog() == true)
            {
                if (_configService.ImportFromFile(dlg.FileName))
                {
                    _isInitializing = true;
                    LoadConfigToUi();
                    _isInitializing = false;
                    MessageBox.Show("Theme preset imported successfully!", "NetPulse", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void OnExportThemeClick(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = $"NetPulse_Theme_{DateTime.Now:yyyyMMdd}.json",
                Title = "Export NetPulse Theme Preset"
            };

            if (dlg.ShowDialog() == true)
            {
                _configService.ExportToFile(dlg.FileName);
                MessageBox.Show("Theme preset exported successfully!", "NetPulse", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnDataCapChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (double.TryParse(TxtDataCap.Text, out double val) && val >= 0)
            {
                _config.DailyDataCapGb = val;
                CommitSave();
            }
        }

        private void OnSoundAlertsChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.EnableSoundAlerts = ChkSoundAlerts.IsChecked == true;
            CommitSave();
        }

        private void OnDisconnectNotifyChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _config.NotifyOnDisconnect = ChkDisconnectNotify.IsChecked == true;
            CommitSave();
        }

        private void OnExportCsvClick(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"NetPulse_DataUsage_{DateTime.Now:yyyyMMdd}.csv",
                Title = "Export Data Usage History"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _usageLedger.ExportToCsv(dlg.FileName);
                    MessageBox.Show("Usage history exported to CSV successfully!", "NetPulse", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "NetPulse Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Search & Filtering

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtSearch.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(query))
            {
                FilterChildren(PanelGeneral, null);
                FilterChildren(PanelUnits, null);
                FilterChildren(PanelAppearance, null);
                FilterChildren(PanelUsageThemes, null);
                return;
            }

            FilterChildren(PanelGeneral, query);
            FilterChildren(PanelUnits, query);
            FilterChildren(PanelAppearance, query);
            FilterChildren(PanelUsageThemes, query);
        }

        private void FilterChildren(Panel panel, string? query)
        {
            foreach (UIElement elem in panel.Children)
            {
                if (elem is Border border)
                {
                    if (string.IsNullOrEmpty(query))
                    {
                        border.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // Check text inside border
                        string allText = ExtractText(border).ToLowerInvariant();
                        border.Visibility = allText.Contains(query) ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

        private string ExtractText(DependencyObject obj)
        {
            string text = "";
            if (obj is TextBlock tb) text += tb.Text + " ";
            if (obj is CheckBox cb) text += cb.Content + " ";
            if (obj is Button btn) text += btn.Content + " ";

            int count = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < count; i++)
            {
                text += ExtractText(VisualTreeHelper.GetChild(obj, i)) + " ";
            }
            return text;
        }

        #endregion

        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            RequestOpenAbout?.Invoke();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
