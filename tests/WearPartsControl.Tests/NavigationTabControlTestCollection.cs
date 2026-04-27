using Xunit;

namespace WearPartsControl.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NavigationTabControlTestCollection
{
    public const string Name = "NavigationTabControl.Wpf";
}