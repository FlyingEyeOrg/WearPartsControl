using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace WearPartsControl.ApplicationServices.Localization;

[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            return string.Empty;
        }

        return new Binding($"[{Key}]")
        {
            Source = LocalizationBindingSource.Instance,
            Mode = BindingMode.OneWay
        }.ProvideValue(serviceProvider);
    }
}