using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;

namespace BackgroundClicker.Wpf.I18n
{
    internal static class JsonReader
    {
        public static Dictionary<string, string> ReadFlattened(string filePath)
        {
            return ReadFlattened(File.ReadAllBytes(filePath));
        }

        public static Dictionary<string, string> ReadFlattened(Stream stream)
        {
            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                return ReadFlattened(buffer.ToArray());
            }
        }

        public static Dictionary<string, string> ReadFlattened(byte[] bytes)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (bytes == null || bytes.Length == 0) return map;

            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max))
            {
                XElement root = XElement.Load(reader);
                Flatten(root, string.Empty, map);
            }

            return map;
        }

        private static void Flatten(XElement element, string prefix, Dictionary<string, string> map)
        {
            string type = (string)element.Attribute("type");

            if (type == "object")
            {
                foreach (XElement child in element.Elements())
                {
                    string name = XmlConvert.DecodeName(child.Name.LocalName);
                    string key = prefix.Length == 0 ? name : prefix + "." + name;
                    Flatten(child, key, map);
                }
            }
            else if (type == "array")
            {
                int index = 0;
                foreach (XElement child in element.Elements())
                {
                    Flatten(child, prefix + "." + index.ToString(), map);
                    index++;
                }
            }
            else if (prefix.Length > 0)
            {
                map[prefix] = element.Value;
            }
        }
    }
}
