using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace NetPulseTaskbar.Views
{
    public partial class AboutWindow : Window
    {
        private const string RepositoryUrl = "https://github.com/Dsx7/InternetSpeedMonitor";

        public AboutWindow()
        {
            InitializeComponent();

            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null)
                {
                    TxtVersion.Text = $"Version {version.Major}.{version.Minor}.{version.Build} (Production Release)";
                }
            }
            catch { }
        }

        private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void OnViewOnGitHubClick(object sender, RoutedEventArgs e)
        {
            OpenUrl(RepositoryUrl);
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser: {ex.Message}", "NetPulse", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
