using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using BackgroundClicker.Wpf.I18n;

namespace BackgroundClicker.Wpf
{
    public partial class CreditsWindow : Window
    {
        public CreditsWindow()
        {
            InitializeComponent();
            GitHubLink.MouseLeftButtonUp += (s, e) => Process.Start("https://github.com/TheHolyOneZ");

            ApplyVersion();
            Loc.LanguageChanged += OnLanguageChanged;
            Closed += (s, e) => Loc.LanguageChanged -= OnLanguageChanged;
        }

        private void ApplyVersion()
        {
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            string number = string.Format("{0}.{1}.{2}", v.Major, v.Minor, v.Build);
            VersionTextBlock.Text = Loc.T("credits.version", number);
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            ApplyVersion();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}
