using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BackgroundClicker.Wpf
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
        }

        private void RailButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            string sectionName = button.Tag as string;
            if (string.IsNullOrEmpty(sectionName)) return;

            var section = FindName(sectionName) as FrameworkElement;
            if (section != null) section.BringIntoView();
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
