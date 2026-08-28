using System;
using System.Windows;
using System.Windows.Input;
using BackgroundClicker.Wpf.I18n;

namespace BackgroundClicker.Wpf
{
    public partial class PromptWindow : Window
    {
        private readonly Func<string, string> _validate;

        public string Value { get; private set; }

        public PromptWindow(string title, string label, string initialValue,
                            string confirmText, Func<string, string> validate)
        {
            InitializeComponent();

            TitleTextBlock.Text = title;
            LabelTextBlock.Text = label;
            ValueTextBox.Text = initialValue ?? string.Empty;
            ConfirmButton.Content = confirmText;
            _validate = validate;
            Value = initialValue;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ValueTextBox.Focus();
            ValueTextBox.SelectAll();
            Validate();
        }

        private bool Validate()
        {
            string error = _validate == null ? null : _validate(ValueTextBox.Text);

            if (string.IsNullOrEmpty(error))
            {
                ErrorTextBlock.Visibility = Visibility.Collapsed;
                ConfirmButton.IsEnabled = true;
                return true;
            }

            ErrorTextBlock.Text = error;
            ErrorTextBlock.Visibility = Visibility.Visible;
            ConfirmButton.IsEnabled = false;
            return false;
        }

        private void ValueTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Validate();
        }

        private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Confirm();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            Confirm();
        }

        private void Confirm()
        {
            if (!Validate()) return;
            Value = (ValueTextBox.Text ?? string.Empty).Trim();
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }
    }
}
