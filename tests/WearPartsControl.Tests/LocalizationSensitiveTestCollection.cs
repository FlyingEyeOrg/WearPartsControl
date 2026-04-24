using System.Globalization;
using WearPartsControl.ApplicationServices.Localization;
using Xunit;

namespace WearPartsControl.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizationSensitiveTestCollection
{
    public const string Name = "LocalizationSensitive";
}

internal sealed class TestCultureScope : IDisposable
{
    private readonly CultureInfo _originalCurrentCulture;
    private readonly CultureInfo _originalCurrentUiCulture;
    private readonly CultureInfo? _originalDefaultThreadCurrentCulture;
    private readonly CultureInfo? _originalDefaultThreadCurrentUiCulture;

    public TestCultureScope(string cultureName)
    {
        _originalCurrentCulture = CultureInfo.CurrentCulture;
        _originalCurrentUiCulture = CultureInfo.CurrentUICulture;
        _originalDefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentCulture;
        _originalDefaultThreadCurrentUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        LocalizedText.SetCulture(culture);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCurrentCulture;
        CultureInfo.CurrentUICulture = _originalCurrentUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _originalDefaultThreadCurrentCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultThreadCurrentUiCulture;
        LocalizedText.SetCulture(_originalCurrentUiCulture);
    }
}