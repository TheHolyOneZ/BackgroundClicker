using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BackgroundClicker.Wpf.I18n
{
    public sealed class Language
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string FilePath { get; set; }
        public Dictionary<string, string> Strings { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public static class Loc
    {
        public const string FallbackCode = "en";
        private const string LangFolderName = "Lang";

        private static readonly List<Language> _languages = new List<Language>();
        private static Language _current;
        private static Language _fallback;
        private static bool _initialized;

        public static event EventHandler LanguageChanged;

        public static ReadOnlyCollection<Language> Available
        {
            get { return _languages.AsReadOnly(); }
        }

        public static Language Current
        {
            get { return _current; }
        }

        public static string CurrentCode
        {
            get { return _current != null ? _current.Code : FallbackCode; }
        }

        public static string LanguageFolder
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LangFolderName); }
        }

        public static void Initialize(string preferredCode)
        {
            if (!_initialized)
            {
                LoadAll();
                _initialized = true;
            }

            _fallback = FindByCode(FallbackCode) ?? _languages.FirstOrDefault();

            Language chosen = FindByCode(preferredCode)
                              ?? FindByCode(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
                              ?? _fallback;

            _current = chosen;
        }

        private static void LoadAll()
        {
            _languages.Clear();
            LoadEmbedded();
            LoadFromDisk();

            _languages.Sort((a, b) =>
            {
                bool aEn = string.Equals(a.Code, FallbackCode, StringComparison.OrdinalIgnoreCase);
                bool bEn = string.Equals(b.Code, FallbackCode, StringComparison.OrdinalIgnoreCase);
                if (aEn != bEn) return aEn ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        private static void LoadEmbedded()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            foreach (string resource in assembly.GetManifestResourceNames())
            {
                if (resource.IndexOf(".Lang.", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!resource.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resource))
                    {
                        if (stream == null) continue;

                        string fallbackCode = Path.GetFileNameWithoutExtension(resource);
                        Language lang = Build(JsonReader.ReadFlattened(stream), fallbackCode, null);
                        if (lang != null) Add(lang);
                    }
                }
                catch
                {
                }
            }
        }

        private static void LoadFromDisk()
        {
            string folder = LanguageFolder;
            if (!Directory.Exists(folder)) return;

            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*.json");
            }
            catch
            {
                return;
            }

            foreach (string file in files)
            {
                Language lang = TryLoad(file);
                if (lang != null) Add(lang);
            }
        }

        private static void Add(Language lang)
        {
            int existing = _languages.FindIndex(
                l => string.Equals(l.Code, lang.Code, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0) _languages[existing] = lang;
            else _languages.Add(lang);
        }

        private static Language Build(Dictionary<string, string> strings, string fallbackCode, string filePath)
        {
            if (strings == null || strings.Count == 0) return null;

            string code;
            if (!strings.TryGetValue("_meta.code", out code) || string.IsNullOrEmpty(code))
            {
                code = fallbackCode;
            }
            if (string.IsNullOrEmpty(code)) return null;

            string name;
            if (!strings.TryGetValue("_meta.name", out name) || string.IsNullOrEmpty(name))
            {
                name = code.ToUpperInvariant();
            }

            return new Language
            {
                Code = code.Trim(),
                Name = name.Trim(),
                FilePath = filePath,
                Strings = strings
            };
        }

        private static Language TryLoad(string file)
        {
            try
            {
                return Build(JsonReader.ReadFlattened(file), Path.GetFileNameWithoutExtension(file), file);
            }
            catch
            {
                return null;
            }
        }

        private static Language FindByCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            return _languages.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        public static bool SetLanguage(string code)
        {
            Language lang = FindByCode(code);
            if (lang == null) return false;
            return SetLanguage(lang);
        }

        public static bool SetLanguage(Language lang)
        {
            if (lang == null || lang == _current) return false;

            _current = lang;

            EventHandler handler = LanguageChanged;
            if (handler != null) handler(null, EventArgs.Empty);

            return true;
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            string value;

            if (_current != null && _current.Strings.TryGetValue(key, out value) && value != null)
                return value;

            if (_fallback != null && _fallback.Strings.TryGetValue(key, out value) && value != null)
                return value;

            return key;
        }

        public static string T(string key, params object[] args)
        {
            string format = T(key);
            if (args == null || args.Length == 0) return format;

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }
}
