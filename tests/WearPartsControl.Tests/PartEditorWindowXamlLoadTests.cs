using System;
using System.Windows;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.PartServices;
using WearPartsControl.ViewModels;
using WearPartsControl.Views;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(NavigationTabControlTestCollection.Name)]
public sealed class PartEditorWindowXamlLoadTests
{
    [Fact]
    public void AddPartWindow_ShouldLoadWithoutXamlParseException()
    {
        RunWithEnglishCulture(() =>
        {
            var viewModel = new AddPartWindowViewModel(new StubWearPartManagementService(), new StubWearPartTypeService(), new UiBusyService(TimeSpan.Zero));
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
            var viewModel = new EditPartWindowViewModel(new StubWearPartManagementService(), new StubWearPartTypeService(), new UiBusyService(TimeSpan.Zero));
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
    public async Task EditPartWindowViewModel_InitializeForEditAsync_ShouldExposeThresholdFields()
    {
        var viewModel = new EditPartWindowViewModel(new StubWearPartManagementService(), new StubWearPartTypeService(), new UiBusyService(TimeSpan.Zero));

        await viewModel.InitializeForEditAsync(new WearPartDefinition
        {
            Id = Guid.NewGuid(),
            ClientAppConfigurationId = Guid.NewGuid(),
            ResourceNumber = "RES-01",
            PartName = "刀具A",
            InputMode = "Manual",
            CurrentValueAddress = "DB1.0",
            CurrentValueDataType = "FLOAT",
            WarningValueAddress = "DB1.1",
            WarningValueDataType = "FLOAT",
            ShutdownValueAddress = "DB1.2",
            ShutdownValueDataType = "FLOAT",
            WarningLifetimeThreshold = 20,
            ShutdownLifetimeThreshold = 30,
            LifetimeType = "Count",
            CodeMinLength = 1,
            CodeMaxLength = 32,
            WearPartTypeId = Guid.NewGuid()
        });

        Assert.Equal("20", viewModel.WarningLifetimeThreshold);
        Assert.Equal("30", viewModel.ShutdownLifetimeThreshold);
    }

    [Fact]
    public void WearPartThresholdWindow_ShouldLoadWithoutXamlParseException()
    {
        RunWithEnglishCulture(() =>
        {
            var currentUserAccessor = new CurrentUserAccessor();
            currentUserAccessor.SetCurrentUser(new MhrUser
            {
                WorkId = "WORK-TEST",
                CardId = "CARD-TEST",
                AccessLevel = 4
            });

            var viewModel = new WearPartThresholdWindowViewModel(
                new StubWearPartThresholdService(),
                currentUserAccessor,
                new StubUiDispatcher(),
                new UiBusyService(TimeSpan.Zero));
            var window = new WearPartThresholdWindow(viewModel);

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

    private sealed class StubWearPartTypeService : IWearPartTypeService
    {
        public Task<IReadOnlyList<WearPartTypeDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WearPartTypeDefinition>>([
                new WearPartTypeDefinition { Id = Guid.NewGuid(), Code = WearPartTypeCodes.Uncategorized, Name = "未分类" }
            ]);
        }
    }

    private sealed class StubWearPartThresholdService : IWearPartThresholdService
    {
        public Task<WearPartThresholdProfile> GetProfileAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WearPartThresholdProfile
            {
                WearPartDefinitionId = wearPartDefinitionId,
                ClientAppConfigurationId = Guid.NewGuid(),
                ResourceNumber = "RES-01",
                PartName = "刀具A",
                LifetimeType = "Count",
                WarningLifetimeThreshold = 20,
                ShutdownLifetimeThreshold = 30
            });
        }

        public Task<WearPartThresholdProfile> UpdateThresholdsAsync(WearPartThresholdUpdateRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WearPartThresholdProfile
            {
                WearPartDefinitionId = request.WearPartDefinitionId,
                ClientAppConfigurationId = Guid.NewGuid(),
                ResourceNumber = "RES-01",
                PartName = "刀具A",
                LifetimeType = "Count",
                WarningLifetimeThreshold = request.WarningLifetimeThreshold,
                ShutdownLifetimeThreshold = request.ShutdownLifetimeThreshold
            });
        }

        public Task<WearPartThresholdPlcSnapshot> ReadPlcThresholdsAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WearPartThresholdPlcSnapshot
            {
                WarningLifetimeThreshold = 20,
                ShutdownLifetimeThreshold = 30
            });
        }

        public Task<WearPartThresholdPlcSnapshot> OverwritePlcThresholdsAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WearPartThresholdPlcSnapshot
            {
                WarningLifetimeThreshold = 20,
                ShutdownLifetimeThreshold = 30
            });
        }
    }

    private sealed class StubUiDispatcher : IUiDispatcher
    {
        public void Run(Action action) => action();

        public Task RunAsync(Action action, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
        {
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync()
        {
            return Task.CompletedTask;
        }
    }
}