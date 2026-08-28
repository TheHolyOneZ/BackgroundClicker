using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using BackgroundClicker.Wpf.I18n;

namespace BackgroundClicker.Wpf
{
    public partial class EditStepWindow : Window
    {
        private readonly MainWindow.SequenceStep _step;

        public EditStepWindow(MainWindow.SequenceStep stepToEdit)
        {
            InitializeComponent();
            _step = stepToEdit;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_step == null)
            {
                Close();
                return;
            }

            WindowTitleTextBox.Text = _step.WindowTitle ?? string.Empty;
            XCoordTextBox.Text = _step.X.ToString(CultureInfo.InvariantCulture);
            YCoordTextBox.Text = _step.Y.ToString(CultureInfo.InvariantCulture);
            DelayTextBox.Text = _step.DelaySeconds.ToString(CultureInfo.InvariantCulture);
            DoubleClickCheckBox.IsChecked = _step.DoubleClick;
        }

        private void ShowError(string key)
        {
            ErrorTextBlock.Text = Loc.T(key);
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            int x, y;
            if (!int.TryParse(XCoordTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
             || !int.TryParse(YCoordTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
            {
                ShowError("notify.invalidCoordinates");
                return;
            }

            double delay;
            if (!double.TryParse(DelayTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out delay)
             || delay < 0)
            {
                ShowError("editStep.invalidDelay");
                return;
            }

            _step.WindowTitle = (WindowTitleTextBox.Text ?? string.Empty).Trim();
            _step.X = x;
            _step.Y = y;
            _step.DelaySeconds = delay;
            _step.DoubleClick = DoubleClickCheckBox.IsChecked == true;

            this.DialogResult = true;
            this.Close();
        }

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
