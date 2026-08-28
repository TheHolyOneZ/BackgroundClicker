using System;
using System.IO;
using System.Xml.Serialization;

namespace BackgroundClicker.Wpf
{
    [Serializable]
    public class AppSettings
    {
        private const string FolderName = "BackgroundClickerWpf";
        private const string FileName = "settings.xml";

        public string LanguageCode { get; set; }

        public static string GetSettingsFilePath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                FolderName);

            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch
            {
                dir = AppDomain.CurrentDomain.BaseDirectory;
            }

            return Path.Combine(dir, FileName);
        }

        public static AppSettings Load()
        {
            string path = GetSettingsFilePath();

            try
            {
                if (File.Exists(path))
                {
                    using (var stream = File.OpenRead(path))
                    {
                        var serializer = new XmlSerializer(typeof(AppSettings));
                        var loaded = serializer.Deserialize(stream) as AppSettings;
                        if (loaded != null) return loaded;
                    }
                }
            }
            catch
            {
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                using (var stream = File.Create(GetSettingsFilePath()))
                {
                    var serializer = new XmlSerializer(typeof(AppSettings));
                    serializer.Serialize(stream, this);
                }
            }
            catch
            {
            }
        }
    }
}
