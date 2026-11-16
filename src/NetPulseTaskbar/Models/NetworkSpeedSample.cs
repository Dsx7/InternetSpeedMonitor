using System;

namespace NetPulseTaskbar.Models
{
    public class NetworkSpeedSample
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double DownloadBytesPerSec { get; set; }
        public double UploadBytesPerSec { get; set; }
        public double PingMs { get; set; }
        public double JitterMs { get; set; }

        public double DownloadBitsPerSec => DownloadBytesPerSec * 8.0;
        public double UploadBitsPerSec => UploadBytesPerSec * 8.0;
    }
}
