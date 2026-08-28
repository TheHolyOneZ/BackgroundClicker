using System.Windows;
using System.Windows.Media;
using BackgroundClicker.Wpf.I18n;

namespace BackgroundClicker.Wpf
{
    public enum NotificationResult
    {
        None,
        Primary,
        Secondary
    }

    public partial class NotificationWindow : Window
    {
        public NotificationResult Result { get; private set; }

        public NotificationWindow(string message,
                                  bool isError = false,
                                  string primaryButtonText = null,
                                  string secondaryButtonText = null)
        {
            InitializeComponent();

            Result = NotificationResult.None;
            MessageTextBlock.Text = message;

            PrimaryButton.Content = primaryButtonText ?? Loc.T("notify.ok");

            if (string.IsNullOrEmpty(secondaryButtonText))
            {
                SecondaryButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                SecondaryButton.Visibility = Visibility.Visible;
                SecondaryButton.Content = secondaryButtonText;
            }

            if (isError)
            {
                AccentBar.Fill = (Brush)FindResource("StateErrorBrush");
            }
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            Result = NotificationResult.Primary;
            DialogResult = true;
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            Result = NotificationResult.Secondary;
            DialogResult = false;
        }
    }
}
