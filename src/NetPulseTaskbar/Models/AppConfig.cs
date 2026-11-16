using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NetPulseTaskbar.Models
{
    public class AppConfig : INotifyPropertyChanged
    {
        private int _pollingIntervalMs = 1000;
        private bool _enableSmoothing = true;
        private double _smoothingFactor = 0.35;
        private string _selectedAdapterId = "Auto";
        private string _pingHost = "1.1.1.1";
        private bool _enablePing = true;

        private string _positionMode = "TaskbarDock"; // "TaskbarDock" or "FreeFloating"
        private double _widgetLeft = 100;
        private double _widgetTop = 100;
        private bool _lockPosition = false;
        private bool _clickThrough = false;
        private bool _hideInFullscreen = true;

        private string _unitMode = "AutoBytes"; // AutoBits, AutoBytes, kbps, mbps, Mbps, MBPS, MBps, KB/s, MB/s
        private string _decimalPrecisionMode = "Auto"; // Auto, 0, 1, 2
        private string _downloadPrefix = "↓ ";
        private string _uploadPrefix = "↑ ";
        private bool _hideUnitSuffix = false;
        private bool _compactSpaceless = false;
        private bool _fixedWidthPadding = true;

        private string _layoutOrientation = "Vertical"; // Vertical, Horizontal, DownloadOnly, UploadOnly
        private bool _swapUploadDownloadOrder = false;
        private string _displayMode = "Full"; // Full, Compact, MinimalSpeed, DesktopWidget, TrayOnly
        private bool _showMiniSparkline = true;
        private int _sparklineHistorySeconds = 30;

        private string _fontFamily = "Segoe UI Variable Display";
        private double _fontSize = 10.5;
        private string _fontWeight = "SemiBold";
        private string _fontStyle = "Normal";
        private bool _enableTabularFigures = true;
        private bool _enableTextShadow = true;

        private string _downloadColorHex = "#00E676";
        private string _uploadColorHex = "#38BDF8";
        private string _prefixColorHex = "#94A3B8";
        private string _backgroundColorHex = "#E6111827";
        private string _borderColorHex = "#33475569";
        private double _borderThickness = 1.0;
        private double _cornerRadius = 6.0;
        private double _widgetWidth = 140.0;
        private double _widgetHeight = 36.0;

        private bool _enableDynamicThresholdColoring = false;
        private double _threshold1SpeedMbps = 5.0;
        private double _threshold2SpeedMbps = 50.0;
        private string _idleColorHex = "#94A3B8";
        private string _moderateColorHex = "#00E676";
        private string _highSaturationColorHex = "#F43F5E";

        private bool _startWithWindows = false;
        private bool _pauseMonitoring = false;
        private bool _enableSoundAlerts = false;
        private bool _notifyOnDisconnect = true;
        private double _dailyDataCapGb = 0.0;
        private string _themePreset = "Windows 11 Native";

        // Properties with change notifications
        public int PollingIntervalMs { get => _pollingIntervalMs; set => SetField(ref _pollingIntervalMs, value); }
        public bool EnableSmoothing { get => _enableSmoothing; set => SetField(ref _enableSmoothing, value); }
        public double SmoothingFactor { get => _smoothingFactor; set => SetField(ref _smoothingFactor, value); }
        public string SelectedAdapterId { get => _selectedAdapterId; set => SetField(ref _selectedAdapterId, value); }
        public string PingHost { get => _pingHost; set => SetField(ref _pingHost, value); }
        public bool EnablePing { get => _enablePing; set => SetField(ref _enablePing, value); }

        public string PositionMode { get => _positionMode; set => SetField(ref _positionMode, value); }
        public double WidgetLeft { get => _widgetLeft; set => SetField(ref _widgetLeft, value); }
        public double WidgetTop { get => _widgetTop; set => SetField(ref _widgetTop, value); }
        public bool LockPosition { get => _lockPosition; set => SetField(ref _lockPosition, value); }
        public bool ClickThrough { get => _clickThrough; set => SetField(ref _clickThrough, value); }
        public bool HideInFullscreen { get => _hideInFullscreen; set => SetField(ref _hideInFullscreen, value); }

        public string UnitMode { get => _unitMode; set => SetField(ref _unitMode, value); }
        public string DecimalPrecisionMode { get => _decimalPrecisionMode; set => SetField(ref _decimalPrecisionMode, value); }
        public string DownloadPrefix { get => _downloadPrefix; set => SetField(ref _downloadPrefix, value); }
        public string UploadPrefix { get => _uploadPrefix; set => SetField(ref _uploadPrefix, value); }
        public bool HideUnitSuffix { get => _hideUnitSuffix; set => SetField(ref _hideUnitSuffix, value); }
        public bool CompactSpaceless { get => _compactSpaceless; set => SetField(ref _compactSpaceless, value); }
        public bool FixedWidthPadding { get => _fixedWidthPadding; set => SetField(ref _fixedWidthPadding, value); }

        public string LayoutOrientation { get => _layoutOrientation; set => SetField(ref _layoutOrientation, value); }
        public bool SwapUploadDownloadOrder { get => _swapUploadDownloadOrder; set => SetField(ref _swapUploadDownloadOrder, value); }
        public string DisplayMode { get => _displayMode; set => SetField(ref _displayMode, value); }
        public bool ShowMiniSparkline { get => _showMiniSparkline; set => SetField(ref _showMiniSparkline, value); }
        public int SparklineHistorySeconds { get => _sparklineHistorySeconds; set => SetField(ref _sparklineHistorySeconds, value); }

        public string FontFamily { get => _fontFamily; set => SetField(ref _fontFamily, value); }
        public double FontSize { get => _fontSize; set => SetField(ref _fontSize, value); }
        public string FontWeight { get => _fontWeight; set => SetField(ref _fontWeight, value); }
        public string FontStyle { get => _fontStyle; set => SetField(ref _fontStyle, value); }
        public bool EnableTabularFigures { get => _enableTabularFigures; set => SetField(ref _enableTabularFigures, value); }
        public bool EnableTextShadow { get => _enableTextShadow; set => SetField(ref _enableTextShadow, value); }

        public string DownloadColorHex { get => _downloadColorHex; set => SetField(ref _downloadColorHex, value); }
        public string UploadColorHex { get => _uploadColorHex; set => SetField(ref _uploadColorHex, value); }
        public string PrefixColorHex { get => _prefixColorHex; set => SetField(ref _prefixColorHex, value); }
        public string BackgroundColorHex { get => _backgroundColorHex; set => SetField(ref _backgroundColorHex, value); }
        public string BorderColorHex { get => _borderColorHex; set => SetField(ref _borderColorHex, value); }
        public double BorderThickness { get => _borderThickness; set => SetField(ref _borderThickness, value); }
        public double CornerRadius { get => _cornerRadius; set => SetField(ref _cornerRadius, value); }
        public double WidgetWidth { get => _widgetWidth; set => SetField(ref _widgetWidth, value); }
        public double WidgetHeight { get => _widgetHeight; set => SetField(ref _widgetHeight, value); }

        public bool EnableDynamicThresholdColoring { get => _enableDynamicThresholdColoring; set => SetField(ref _enableDynamicThresholdColoring, value); }
        public double Threshold1SpeedMbps { get => _threshold1SpeedMbps; set => SetField(ref _threshold1SpeedMbps, value); }
        public double Threshold2SpeedMbps { get => _threshold2SpeedMbps; set => SetField(ref _threshold2SpeedMbps, value); }
        public string IdleColorHex { get => _idleColorHex; set => SetField(ref _idleColorHex, value); }
        public string ModerateColorHex { get => _moderateColorHex; set => SetField(ref _moderateColorHex, value); }
        public string HighSaturationColorHex { get => _highSaturationColorHex; set => SetField(ref _highSaturationColorHex, value); }

        public bool StartWithWindows { get => _startWithWindows; set => SetField(ref _startWithWindows, value); }
        public bool PauseMonitoring { get => _pauseMonitoring; set => SetField(ref _pauseMonitoring, value); }
        public bool EnableSoundAlerts { get => _enableSoundAlerts; set => SetField(ref _enableSoundAlerts, value); }
        public bool NotifyOnDisconnect { get => _notifyOnDisconnect; set => SetField(ref _notifyOnDisconnect, value); }
        public double DailyDataCapGb { get => _dailyDataCapGb; set => SetField(ref _dailyDataCapGb, value); }
        public string ThemePreset { get => _themePreset; set => SetField(ref _themePreset, value); }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public AppConfig Clone()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            return System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
    }
}
