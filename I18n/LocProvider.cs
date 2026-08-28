using System;
using System.ComponentModel;

namespace BackgroundClicker.Wpf.I18n
{
    public sealed class LocProvider : INotifyPropertyChanged
    {
        private static readonly LocProvider _instance = new LocProvider();

        public static LocProvider Instance
        {
            get { return _instance; }
        }

        private LocProvider()
        {
            Loc.LanguageChanged += OnLanguageChanged;
        }

        public string this[string key]
        {
            get { return Loc.T(key); }
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs("Item[]"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
