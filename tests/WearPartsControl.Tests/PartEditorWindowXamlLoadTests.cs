using System;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ViewModels;
using WearPartsControl.Views;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(UserTabControlTestCollection.Name)]
public sealed class PartEditorWindowXamlLoadTests
{
    [Fact]
    public void AddPartWindow_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var viewModel = new AddPartWindowViewModel(new StubWearPartManagementService(), new UiBusyService(TimeSpan.Zero));
            var window = new AddPartWindow(viewModel);

            try
            {
                Assert.Same(viewModel, window.DataContext);
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    [Fact]
    public void EditPartWindow_ShouldLoadWithoutXamlParseException()
    {
        WpfTestHost.Run(() =>
        {
            var viewModel = new EditPartWindowViewModel(new StubWearPartManagementService(), new UiBusyService(TimeSpan.Zero));
            var window = new EditPartWindow(viewModel);

            try
            {
                Assert.Same(viewModel, window.DataContext);
            }
            finally
            {
                window.Close();
            }
        }, ensureApplicationResources: true);
    }

    private sealed class StubWearPartManagementService : IWearPartManagementService
    {
        public Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WearPartDefinition>> GetDefinitionsByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<WearPartDefinition?> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<WearPartDefinition> CreateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<WearPartDefinition> UpdateDefinitionAsync(WearPartDefinition definition, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CopyDefinitionsAsync(string sourceResourceNumber, string targetResourceNumber, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}