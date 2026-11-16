using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using NetPulseTaskbar.Native;
using NetPulseTaskbar.Services;
using NetPulseTaskbar.Views;

namespace NetPulseTaskbar
{
    public partial class App : System.Windows.Application
    {
        private const string MutexName = "Global\\NetPulseTaskbar_SingleInstance_Mutex";
        private Mutex? _singleInstanceMutex;

        public ConfigService ConfigService { get; private set; } = null!;
        public UsageLedgerService UsageLedger { get; private set; } = null!;
        public NetworkTelemetryService Telemetry { get; private set; } = null!;

        private WidgetWindow? _widgetWindow;
        private TrayIconManager? _trayIconManager;
        private DashboardWindow? _dashboardWindow;
        private SettingsWindow? _settingsWindow;
        private AboutWindow? _aboutWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Single Instance Check
            _singleInstanceMutex = new Mutex(true, MutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                // Another instance is already running; alert and exit
                MessageBox.Show("NetPulse is already running in the system tray / taskbar.", "NetPulse Taskbar", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // 2. Global Exception Handling
            DispatcherUnhandledException += (s, args) =>
            {
                Debug.WriteLine($"[NetPulse Dispatcher Exception] {args.Exception}");
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Debug.WriteLine($"[NetPulse Domain Exception] {args.ExceptionObject}");
            };

            // 3. Initialize Services
            ConfigService = new ConfigService();
            UsageLedger = new UsageLedgerService();
            Telemetry = new NetworkTelemetryService(ConfigService.Current, UsageLedger);

            // 4. Initialize Windows & Views
            _widgetWindow = new WidgetWindow(ConfigService.Current, Telemetry);
            _widgetWindow.RequestOpenDashboard += ShowDashboard;
            _widgetWindow.RequestOpenSettings += ShowSettings;
            _widgetWindow.RequestOpenAbout += ShowAbout;
            _widgetWindow.RequestExit += ExitApplication;

            _trayIconManager = new TrayIconManager(ConfigService.Current, Telemetry, UsageLedger);
            _trayIconManager.OpenDashboardRequested += ShowDashboard;
            _trayIconManager.OpenSettingsRequested += ShowSettings;
            _trayIconManager.OpenAboutRequested += ShowAbout;
            _trayIconManager.ExitRequested += ExitApplication;

            // 5. Start Telemetry Engine
            Telemetry.Start();

            // 6. Show Widget Window
            _widgetWindow.Show();
        }

        public void ShowDashboard()
        {
            if (_dashboardWindow == null || !_dashboardWindow.IsLoaded)
            {
                _dashboardWindow = new DashboardWindow(Telemetry, ConfigService.Current, UsageLedger);
                _dashboardWindow.RequestOpenSettings += ShowSettings;
                _dashboardWindow.RequestOpenAbout += ShowAbout;
                _dashboardWindow.Closed += (s, e) => _dashboardWindow = null;
            }

            _dashboardWindow.Show();
            if (_dashboardWindow.WindowState == WindowState.Minimized)
            {
                _dashboardWindow.WindowState = WindowState.Normal;
            }
            _dashboardWindow.Activate();
        }

        public void ShowSettings()
        {
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow(ConfigService, Telemetry, UsageLedger);
                _settingsWindow.RequestOpenAbout += ShowAbout;
                _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            }

            _settingsWindow.Show();
            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }
            _settingsWindow.Activate();
        }

        public void ShowAbout()
        {
            if (_aboutWindow == null || !_aboutWindow.IsLoaded)
            {
                _aboutWindow = new AboutWindow();
                _aboutWindow.Closed += (s, e) => _aboutWindow = null;
            }

            _aboutWindow.Show();
            if (_aboutWindow.WindowState == WindowState.Minimized)
            {
                _aboutWindow.WindowState = WindowState.Normal;
            }
            _aboutWindow.Activate();
        }

        public void ExitApplication()
        {
            Telemetry.Stop();
            Telemetry.Dispose();

            ConfigService.Save();
            UsageLedger.Save();

            _trayIconManager?.Dispose();
            _widgetWindow?.Close();
            _dashboardWindow?.Close();
            _settingsWindow?.Close();

            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();

            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                ConfigService?.Save();
                UsageLedger?.Save();
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
            }
            catch { }

            base.OnExit(e);
        }
    }
}
