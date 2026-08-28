using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace BackgroundClicker.Wpf.I18n
{
    [MarkupExtensionReturnType(typeof(object))]
    public class TExtension : MarkupExtension
    {
        public TExtension()
        {
        }

        public TExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key)) return string.Empty;

            var binding = new Binding("[" + Key + "]")
            {
                Source = LocProvider.Instance,
                Mode = BindingMode.OneWay
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
