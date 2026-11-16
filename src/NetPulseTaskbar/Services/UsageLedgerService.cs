using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NetPulseTaskbar.Models;

namespace NetPulseTaskbar.Services
{
    public class UsageLedgerData
    {
        public Dictionary<string, UsageRecord> DailyRecords { get; set; } = new();
    }

    public class UsageLedgerService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private readonly string _ledgerDirectory;
        private readonly string _ledgerFilePath;
        private readonly UsageLedgerData _data = new();
        private readonly object _lock = new();

        public DateTime SessionStartTime { get; private set; } = DateTime.UtcNow;
        public long SessionDownloadBytes { get; private set; }
        public long SessionUploadBytes { get; private set; }
        public long SessionTotalBytes => SessionDownloadBytes + SessionUploadBytes;

        private bool _capAlertFiredToday = false;
        private string _lastAlertDate = "";

        public event Action<double, double>? DataCapExceeded;

        public UsageLedgerService()
        {
            _ledgerDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NetPulseTaskbar"
            );
            _ledgerFilePath = Path.Combine(_ledgerDirectory, "usage_ledger.json");

            Load();
        }

        public void RecordBytes(long downloadDelta, long uploadDelta, double dailyCapGb)
        {
            if (downloadDelta <= 0 && uploadDelta <= 0) return;

            string todayKey = DateTime.Now.ToString("yyyy-MM-dd");

            lock (_lock)
            {
                SessionDownloadBytes += downloadDelta;
                SessionUploadBytes += uploadDelta;

                if (!_data.DailyRecords.TryGetValue(todayKey, out var record))
                {
                    record = new UsageRecord { DateKey = todayKey };
                    _data.DailyRecords[todayKey] = record;
                }

                record.DownloadBytes += downloadDelta;
                record.UploadBytes += uploadDelta;

                // Check Data Cap
                if (dailyCapGb > 0)
                {
                    if (_lastAlertDate != todayKey)
                    {
                        _lastAlertDate = todayKey;
                        _capAlertFiredToday = false;
                    }

                    double totalGb = record.TotalBytes / (1024.0 * 1024.0 * 1024.0);
                    if (!_capAlertFiredToday && totalGb >= dailyCapGb)
                    {
                        _capAlertFiredToday = true;
                        DataCapExceeded?.Invoke(totalGb, dailyCapGb);
                    }
                }
            }
        }

        public void ResetSession()
        {
            lock (_lock)
            {
                SessionStartTime = DateTime.UtcNow;
                SessionDownloadBytes = 0;
                SessionUploadBytes = 0;
            }
        }

        public UsageRecord GetTodayUsage()
        {
            string todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            lock (_lock)
            {
                if (_data.DailyRecords.TryGetValue(todayKey, out var record))
                {
                    return new UsageRecord
                    {
                        DateKey = record.DateKey,
                        DownloadBytes = record.DownloadBytes,
                        UploadBytes = record.UploadBytes
                    };
                }
            }
            return new UsageRecord { DateKey = todayKey };
        }

        public List<UsageRecord> GetHistoryList()
        {
            lock (_lock)
            {
                return _data.DailyRecords.Values
                    .OrderByDescending(r => r.DateKey)
                    .Select(r => new UsageRecord
                    {
                        DateKey = r.DateKey,
                        DownloadBytes = r.DownloadBytes,
                        UploadBytes = r.UploadBytes
                    })
                    .ToList();
            }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(_ledgerDirectory))
                {
                    Directory.CreateDirectory(_ledgerDirectory);
                }

                lock (_lock)
                {
                    string json = JsonSerializer.Serialize(_data, JsonOptions);
                    File.WriteAllText(_ledgerFilePath, json);
                }
            }
            catch
            {
                // Silently handle disk flush errors
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_ledgerFilePath))
                {
                    string json = File.ReadAllText(_ledgerFilePath);
                    var loaded = JsonSerializer.Deserialize<UsageLedgerData>(json, JsonOptions);
                    if (loaded?.DailyRecords != null)
                    {
                        lock (_lock)
                        {
                            _data.DailyRecords.Clear();
                            foreach (var kvp in loaded.DailyRecords)
                            {
                                _data.DailyRecords[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Keep empty records on corrupt file
            }
        }

        public void ExportToCsv(string targetPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Date,Download Bytes,Upload Bytes,Total Bytes,Formatted Download,Formatted Upload,Formatted Total");

            lock (_lock)
            {
                foreach (var record in _data.DailyRecords.Values.OrderByDescending(r => r.DateKey))
                {
                    sb.AppendLine($"\"{record.DateKey}\",{record.DownloadBytes},{record.UploadBytes},{record.TotalBytes},\"{record.FormattedDownload}\",\"{record.FormattedUpload}\",\"{record.FormattedTotal}\"");
                }
            }

            File.WriteAllText(targetPath, sb.ToString(), Encoding.UTF8);
        }
    }
}
