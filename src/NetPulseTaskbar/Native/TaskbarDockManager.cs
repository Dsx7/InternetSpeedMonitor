using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using NetPulseTaskbar.Models;

namespace NetPulseTaskbar.Native
{
    public class TaskbarDockManager : IDisposable
    {
        private readonly Window _window;
        private readonly AppConfig _config;
        private readonly DispatcherTimer _pollTimer;
        private IntPtr _hwnd = IntPtr.Zero;
        private IntPtr _winEventHook = IntPtr.Zero;
        private NativeMethods.WinEventDelegate? _winEventDelegate;
        private bool _isClickThroughEffective = false;
        private bool _isTemporarilyInteractive = false;
        private bool _isDockSyncPaused = false;
        private bool _isDisposed = false;

        public event Action<bool>? FullscreenStateChanged;

        public void PauseDockSync(bool pause)
        {
            _isDockSyncPaused = pause;
        }

        public TaskbarDockManager(Window window, AppConfig config)
        {
            _window = window;
            _config = config;

            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _pollTimer.Tick += OnPollTick;
        }

        public void Initialize()
        {
            _hwnd = new WindowInteropHelper(_window).Handle;
            ApplyWindowStyles();

            // Set up WinEvent hook for location change events
            _winEventDelegate = new NativeMethods.WinEventDelegate(OnWinEvent);
            _winEventHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero,
                _winEventDelegate,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS
            );

            _pollTimer.Start();
            UpdatePosition();
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!_isDockSyncPaused && _config.PositionMode == "TaskbarDock")
            {
                _window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(UpdatePosition));
            }
        }

        private void OnPollTick(object? sender, EventArgs e)
        {
            if (_isDisposed || _hwnd == IntPtr.Zero) return;

            // 1. Fullscreen / Game detection
            CheckFullscreenDetection();

            // 2. Ctrl+Alt hover un-click-through check
            CheckClickThroughOverride();

            // 3. Taskbar position sync if docked
            if (!_isDockSyncPaused && _config.PositionMode == "TaskbarDock")
            {
                UpdatePosition();
            }

            // 4. Ensure TopMost Z-order over taskbar
            EnforceTopMost();
        }

        private void CheckFullscreenDetection()
        {
            if (!_config.HideInFullscreen)
            {
                if (_window.Visibility != Visibility.Visible)
                {
                    _window.Visibility = Visibility.Visible;
                    FullscreenStateChanged?.Invoke(false);
                }
                return;
            }

            int hr = NativeMethods.SHQueryUserNotificationState(out var state);
            if (hr == 0) // S_OK
            {
                bool isFullscreen = state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN ||
                                    state == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY;

                if (isFullscreen && _window.Visibility == Visibility.Visible)
                {
                    _window.Visibility = Visibility.Collapsed;
                    FullscreenStateChanged?.Invoke(true);
                }
                else if (!isFullscreen && _window.Visibility != Visibility.Visible)
                {
                    _window.Visibility = Visibility.Visible;
                    FullscreenStateChanged?.Invoke(false);
                    UpdatePosition();
                }
            }
        }

        private void CheckClickThroughOverride()
        {
            if (!_config.ClickThrough)
            {
                if (_isClickThroughEffective)
                {
                    _isClickThroughEffective = false;
                    _isTemporarilyInteractive = false;
                    NativeMethods.SetWindowStyle(_hwnd, false);
                }
                return;
            }

            // When click-through is enabled, check if Ctrl+Alt is pressed while hovering over widget
            bool ctrlAlt = NativeMethods.IsCtrlAltPressed();
            NativeMethods.GetCursorPos(out var pt);
            NativeMethods.GetWindowRect(_hwnd, out var rect);

            bool isMouseOver = pt.X >= rect.Left && pt.X <= rect.Right && pt.Y >= rect.Top && pt.Y <= rect.Bottom;

            if (ctrlAlt && isMouseOver)
            {
                if (!_isTemporarilyInteractive)
                {
                    _isTemporarilyInteractive = true;
                    _isClickThroughEffective = false;
                    NativeMethods.SetWindowStyle(_hwnd, false);
                }
            }
            else
            {
                if (_isTemporarilyInteractive || !_isClickThroughEffective)
                {
                    _isTemporarilyInteractive = false;
                    _isClickThroughEffective = true;
                    NativeMethods.SetWindowStyle(_hwnd, true);
                }
            }
        }

        public void ApplyWindowStyles()
        {
            if (_hwnd == IntPtr.Zero) return;
            _isClickThroughEffective = _config.ClickThrough;
            NativeMethods.SetWindowStyle(_hwnd, _isClickThroughEffective);
        }

        public void EnforceTopMost()
        {
            if (_hwnd == IntPtr.Zero || _window.Visibility != Visibility.Visible) return;

            NativeMethods.SetWindowPos(
                _hwnd,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW
            );
        }

        public void UpdatePosition()
        {
            if (_isDisposed || _hwnd == IntPtr.Zero) return;

            if (_config.PositionMode != "TaskbarDock")
            {
                // Free floating mode: apply configured coords
                _window.Left = _config.WidgetLeft;
                _window.Top = _config.WidgetTop;
                return;
            }

            // Dock to taskbar: Find Shell_TrayWnd and TrayNotifyWnd
            IntPtr hTaskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (hTaskbar == IntPtr.Zero)
            {
                return;
            }

            if (!NativeMethods.GetWindowRect(hTaskbar, out var taskbarRect))
            {
                return;
            }

            IntPtr hTrayNotify = NativeMethods.FindWindowEx(hTaskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            NativeMethods.RECT notifyRect = taskbarRect;
            bool hasNotifyWnd = false;

            if (hTrayNotify != IntPtr.Zero && NativeMethods.GetWindowRect(hTrayNotify, out notifyRect))
            {
                hasNotifyWnd = true;
            }

            // Calculate DPI scaling factor
            double dpiFactorX = 1.0;
            double dpiFactorY = 1.0;
            var source = PresentationSource.FromVisual(_window);
            if (source?.CompositionTarget != null)
            {
                dpiFactorX = source.CompositionTarget.TransformToDevice.M11;
                dpiFactorY = source.CompositionTarget.TransformToDevice.M22;
            }

            double widgetWidthDIP = _window.ActualWidth > 0 ? _window.ActualWidth : _config.WidgetWidth;
            double widgetHeightDIP = _window.ActualHeight > 0 ? _window.ActualHeight : _config.WidgetHeight;

            double widgetWidthPx = widgetWidthDIP * dpiFactorX;
            double widgetHeightPx = widgetHeightDIP * dpiFactorY;

            // Check taskbar orientation: Horizontal (bottom or top) vs Vertical (left or right)
            bool isHorizontal = taskbarRect.Width >= taskbarRect.Height;

            double targetLeftPx;
            double targetTopPx;

            if (isHorizontal)
            {
                // Bottom or Top taskbar
                if (hasNotifyWnd)
                {
                    // Place immediately to the left of the tray notification area with 6px margin
                    targetLeftPx = notifyRect.Left - widgetWidthPx - 6;
                }
                else
                {
                    targetLeftPx = taskbarRect.Right - widgetWidthPx - 16;
                }

                // Center vertically inside taskbar
                targetTopPx = taskbarRect.Top + (taskbarRect.Height - widgetHeightPx) / 2.0;
            }
            else
            {
                // Vertical taskbar (Left or Right)
                targetLeftPx = taskbarRect.Left + (taskbarRect.Width - widgetWidthPx) / 2.0;
                if (hasNotifyWnd)
                {
                    targetTopPx = notifyRect.Top - widgetHeightPx - 6;
                }
                else
                {
                    targetTopPx = taskbarRect.Bottom - widgetHeightPx - 16;
                }
            }

            // Convert target screen pixel coordinates back to WPF DIP coordinates
            double targetLeftDIP = targetLeftPx / dpiFactorX;
            double targetTopDIP = targetTopPx / dpiFactorY;

            if (Math.Abs(_window.Left - targetLeftDIP) > 0.5 || Math.Abs(_window.Top - targetTopDIP) > 0.5)
            {
                _window.Left = targetLeftDIP;
                _window.Top = targetTopDIP;
            }
        }

        public void SnapToEdges(ref double left, ref double top, double width, double height)
        {
            const double SnapDistance = 18.0;

            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            // Snap to screen left / right
            if (Math.Abs(left) < SnapDistance) left = 0;
            if (Math.Abs((left + width) - screenW) < SnapDistance) left = screenW - width;

            // Snap to screen top / bottom
            if (Math.Abs(top) < SnapDistance) top = 0;
            if (Math.Abs((top + height) - screenH) < SnapDistance) top = screenH - height;

            // Snap to taskbar if near
            IntPtr hTaskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (hTaskbar != IntPtr.Zero && NativeMethods.GetWindowRect(hTaskbar, out var tbRect))
            {
                var source = PresentationSource.FromVisual(_window);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                double tbLeft = tbRect.Left / dpiX;
                double tbTop = tbRect.Top / dpiY;
                double tbRight = tbRect.Right / dpiX;
                double tbBottom = tbRect.Bottom / dpiY;

                // Snap above bottom taskbar
                if (tbRect.Top > 0 && Math.Abs((top + height) - tbTop) < SnapDistance)
                {
                    top = tbTop - height;
                }
                // Snap below top taskbar
                if (tbRect.Top == 0 && tbRect.Bottom < screenH && Math.Abs(top - tbBottom) < SnapDistance)
                {
                    top = tbBottom;
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _pollTimer.Stop();

            if (_winEventHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }
        }
    }
}
