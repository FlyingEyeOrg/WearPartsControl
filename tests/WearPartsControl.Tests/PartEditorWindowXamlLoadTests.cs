using System;
using System.Windows;
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
        RunWithEnglishCulture(() =>
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
        });
    }

    [Fact]
    public void EditPartWindow_ShouldLoadWithoutXamlParseException()
    {
        RunWithEnglishCulture(() =>
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
        });
    }

    [Fact]
    public void NotificationPreviewWindow_ShouldLoadWithoutXamlParseException()
    {
        RunWithEnglishCulture(() =>
        {
            var window = new NotificationPreviewWindow("# warning", "# shutdown");

            try
            {
                Assert.NotNull(window);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MessageDialogWindow_ShouldLoadWithoutXamlParseException()
    {
        RunWithEnglishCulture(() =>
        {
            var window = new MessageDialogWindow(
                "This is a localized dialog message used to validate dynamic layout in English.",
                "Dialog Title",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            try
            {
                Assert.NotNull(window);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunWithEnglishCulture(Action action)
    {
        using var cultureScope = new TestCultureScope("en-US");
        WpfTestHost.Run(action, ensureApplicationResources: true);
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