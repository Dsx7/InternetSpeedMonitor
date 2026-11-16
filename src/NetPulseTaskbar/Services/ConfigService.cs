using System;
using System.IO;
using System.Text.Json;
using NetPulseTaskbar.Models;

namespace NetPulseTaskbar.Services
{
    public class ConfigService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _configDirectory;
        private readonly string _configFilePath;

        public AppConfig Current { get; private set; }

        public event Action<AppConfig>? ConfigChanged;

        public ConfigService()
        {
            _configDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NetPulseTaskbar"
            );
            _configFilePath = Path.Combine(_configDirectory, "config.json");

            Current = Load();
        }

        public AppConfig Load()
        {
            try
            {
                if (!Directory.Exists(_configDirectory))
                {
                    Directory.CreateDirectory(_configDirectory);
                }

                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (config != null)
                    {
                        Current = config;
                        return Current;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to defaults on read error
            }

            Current = new AppConfig();
            Save();
            return Current;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(_configDirectory))
                {
                    Directory.CreateDirectory(_configDirectory);
                }

                string json = JsonSerializer.Serialize(Current, JsonOptions);
                File.WriteAllText(_configFilePath, json);
                ConfigChanged?.Invoke(Current);
            }
            catch (Exception)
            {
                // Ignore disk write errors gracefully
            }
        }

        public void ApplyThemePreset(string themeName)
        {
            Current.ThemePreset = themeName;

            switch (themeName)
            {
                case "Cyberpunk Neon":
                    Current.DownloadColorHex = "#00FF66";
                    Current.UploadColorHex = "#FF007F";
                    Current.PrefixColorHex = "#FFE600";
                    Current.BackgroundColorHex = "#F00A0A14";
                    Current.BorderColorHex = "#8000FFCC";
                    Current.BorderThickness = 1.5;
                    Current.CornerRadius = 4.0;
                    Current.FontFamily = "Consolas";
                    break;

                case "Minimal Monochrome":
                    Current.DownloadColorHex = "#FFFFFF";
                    Current.UploadColorHex = "#CBD5E1";
                    Current.PrefixColorHex = "#64748B";
                    Current.BackgroundColorHex = "#C0000000";
                    Current.BorderColorHex = "#20FFFFFF";
                    Current.BorderThickness = 0.5;
                    Current.CornerRadius = 8.0;
                    Current.FontFamily = "Segoe UI Variable Display";
                    break;

                case "High-Contrast Pro":
                    Current.DownloadColorHex = "#00FF00";
                    Current.UploadColorHex = "#FFFF00";
                    Current.PrefixColorHex = "#00FFFF";
                    Current.BackgroundColorHex = "#FF000000";
                    Current.BorderColorHex = "#FFFFFFFF";
                    Current.BorderThickness = 2.0;
                    Current.CornerRadius = 0.0;
                    Current.FontFamily = "Segoe UI";
                    break;

                case "Windows 11 Native":
                default:
                    Current.DownloadColorHex = "#00E676";
                    Current.UploadColorHex = "#38BDF8";
                    Current.PrefixColorHex = "#94A3B8";
                    Current.BackgroundColorHex = "#E6111827";
                    Current.BorderColorHex = "#33475569";
                    Current.BorderThickness = 1.0;
                    Current.CornerRadius = 6.0;
                    Current.FontFamily = "Segoe UI Variable Display";
                    break;
            }

            Save();
        }

        public void ExportToFile(string filePath)
        {
            string json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        public bool ImportFromFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var imported = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (imported != null)
                    {
                        Current = imported;
                        Save();
                        return true;
                    }
                }
            }
            catch
            {
                // Format error
            }
            return false;
        }
    }
}
