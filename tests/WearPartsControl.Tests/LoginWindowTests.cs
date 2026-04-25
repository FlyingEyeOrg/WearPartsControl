using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WearPartsControl.ApplicationServices;
using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using WearPartsControl.ViewModels;
using WearPartsControl.Views;
using Xunit;

namespace WearPartsControl.Tests;

[Collection(UserTabControlTestCollection.Name)]
public sealed class LoginWindowTests
{
    [Fact]
    public void Show_ShouldLoadWithoutXamlParseException_WhenViewModelHasReadOnlyDisplayProperties()
    {
        using var cultureScope = new TestCultureScope("en-US");

        WpfTestHost.Run(() =>
        {
            var viewModel = new LoginWindowViewModel(
                new StubLoginService(),
                new StubClientAppConfigurationRepository(),
                new StubAppSettingsService(),
                new StubUiDispatcher());
            viewModel.InitializeAsync().GetAwaiter().GetResult();

            var window = new LoginWindow(viewModel);

            try
            {
                window.Show();
                WpfTestHost.DrainDispatcher();
                window.UpdateLayout();
                WpfTestHost.DrainDispatcher();

                Assert.Same(viewModel, window.DataContext);
                Assert.Equal("RES-001", viewModel.ResourceNumber);
                Assert.Equal("SITE-01", viewModel.SiteCode);
            }
            finally
            {
                window.Close();
                WpfTestHost.DrainDispatcher();
            }
        }, ensureApplicationResources: true);
    }

    private sealed class StubLoginService : ILoginService
    {
        public Task<MhrUser?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<MhrUser?>(new MhrUser
            {
                CardId = authId,
                WorkId = "WORK-01",
                AccessLevel = 1
            });
        }

        public MhrUser? GetCurrentUser() => null;

        public ValueTask LogoutAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class StubAppSettingsService : IAppSettingsService
    {
        public event EventHandler<AppSettings>? SettingsSaved;

        public ValueTask<AppSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AppSettings
            {
                ResourceNumber = "RES-001",
                LoginInputMaxIntervalMilliseconds = 88,
                UseWorkNumberLogin = false
            });
        }

        public ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SettingsSaved?.Invoke(this, settings);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubClientAppConfigurationRepository : IClientAppConfigurationRepository
    {
        public IUnitOfWork UnitOfWork => throw new NotSupportedException();

        public Task<ClientAppConfigurationEntity?> GetByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ClientAppConfigurationEntity?>(new ClientAppConfigurationEntity
            {
                SiteCode = "SITE-01",
                FactoryCode = "FACTORY-01",
                AreaCode = "AREA-01",
                ProcedureCode = "PROC-01",
                EquipmentCode = "EQ-01",
                ResourceNumber = resourceNumber,
                PlcProtocolType = "SiemensS7",
                PlcIpAddress = "127.0.0.1",
                PlcPort = 102
            });
        }

        public Task<ClientAppConfigurationEntity?> GetForUpdateByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
        {
            return GetByResourceNumberAsync(resourceNumber, cancellationToken);
        }

        public Task<bool> ExistsByResourceNumberAsync(string resourceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ClientAppConfigurationEntity>> ListAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ClientAppConfigurationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ClientAppConfigurationEntity?> GetForUpdateByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(ClientAppConfigurationEntity entity, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(ClientAppConfigurationEntity entity, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubUiDispatcher : IUiDispatcher
    {
        public void Run(Action action) => action();

        public Task RunAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            action();
            return Task.CompletedTask;
        }

        public Task RenderAsync() => Task.CompletedTask;
    }
}