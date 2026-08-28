using System;
using System.Windows;
using System.Windows.Threading;
using BackgroundClicker.Wpf.I18n;

namespace BackgroundClicker.Wpf
{
    public partial class App : Application
    {
        public static AppSettings Settings { get; private set; }

        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Settings = AppSettings.Load();
            Loc.Initialize(Settings.LanguageCode);

            base.OnStartup(e);
        }

        public static void ChangeLanguage(Language language)
        {
            if (!Loc.SetLanguage(language)) return;

            if (Settings == null) Settings = new AppSettings();
            Settings.LanguageCode = Loc.CurrentCode;
            Settings.Save();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString(), "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Environment.Exit(1);
        }
    }
}
