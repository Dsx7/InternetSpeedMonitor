using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using NetPulseTaskbar.Models;

namespace NetPulseTaskbar.Services
{
    public class NetworkTelemetryService : IDisposable
    {
        private readonly AppConfig _config;
        private readonly UsageLedgerService _usageLedger;
        private readonly DispatcherTimer _pollTimer;
        private readonly Ping _pinger = new();

        private long _previousTimestamp;
        private long _previousTotalBytesReceived;
        private long _previousTotalBytesSent;

        private double _smoothedDownloadBps;
        private double _smoothedUploadBps;

        private double _currentPingMs;
        private double _previousPingMs;
        private double _currentJitterMs;
        private DateTime _lastPingTime = DateTime.MinValue;
        private bool _isPinging = false;

        private bool _wasConnected = true;
        private readonly List<NetworkSpeedSample> _history = new();
        private readonly object _historyLock = new();

        public AdapterInfo CurrentAdapter { get; private set; } = new();
        public double CurrentDownloadBps { get; private set; }
        public double CurrentUploadBps { get; private set; }
        public double CurrentPingMs => _currentPingMs;
        public double CurrentJitterMs => _currentJitterMs;
        public bool IsConnected => _wasConnected;

        public event Action<NetworkSpeedSample>? TelemetryUpdated;
        public event Action<bool, string>? ConnectionStateChanged;

        public NetworkTelemetryService(AppConfig config, UsageLedgerService usageLedger)
        {
            _config = config;
            _usageLedger = usageLedger;

            _pollTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(config.PollingIntervalMs)
            };
            _pollTimer.Tick += OnPollTick;

            _config.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppConfig.PollingIntervalMs))
                {
                    _pollTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(100, _config.PollingIntervalMs));
                }
            };
        }

        public void Start()
        {
            // Reset baselines
            _previousTimestamp = Stopwatch.GetTimestamp();
            GetAdapterCounters(out _previousTotalBytesReceived, out _previousTotalBytesSent);
            _smoothedDownloadBps = 0;
            _smoothedUploadBps = 0;

            _pollTimer.Start();
        }

        public void Stop()
        {
            _pollTimer.Stop();
        }

        public List<AdapterInfo> GetAvailableAdapters()
        {
            var list = new List<AdapterInfo>();
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in interfaces)
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    var info = BuildAdapterInfo(nic);
                    list.Add(info);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enumerating adapters: {ex.Message}");
            }
            return list;
        }

        private AdapterInfo BuildAdapterInfo(NetworkInterface nic)
        {
            var info = new AdapterInfo
            {
                Id = nic.Id,
                Name = nic.Name,
                Description = nic.Description,
                InterfaceType = nic.NetworkInterfaceType,
                Status = nic.OperationalStatus,
                SpeedBitsPerSec = nic.Speed > 0 ? nic.Speed : 0
            };

            // Format speed
            if (nic.Speed >= 1_000_000_000)
                info.FormattedSpeed = $"{nic.Speed / 1_000_000_000.0:0.##} Gbps";
            else if (nic.Speed >= 1_000_000)
                info.FormattedSpeed = $"{nic.Speed / 1_000_000.0:0.##} Mbps";
            else if (nic.Speed > 0)
                info.FormattedSpeed = $"{nic.Speed / 1000.0:0.##} kbps";
            else
                info.FormattedSpeed = "Unknown";

            // Format MAC address
            try
            {
                var physicalAddress = nic.GetPhysicalAddress();
                var bytes = physicalAddress.GetAddressBytes();
                if (bytes.Length > 0)
                {
                    info.MacAddress = string.Join(":", bytes.Select(b => b.ToString("X2")));
                }
            }
            catch { }

            // IP & Gateway & DNS properties
            try
            {
                var ipProps = nic.GetIPProperties();
                var ipv4 = ipProps.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4 != null) info.IPv4Address = ipv4.Address.ToString();

                var ipv6 = ipProps.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6);
                if (ipv6 != null) info.IPv6Address = ipv6.Address.ToString();

                var gw = ipProps.GatewayAddresses.FirstOrDefault();
                if (gw != null && gw.Address != null) info.GatewayAddress = gw.Address.ToString();

                var dns = ipProps.DnsAddresses.Where(d => d.AddressFamily == AddressFamily.InterNetwork).Select(d => d.ToString());
                info.DnsServers = string.Join(", ", dns);
            }
            catch { }

            return info;
        }

        private void OnPollTick(object? sender, EventArgs e)
        {
            if (_config.PauseMonitoring) return;

            long currentTimestamp = Stopwatch.GetTimestamp();
            long elapsedTicks = currentTimestamp - _previousTimestamp;
            double elapsedSeconds = (double)elapsedTicks / Stopwatch.Frequency;

            if (elapsedSeconds <= 0.0001) return;

            GetAdapterCounters(out long currentBytesReceived, out long currentBytesSent);

            long deltaReceived = currentBytesReceived - _previousTotalBytesReceived;
            long deltaSent = currentBytesSent - _previousTotalBytesSent;

            // Guard against counter wrap-around or adapter reset
            if (deltaReceived < 0) deltaReceived = 0;
            if (deltaSent < 0) deltaSent = 0;

            _previousTimestamp = currentTimestamp;
            _previousTotalBytesReceived = currentBytesReceived;
            _previousTotalBytesSent = currentBytesSent;

            double rawDownloadBps = deltaReceived / elapsedSeconds;
            double rawUploadBps = deltaSent / elapsedSeconds;

            if (_config.EnableSmoothing)
            {
                double alpha = Math.Clamp(_config.SmoothingFactor, 0.05, 0.95);
                _smoothedDownloadBps = (alpha * rawDownloadBps) + ((1.0 - alpha) * _smoothedDownloadBps);
                _smoothedUploadBps = (alpha * rawUploadBps) + ((1.0 - alpha) * _smoothedUploadBps);
                CurrentDownloadBps = _smoothedDownloadBps;
                CurrentUploadBps = _smoothedUploadBps;
            }
            else
            {
                CurrentDownloadBps = rawDownloadBps;
                CurrentUploadBps = rawUploadBps;
            }

            // Persist to usage ledger
            _usageLedger.RecordBytes(deltaReceived, deltaSent, _config.DailyDataCapGb);

            // Execute Ping asynchronously every 2 seconds if enabled
            if (_config.EnablePing && (DateTime.UtcNow - _lastPingTime).TotalSeconds >= 2.0)
            {
                _lastPingTime = DateTime.UtcNow;
                _ = TriggerPingAsync(_config.PingHost);
            }

            var sample = new NetworkSpeedSample
            {
                Timestamp = DateTime.UtcNow,
                DownloadBytesPerSec = CurrentDownloadBps,
                UploadBytesPerSec = CurrentUploadBps,
                PingMs = _currentPingMs,
                JitterMs = _currentJitterMs
            };

            lock (_historyLock)
            {
                _history.Add(sample);
                // Retain up to 120 seconds of samples
                int maxSamples = Math.Max(60, 120000 / Math.Max(100, _config.PollingIntervalMs));
                while (_history.Count > maxSamples)
                {
                    _history.RemoveAt(0);
                }
            }

            TelemetryUpdated?.Invoke(sample);
        }

        private void GetAdapterCounters(out long totalReceived, out long totalSent)
        {
            totalReceived = 0;
            totalSent = 0;

            bool isAnyUp = false;
            AdapterInfo? primaryInfo = null;

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();

                if (_config.SelectedAdapterId == "Auto")
                {
                    foreach (var nic in interfaces)
                    {
                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                            nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                        {
                            continue;
                        }

                        if (nic.OperationalStatus == OperationalStatus.Up)
                        {
                            isAnyUp = true;
                            var stats = nic.GetIPStatistics();
                            totalReceived += stats.BytesReceived;
                            totalSent += stats.BytesSent;

                            if (primaryInfo == null)
                            {
                                primaryInfo = BuildAdapterInfo(nic);
                            }
                        }
                    }
                }
                else
                {
                    var nic = interfaces.FirstOrDefault(i => i.Id == _config.SelectedAdapterId);
                    if (nic != null && nic.OperationalStatus == OperationalStatus.Up)
                    {
                        isAnyUp = true;
                        var stats = nic.GetIPStatistics();
                        totalReceived = stats.BytesReceived;
                        totalSent = stats.BytesSent;
                        primaryInfo = BuildAdapterInfo(nic);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error querying network statistics: {ex.Message}");
            }

            if (primaryInfo != null)
            {
                CurrentAdapter = primaryInfo;
            }

            // Connection state change detection
            if (isAnyUp != _wasConnected)
            {
                _wasConnected = isAnyUp;
                string status = isAnyUp ? $"Connected ({CurrentAdapter.Name})" : "Disconnected (No Active Network)";
                ConnectionStateChanged?.Invoke(isAnyUp, status);
            }
        }

        private async Task TriggerPingAsync(string host)
        {
            if (_isPinging || string.IsNullOrWhiteSpace(host)) return;
            _isPinging = true;

            try
            {
                var reply = await _pinger.SendPingAsync(host, 1200);
                if (reply.Status == IPStatus.Success)
                {
                    double rtt = reply.RoundtripTime;
                    _currentJitterMs = _previousPingMs > 0 ? Math.Abs(rtt - _previousPingMs) : 0;
                    _previousPingMs = _currentPingMs;
                    _currentPingMs = rtt;
                }
                else
                {
                    _currentPingMs = 0;
                    _currentJitterMs = 0;
                }
            }
            catch
            {
                _currentPingMs = 0;
                _currentJitterMs = 0;
            }
            finally
            {
                _isPinging = false;
            }
        }

        public List<NetworkSpeedSample> GetHistory()
        {
            lock (_historyLock)
            {
                return new List<NetworkSpeedSample>(_history);
            }
        }

        public void Dispose()
        {
            _pollTimer.Stop();
            _pinger.Dispose();
        }
    }
}
