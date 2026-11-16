using System;

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
