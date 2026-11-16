using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using NetPulseTaskbar.Models;
using NetPulseTaskbar.Native;
using NetPulseTaskbar.Services;

namespace NetPulseTaskbar.Views
{
    public partial class WidgetWindow : Window
    {
        private readonly AppConfig _config;
        private readonly NetworkTelemetryService _telemetry;
        private readonly TaskbarDockManager _dockManager;
        private readonly DispatcherTimer _hoverTimer;

        private TelemetryFlyoutWindow? _flyoutWindow;
        private bool _isDragging = false;
        private NativeMethods.POINT _dragStartScreenPoint;
        private double _dragDpiScaleX = 1.0;
        private double _dragDpiScaleY = 1.0;
        private double _origWindowLeft;
        private double _origWindowTop;

        public event Action? RequestOpenDashboard;
        public event Action? RequestOpenSettings;
        public event Action? RequestOpenAbout;
        public event Action? RequestExit;

        public WidgetWindow(AppConfig config, NetworkTelemetryService telemetry)
        {
            InitializeComponent();

            _config = config;
            _telemetry = telemetry;
            _dockManager = new TaskbarDockManager(this, _config);

            _hoverTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _hoverTimer.Tick += OnHoverTimerTick;

            Loaded += OnLoaded;
            Closed += OnClosed;

            ApplyConfiguration();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _dockManager.Initialize();
            _telemetry.TelemetryUpdated += OnTelemetryUpdated;
            _config.PropertyChanged += (s, ev) => Dispatcher.Invoke(ApplyConfiguration);
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _telemetry.TelemetryUpdated -= OnTelemetryUpdated;
            _dockManager.Dispose();
            _hoverTimer.Stop();
            _flyoutWindow?.Close();
        }

        public void ApplyConfiguration()
        {
            if (_config.DisplayMode == "TrayOnly")
            {
                Visibility = Visibility.Collapsed;
                return;
            }
            else if (Visibility != Visibility.Visible)
            {
                Visibility = Visibility.Visible;
            }

            Width = _config.WidgetWidth;
            Height = _config.WidgetHeight;

            // Background & Border styling
            ContainerBorder.Background = ParseBrush(_config.BackgroundColorHex, "#E6111827");
            ContainerBorder.BorderBrush = ParseBrush(_config.BorderColorHex, "#33475569");
            ContainerBorder.BorderThickness = new Thickness(_config.BorderThickness);
            ContainerBorder.CornerRadius = new CornerRadius(_config.CornerRadius);

            // Shadow
            var textShadow = _config.EnableTextShadow ? (DropShadowEffect)FindResource("TextDropShadow") : null;
            TopRowText.Effect = textShadow;
            BottomRowText.Effect = textShadow;
            HorizFirstText.Effect = textShadow;
            HorizSecondText.Effect = textShadow;
            SingleText.Effect = textShadow;

            // Font typography
            FontFamily targetFont;
            try
            {
                targetFont = new FontFamily(_config.FontFamily);
            }
            catch
            {
                targetFont = new FontFamily("Segoe UI Variable Display");
            }

            var fontWeight = ParseFontWeight(_config.FontWeight);
            var fontStyle = _config.FontStyle?.Equals("Italic", StringComparison.OrdinalIgnoreCase) == true ? FontStyles.Italic : FontStyles.Normal;

            ApplyFontToText(TopRowText, targetFont, _config.FontSize, fontWeight, fontStyle);
            ApplyFontToText(BottomRowText, targetFont, _config.FontSize, fontWeight, fontStyle);
            ApplyFontToText(HorizFirstText, targetFont, _config.FontSize, fontWeight, fontStyle);
            ApplyFontToText(HorizSecondText, targetFont, _config.FontSize, fontWeight, fontStyle);
            ApplyFontToText(SingleText, targetFont, _config.FontSize + 2, fontWeight, fontStyle);

            // Sparkline styling
            MiniSparkline.Visibility = _config.ShowMiniSparkline ? Visibility.Visible : _config.DisplayMode == "MinimalSpeed" ? Visibility.Collapsed : Visibility.Visible;
            MiniSparkline.DownloadBrush = ParseBrush(_config.DownloadColorHex, "#00E676");
            MiniSparkline.UploadBrush = ParseBrush(_config.UploadColorHex, "#38BDF8");

            // Layout arrangement
            UpdateLayoutMode();

            // Window styles & dock manager
            _dockManager.ApplyWindowStyles();
            _dockManager.UpdatePosition();
        }

        private void UpdateLayoutMode()
        {
            string mode = _config.LayoutOrientation ?? "Vertical";

            VerticalLayout.Visibility = Visibility.Collapsed;
            HorizontalLayout.Visibility = Visibility.Collapsed;
            SingleLayout.Visibility = Visibility.Collapsed;

            switch (mode)
            {
                case "Horizontal":
                    HorizontalLayout.Visibility = Visibility.Visible;
                    break;
                case "DownloadOnly":
                case "UploadOnly":
                    SingleLayout.Visibility = Visibility.Visible;
                    break;
                case "Vertical":
                default:
                    VerticalLayout.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void OnTelemetryUpdated(NetworkSpeedSample sample)
        {
            Dispatcher.Invoke(() =>
            {
                var dlResult = SpeedFormatter.FormatSpeed(sample.DownloadBytesPerSec, _config, false);
                var ulResult = SpeedFormatter.FormatSpeed(sample.UploadBytesPerSec, _config, true);

                Brush dlBrush = GetSpeedBrush(sample.DownloadBytesPerSec, false);
                Brush ulBrush = GetSpeedBrush(sample.UploadBytesPerSec, true);

                bool swap = _config.SwapUploadDownloadOrder;
                string firstText = swap ? ulResult.FullDisplayString : dlResult.FullDisplayString;
                string secondText = swap ? dlResult.FullDisplayString : ulResult.FullDisplayString;

                Brush firstBrush = swap ? ulBrush : dlBrush;
                Brush secondBrush = swap ? dlBrush : ulBrush;

                // Vertical Layout
                TopRowText.Text = firstText;
                TopRowText.Foreground = firstBrush;

                BottomRowText.Text = secondText;
                BottomRowText.Foreground = secondBrush;

                // Horizontal Layout
                HorizFirstText.Text = firstText;
                HorizFirstText.Foreground = firstBrush;

                HorizSecondText.Text = secondText;
                HorizSecondText.Foreground = secondBrush;

                // Single Stream Layout
                if (_config.LayoutOrientation == "UploadOnly")
                {
                    SingleText.Text = ulResult.FullDisplayString;
                    SingleText.Foreground = ulBrush;
                }
                else
                {
                    SingleText.Text = dlResult.FullDisplayString;
                    SingleText.Foreground = dlBrush;
                }

                // Add to sparkline
                if (_config.ShowMiniSparkline)
                {
                    MiniSparkline.AddSample(sample.DownloadBytesPerSec, sample.UploadBytesPerSec);
                }
            });
        }

        private Brush GetSpeedBrush(double bytesPerSec, bool isUpload)
        {
            if (!_config.EnableDynamicThresholdColoring)
            {
                string hex = isUpload ? _config.UploadColorHex : _config.DownloadColorHex;
                return ParseBrush(hex, isUpload ? "#38BDF8" : "#00E676");
            }

            double speedMbps = (bytesPerSec * 8.0) / 1_000_000.0;
            if (speedMbps >= _config.Threshold2SpeedMbps)
            {
                return ParseBrush(_config.HighSaturationColorHex, "#F43F5E");
            }
            if (speedMbps >= _config.Threshold1SpeedMbps)
            {
                return ParseBrush(_config.ModerateColorHex, "#00E676");
            }
            return ParseBrush(_config.IdleColorHex, "#94A3B8");
        }

        private void ApplyFontToText(TextBlock tb, FontFamily font, double size, FontWeight weight, FontStyle style)
        {
            tb.FontFamily = font;
            tb.FontSize = Math.Max(7.0, Math.Min(32.0, size));
            tb.FontWeight = weight;
            tb.FontStyle = style;
        }

        private FontWeight ParseFontWeight(string? weight)
        {
            return weight?.ToLowerInvariant() switch
            {
                "bold" => FontWeights.Bold,
                "extrabold" => FontWeights.ExtraBold,
                "medium" => FontWeights.Medium,
                "light" => FontWeights.Light,
                "semibold" => FontWeights.SemiBold,
                _ => FontWeights.Normal,
            };
        }

        private Brush ParseBrush(string? hex, string defaultHex)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    return brush;
                }
            }
            catch { }

            var defColor = (Color)ColorConverter.ConvertFromString(defaultHex);
            var defBrush = new SolidColorBrush(defColor);
            defBrush.Freeze();
            return defBrush;
        }

        #region Mouse & Drag Handling

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_config.LockPosition) return;

            NativeMethods.GetCursorPos(out _dragStartScreenPoint);
            _origWindowLeft = Left;
            _origWindowTop = Top;

            var source = PresentationSource.FromVisual(this);
            _dragDpiScaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            _dragDpiScaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            _isDragging = true;
            _dockManager.PauseDockSync(true);
            Cursor = Cursors.SizeAll;
            CaptureMouse();
            _hoverTimer.Stop();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            NativeMethods.GetCursorPos(out var currentScreenPt);
            double deltaX = (currentScreenPt.X - _dragStartScreenPoint.X) / _dragDpiScaleX;
            double deltaY = (currentScreenPt.Y - _dragStartScreenPoint.Y) / _dragDpiScaleY;

            double targetLeft = _origWindowLeft + deltaX;
            double targetTop = _origWindowTop + deltaY;

            // Apply magnetic edge snapping
            _dockManager.SnapToEdges(ref targetLeft, ref targetTop, ActualWidth, ActualHeight);

            Left = targetLeft;
            Top = targetTop;

            if (_config.PositionMode == "TaskbarDock")
            {
                _config.PositionMode = "FreeFloating";
            }
            _config.WidgetLeft = Left;
            _config.WidgetTop = Top;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
                _dockManager.PauseDockSync(false);
                _dockManager.EnforceTopMost();

                var app = (App)Application.Current;
                app?.ConfigService?.Save();
            }
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            _hoverTimer.Start();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _hoverTimer.Stop();
            _flyoutWindow?.Hide();
        }

        private void OnHoverTimerTick(object? sender, EventArgs e)
        {
            _hoverTimer.Stop();
            if (!IsMouseOver) return;

            if (_flyoutWindow == null)
            {
                _flyoutWindow = new TelemetryFlyoutWindow(_telemetry, _config);
            }

            _flyoutWindow.ShowAboveWidget(this);
        }

        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            RequestOpenDashboard?.Invoke();
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var menu = new ContextMenu();

            var miDash = new MenuItem { Header = "Open Dashboard" };
            miDash.Click += (s, ev) => RequestOpenDashboard?.Invoke();
            menu.Items.Add(miDash);

            var miSettings = new MenuItem { Header = "Settings..." };
            miSettings.Click += (s, ev) => RequestOpenSettings?.Invoke();
            menu.Items.Add(miSettings);

            menu.Items.Add(new Separator());

            var miDock = new MenuItem
            {
                Header = "Dock to Taskbar",
                IsCheckable = true,
                IsChecked = _config.PositionMode == "TaskbarDock"
            };
            miDock.Click += (s, ev) =>
            {
                _config.PositionMode = miDock.IsChecked ? "TaskbarDock" : "FreeFloating";
                _dockManager.UpdatePosition();
            };
            menu.Items.Add(miDock);

            var miLock = new MenuItem
            {
                Header = "Lock Position",
                IsCheckable = true,
                IsChecked = _config.LockPosition
            };
            miLock.Click += (s, ev) => _config.LockPosition = miLock.IsChecked;
            menu.Items.Add(miLock);

            var miClickThrough = new MenuItem
            {
                Header = "Click-Through Mode (Ctrl+Alt to interact)",
                IsCheckable = true,
                IsChecked = _config.ClickThrough
            };
            miClickThrough.Click += (s, ev) =>
            {
                _config.ClickThrough = miClickThrough.IsChecked;
                _dockManager.ApplyWindowStyles();
            };
            menu.Items.Add(miClickThrough);

            menu.Items.Add(new Separator());

            var miAbout = new MenuItem { Header = "About NetPulse..." };
            miAbout.Click += (s, ev) => RequestOpenAbout?.Invoke();
            menu.Items.Add(miAbout);

            var miExit = new MenuItem { Header = "Exit NetPulse" };
            miExit.Click += (s, ev) => RequestExit?.Invoke();
            menu.Items.Add(miExit);

            menu.IsOpen = true;
        }

        #endregion
    }
}
