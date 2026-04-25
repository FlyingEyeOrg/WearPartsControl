using Microsoft.Extensions.DependencyInjection;

namespace WearPartsControl.ApplicationServices.Shell;

public sealed class MainWindowContentFactory : IMainWindowContentFactory
{
    private readonly IServiceProvider _serviceProvider;

    public MainWindowContentFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public object Create(Type contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        return _serviceProvider.GetRequiredService(contentType);
    }
}