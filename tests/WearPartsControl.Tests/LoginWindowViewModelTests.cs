using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Infrastructure.EntityFrameworkCore;
using WearPartsControl.ViewModels;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class LoginWindowViewModelTests
{
    [Fact]
    public async Task LoginCommand_ShouldUseConfiguredResourceAndSiteCode()
    {
        var loginService = new StubLoginService();
        var viewModel = new LoginWindowViewModel(
            loginService,
            new StubClientAppConfigurationRepository(),
            new StubAppSettingsService());

        bool? dialogResult = null;
        viewModel.RequestClose += (_, result) => dialogResult = result;

        await viewModel.InitializeAsync();
        viewModel.AuthId = "CARD-01";

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal("RES-001", loginService.ResourceNumber);
        Assert.Equal("SITE-01", loginService.SiteCode);
        Assert.True(loginService.IsIdCard);
        Assert.True(dialogResult);
    }

    private sealed class StubLoginService : ILoginService
    {
        public string ResourceNumber { get; private set; } = string.Empty;

        public string SiteCode { get; private set; } = string.Empty;

        public bool IsIdCard { get; private set; }

        public Task<MhrUser?> LoginAsync(string authId, string factory, string resourceId, bool isIdCard, CancellationToken cancellationToken = default)
        {
            SiteCode = factory;
            ResourceNumber = resourceId;
            IsIdCard = isIdCard;

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
                LoginInputMaxIntervalMilliseconds = 88
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
}