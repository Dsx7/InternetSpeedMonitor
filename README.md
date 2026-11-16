# NetPulse Taskbar: Production-Grade Windows Network Speed Monitor

**NetPulse Taskbar** is a lightweight, high-performance, production-grade Windows desktop application that renders real-time download and upload speeds directly inside or overlaid on the Windows 10 and Windows 11 taskbar. It compiles into a standalone, single-file `.exe` with zero external runtime prerequisites.

---

## 🌟 Key Features

1. **Native Taskbar Docking & Floating Overlays:**
   - Automatically detects the Windows Taskbar (`Shell_TrayWnd`), system tray notification area (`TrayNotifyWnd`), and running task list (`MSTaskListWClass`).
   - Docks smoothly immediately to the left of the system tray clock and icons, dynamically adjusting to icon expansions, DPI scaling, and screen resolution changes.
   - Supports **Free-Floating Mode** with magnetic snapping to desktop and taskbar edges.
   - **Click-Through Mode:** Applies `WS_EX_TRANSPARENT`. Temporarily interactable by holding `Ctrl + Alt` while hovering.
   - **Fullscreen / Game Detection:** Uses `SHQueryUserNotificationState` to detect D3D fullscreen games and presentations (`QUNS_RUNNING_D3D_FULL_SCREEN`, `QUNS_BUSY`), automatically hiding the widget to prevent distraction or interference.
   - Clean Z-order management: `WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE` guarantees the widget never steals focus or clutters Alt+Tab.

2. **High-Precision Network Telemetry:**
   - Uses `Stopwatch.GetTimestamp()` (`QueryPerformanceCounter`) to compute exact fractional-second bandwidth rates.
   - Aggregates all active network interfaces or binds to a user-pinned physical/virtual adapter GUID.
   - Exponential Moving Average (EMA) smoothing with configurable alpha factor.
   - Asynchronous real-time ICMP ping and jitter tracking to a configurable host (default `1.1.1.1`).
   - Disconnect and reconnect detection with balloon notifications and optional audio alerts.

3. **Universal Unit Formatting:**
   - Supports 9 unit modes: `Auto (Bytes/s)`, `Auto (Bits/s)`, `mbps`, `Mbps`, `MBPS`, `MBps`, `KB/s`, `MB/s`, `kbps`.
   - Adaptive decimal precision (`Auto`, `0`, `1`, `2`).
   - Custom configurable prefixes (`↓ `, `↑ `, `D: `, `U: `, or hidden).
   - Fixed-width numeric padding for jitter-free visual stability.

4. **Rich Visual Styling & Typography:**
   - OpenType tabular numerals (`Typography.NumeralAlignment="Tabular"`) ensuring proportional fonts don't wiggle when numbers change.
   - Full support for all installed system fonts (e.g., *Segoe UI Variable Display*, *Consolas*, *Aptos*).
   - Dynamic Bandwidth Saturation Coloring (Low/Moderate/High thresholds override colors in real time).
   - Live mini background sparkline rendered directly inside the taskbar pill.

5. **Hover Flyout HUD (Acrylic Telemetry Card):**
   - Appears when hovering over the widget for 400ms.
   - Dual-series 60-second rolling speed graph.
   - Active network adapter details (IPv4, Gateway, MAC, Link Speed).
   - Live latency and rolling jitter badge.
   - Session & today's cumulative usage counters.

6. **Interactive Dashboard & Multi-Tab Settings:**
   - Detailed dashboard window with large gauges, peak speeds, live history chart, and quick pause/reset controls.
   - 4-tab searchable settings panel updating the taskbar widget live in real time.
   - 4 built-in theme presets (*Windows 11 Native*, *Cyberpunk Neon*, *Minimal Monochrome*, *High-Contrast Pro*), with JSON import/export.
   - Daily and monthly data usage breakdown with CSV export and daily data cap warnings.

7. **System Tray Controller:**
   - Full context menu for quick unit switching, mode switching, position locking, click-through toggles, and autostart configuration (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).

---

## 📂 Project Architecture

```
Internet_Speed_Monitor/
├── NetPulseTaskbar.sln             # Visual Studio / dotnet Solution
├── build.bat                       # One-click build script (produces standalone .exe)
├── run.bat                         # One-click launch script
├── README.md                       # Project documentation
├── publish/
│   └── NetPulseTaskbar.exe         # Standalone Single-File Executable (~89 MB)
└── src/
    └── NetPulseTaskbar/
        ├── NetPulseTaskbar.csproj  # Target net8.0-windows, SingleFile, ReadyToRun
        ├── app.manifest            # PerMonitorV2 High-DPI and Windows 10/11 compatibility
        ├── App.xaml                # Application definition & resource styles
        ├── App.xaml.cs             # Single-instance Mutex, exception handling, lifecycle
        ├── Models/
        │   ├── AppConfig.cs        # Comprehensive INotifyPropertyChanged configuration
        │   ├── AdapterInfo.cs      # Network interface metadata model
        │   ├── NetworkSpeedSample.cs # Timestamped bandwidth & ping sample
        │   └── UsageRecord.cs      # Daily and session data accounting
        ├── Native/
        │   ├── NativeMethods.cs    # P/Invoke definitions (user32, shell32, iphlpapi, dwmapi)
        │   └── TaskbarDockManager.cs # Taskbar positioning, Z-order, snapping, fullscreen detection
        ├── Services/
        │   ├── NetworkTelemetryService.cs # High-precision counter polling, EMA, ping engine
        │   ├── SpeedFormatter.cs   # Unit scaling, decimal precision, prefixing
        │   ├── ConfigService.cs    # JSON persistence (%AppData%\NetPulseTaskbar\config.json)
        │   ├── UsageLedgerService.cs # Data usage tracking, cap alerts, CSV export
        │   ├── AutoStartManager.cs # Windows Run registry key management
        │   └── SoundAlertService.cs# Alert beeps and audio notifications
        ├── Controls/
        │   └── SparklineCanvas.cs  # High-performance WPF DrawingVisual dual chart
        └── Views/
            ├── WidgetWindow.xaml   # Main Taskbar overlay / floating pill
            ├── WidgetWindow.xaml.cs
            ├── TelemetryFlyoutWindow.xaml # 400ms hover acrylic HUD
            ├── TelemetryFlyoutWindow.xaml.cs
            ├── DashboardWindow.xaml # Full desktop telemetry cockpit
            ├── DashboardWindow.xaml.cs
            ├── SettingsWindow.xaml  # 4-tab searchable customization interface
            ├── SettingsWindow.xaml.cs
            └── TrayIconManager.cs   # System tray icon, notifications, context menu
```

---

## 🚀 Building & Running

### Requirements
- Windows 10 (version 1903+) or Windows 11 (64-bit).
- .NET 8.0 SDK (installed in PATH or `$HOME\.dotnet`).

### Quick Build
Double-click `build.bat` or run:
```powershell
.\build.bat
```

### Manual CLI Build Commands

1. **Standard Debug / Release Build:**
   ```powershell
   dotnet build NetPulseTaskbar.sln -c Release
   ```

2. **Compile Standalone Single-File Executable (`NetPulseTaskbar.exe`):**
   ```powershell
   dotnet publish src\NetPulseTaskbar\NetPulseTaskbar.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -o publish
   ```

The compiled standalone executable will be located at:
`publish\NetPulseTaskbar.exe`

---

## 🎮 Mouse & Keyboard Controls

| Action | Control |
| :--- | :--- |
| **Move Widget** | Left-click and drag widget (in Free-Floating mode or when unlocked). Snaps to taskbar and screen edges. |
| **Telemetry HUD** | Hover mouse over the taskbar widget for 400ms. |
| **Open Dashboard** | Double-click the taskbar widget OR click the System Tray icon. |
| **Context Menu** | Right-click the widget OR right-click the System Tray icon. |
| **Click-Through Interaction**| When *Click-Through Mode* is enabled, hold `Ctrl + Alt` while moving the mouse over the widget to temporarily interact. |

---

## ⚙️ Persistent Configuration

All settings are auto-saved to:
`%AppData%\NetPulseTaskbar\config.json`

Data usage history is persisted to:
`%AppData%\NetPulseTaskbar\usage_ledger.json`
