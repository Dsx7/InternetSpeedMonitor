using System;
using System.Globalization;
using NetPulseTaskbar.Models;

namespace NetPulseTaskbar.Services
{
    public class FormattedSpeedResult
    {
        public string ValueString { get; set; } = "0";
        public string UnitString { get; set; } = "KB/s";
        public string FullDisplayString { get; set; } = "0 KB/s";
        public double RawBytesPerSec { get; set; }
    }

    public static class SpeedFormatter
    {
        public static FormattedSpeedResult FormatSpeed(double bytesPerSecond, AppConfig config, bool isUpload)
        {
            if (bytesPerSecond < 0) bytesPerSecond = 0;

            double value = 0;
            string unit = "";

            string mode = config.UnitMode ?? "AutoBytes";

            switch (mode)
            {
                case "AutoBits":
                case "Auto (Bits/s)":
                    {
                        double bits = bytesPerSecond * 8.0;
                        if (bits < 1000.0)
                        {
                            value = bits;
                            unit = "bps";
                        }
                        else if (bits < 1_000_000.0)
                        {
                            value = bits / 1000.0;
                            unit = "kbps";
                        }
                        else if (bits < 1_000_000_000.0)
                        {
                            value = bits / 1_000_000.0;
                            unit = "Mbps";
                        }
                        else
                        {
                            value = bits / 1_000_000_000.0;
                            unit = "Gbps";
                        }
                    }
                    break;

                case "kbps":
                    value = (bytesPerSecond * 8.0) / 1000.0;
                    unit = "kbps";
                    break;

                case "mbps":
                    value = (bytesPerSecond * 8.0) / 1_000_000.0;
                    unit = "mbps";
                    break;

                case "Mbps":
                    value = (bytesPerSecond * 8.0) / 1_000_000.0;
                    unit = "Mbps";
                    break;

                case "MBPS":
                    value = (bytesPerSecond * 8.0) / 1_000_000.0;
                    unit = "MBPS";
                    break;

                case "MBps":
                    value = bytesPerSecond / 1_048_576.0;
                    unit = "MBps";
                    break;

                case "KB/s":
                    value = bytesPerSecond / 1024.0;
                    unit = "KB/s";
                    break;

                case "MB/s":
                    value = bytesPerSecond / 1_048_576.0;
                    unit = "MB/s";
                    break;

                case "AutoBytes":
                case "Auto (Bytes/s)":
                default:
                    if (bytesPerSecond < 1024.0)
                    {
                        value = bytesPerSecond;
                        unit = "B/s";
                    }
                    else if (bytesPerSecond < 1024.0 * 1024.0)
                    {
                        value = bytesPerSecond / 1024.0;
                        unit = "KB/s";
                    }
                    else if (bytesPerSecond < 1024.0 * 1024.0 * 1024.0)
                    {
                        value = bytesPerSecond / (1024.0 * 1024.0);
                        unit = "MB/s";
                    }
                    else
                    {
                        value = bytesPerSecond / (1024.0 * 1024.0 * 1024.0);
                        unit = "GB/s";
                    }
                    break;
            }

            // Decimal precision formatting
            string formatPattern;
            string precisionMode = config.DecimalPrecisionMode ?? "Auto";

            switch (precisionMode)
            {
                case "0":
                    formatPattern = "0";
                    break;
                case "1":
                    formatPattern = "0.0";
                    break;
                case "2":
                    formatPattern = "0.00";
                    break;
                case "Auto":
                default:
                    if (value >= 100.0)
                    {
                        formatPattern = "0";
                    }
                    else if (value >= 10.0)
                    {
                        formatPattern = "0.0";
                    }
                    else
                    {
                        formatPattern = "0.0";
                    }
                    break;
            }

            string formattedValue = value.ToString(formatPattern, CultureInfo.InvariantCulture);

            if (config.FixedWidthPadding)
            {
                formattedValue = formattedValue.PadLeft(5, ' ');
            }

            string prefix = isUpload ? (config.UploadPrefix ?? "") : (config.DownloadPrefix ?? "");
            string space = config.CompactSpaceless ? "" : " ";

            string fullDisplay;
            if (config.HideUnitSuffix)
            {
                fullDisplay = $"{prefix}{formattedValue}";
            }
            else
            {
                fullDisplay = $"{prefix}{formattedValue}{space}{unit}";
            }

            return new FormattedSpeedResult
            {
                ValueString = formattedValue,
                UnitString = unit,
                FullDisplayString = fullDisplay,
                RawBytesPerSec = bytesPerSecond
            };
        }
    }
}
