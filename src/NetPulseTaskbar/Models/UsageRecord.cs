using System;
using System.Globalization;

namespace NetPulseTaskbar.Models
{
    public class UsageRecord
    {
        public string DateKey { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
        public long DownloadBytes { get; set; }
        public long UploadBytes { get; set; }
        public long TotalBytes => DownloadBytes + UploadBytes;

        public string FormattedDownload => FormatBytes(DownloadBytes);
        public string FormattedUpload => FormatBytes(UploadBytes);
        public string FormattedTotal => FormatBytes(TotalBytes);

        public string FriendlyDateString
        {
            get
            {
                if (DateTime.TryParseExact(DateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    if (dt.Date == DateTime.Today) return $"Today ({dt:dd MMM yyyy})";
                    if (dt.Date == DateTime.Today.AddDays(-1)) return $"Yesterday ({dt:dd MMM yyyy})";
                    return dt.ToString("dd MMM yyyy");
                }
                return DateKey;
            }
        }

        public string DayOfWeekName
        {
            get
            {
                if (DateTime.TryParseExact(DateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return dt.ToString("dddd");
                }
                return "";
            }
        }

        public double DownloadPercentOfTotal
        {
            get
            {
                if (TotalBytes <= 0) return 0;
                return (DownloadBytes / (double)TotalBytes) * 100.0;
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) bytes = 0;
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double val = bytes;
            int order = 0;
            while (val >= 1024.0 && order < units.Length - 1)
            {
                order++;
                val /= 1024.0;
            }
            return $"{val:0.##} {units[order]}";
        }
    }
}
