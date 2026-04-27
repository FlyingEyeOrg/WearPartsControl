using System.Globalization;
using WearPartsControl.ApplicationServices.Localization;
using Xunit;

namespace WearPartsControl.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizationSensitiveTestCollection
{
    public const string Name = "LocalizationSensitive";
}