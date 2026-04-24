using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.ClientAppInfo;

public sealed class ClientAppInfoService : IClientAppInfoService
{
    private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IClientAppInfoSelectionOptionsProvider _selectionOptionsProvider;

    public ClientAppInfoService(
        IClientAppConfigurationRepository clientAppConfigurationRepository,
        IAppSettingsService appSettingsService,
        IClientAppInfoSelectionOptionsProvider selectionOptionsProvider)
    {
        _clientAppConfigurationRepository = clientAppConfigurationRepository;
        _appSettingsService = appSettingsService;
        _selectionOptionsProvider = selectionOptionsProvider;
    }

    public async Task<ClientAppInfoModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        var resourceNumber = settings.ResourceNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resourceNumber))
        {
            return CreateDefaultModel();
        }

        var entity = await _clientAppConfigurationRepository.GetByResourceNumberAsync(resourceNumber, cancellationToken).ConfigureAwait(false);
        var model = entity is null ? CreateDefaultModel(resourceNumber) : Map(entity);
        return await NormalizeSelectionValuesAsync(model, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedResourceNumber = NormalizeRequired(request.ResourceNumber, LocalizedText.Get("Services.ClientAppInfo.ResourceNumberRequired"));
        ClientAppConfigurationEntity? entity = null;

        if (request.Id.HasValue && request.Id.Value != Guid.Empty)
        {
            entity = await _clientAppConfigurationRepository.GetForUpdateByIdAsync(request.Id.Value, cancellationToken).ConfigureAwait(false);
        }

        entity ??= await _clientAppConfigurationRepository.GetForUpdateByResourceNumberAsync(normalizedResourceNumber, cancellationToken).ConfigureAwait(false);

        if (entity is not null)
        {
            var duplicated = await _clientAppConfigurationRepository.ExistsByResourceNumberAsync(normalizedResourceNumber, entity.Id, cancellationToken).ConfigureAwait(false);
            if (duplicated)
            {
                throw new UserFriendlyException(LocalizedText.Format("Services.ClientAppInfo.DuplicatedResourceNumber", normalizedResourceNumber), code: "ClientAppInfo:DuplicatedResourceNumber");
            }
        }
        else
        {
            var duplicated = await _clientAppConfigurationRepository.ExistsByResourceNumberAsync(normalizedResourceNumber, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (duplicated)
            {
                throw new UserFriendlyException(LocalizedText.Format("Services.ClientAppInfo.DuplicatedResourceNumber", normalizedResourceNumber), code: "ClientAppInfo:DuplicatedResourceNumber");
            }

            entity = new ClientAppConfigurationEntity();
        }

        request.AreaCode = await _selectionOptionsProvider.MapAreaOptionAsync(request.AreaCode, "zh-CN", cancellationToken).ConfigureAwait(false);
        request.ProcedureCode = await _selectionOptionsProvider.MapProcedureOptionAsync(request.ProcedureCode, "zh-CN", cancellationToken).ConfigureAwait(false);

        Apply(entity, request, normalizedResourceNumber);

        if (entity.CreatedAt == default || entity.Id == Guid.Empty)
        {
            await _clientAppConfigurationRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _clientAppConfigurationRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        await _clientAppConfigurationRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var settings = await _appSettingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        settings.ResourceNumber = normalizedResourceNumber;
        settings.IsSetClientAppInfo = true;
        await _appSettingsService.SaveAsync(settings, cancellationToken).ConfigureAwait(false);

        var savedModel = Map(entity);
        return await NormalizeSelectionValuesAsync(savedModel, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ClientAppInfoModel> NormalizeSelectionValuesAsync(ClientAppInfoModel model, CancellationToken cancellationToken)
    {
        model.AreaCode = await _selectionOptionsProvider.MapAreaOptionAsync(model.AreaCode, "zh-CN", cancellationToken).ConfigureAwait(false);
        model.ProcedureCode = await _selectionOptionsProvider.MapProcedureOptionAsync(model.ProcedureCode, "zh-CN", cancellationToken).ConfigureAwait(false);
        return model;
    }

    private static void Apply(ClientAppConfigurationEntity entity, ClientAppInfoSaveRequest request, string normalizedResourceNumber)
    {
        entity.SiteCode = NormalizeRequired(request.SiteCode, LocalizedText.Get("Services.ClientAppInfo.SiteCodeRequired"));
        entity.FactoryCode = NormalizeRequired(request.FactoryCode, LocalizedText.Get("Services.ClientAppInfo.FactoryCodeRequired"));
        entity.AreaCode = NormalizeRequired(request.AreaCode, LocalizedText.Get("Services.ClientAppInfo.AreaCodeRequired"));
        entity.ProcedureCode = NormalizeRequired(request.ProcedureCode, LocalizedText.Get("Services.ClientAppInfo.ProcedureCodeRequired"));
        entity.EquipmentCode = NormalizeRequired(request.EquipmentCode, LocalizedText.Get("Services.ClientAppInfo.EquipmentCodeRequired"));
        entity.ResourceNumber = normalizedResourceNumber;
        entity.PlcProtocolType = NormalizeRequired(request.PlcProtocolType, LocalizedText.Get("Services.ClientAppInfo.PlcProtocolRequired"));
        entity.PlcIpAddress = NormalizeRequired(request.PlcIpAddress, LocalizedText.Get("Services.ClientAppInfo.PlcIpRequired"));
        entity.PlcPort = request.PlcPort;
        entity.ShutdownPointAddress = NormalizeRequired(request.ShutdownPointAddress, LocalizedText.Get("Services.ClientAppInfo.ShutdownPointAddressRequired"));
        entity.SiemensRack = request.SiemensRack;
        entity.SiemensSlot = request.SiemensSlot;
        entity.IsStringReverse = request.IsStringReverse;
        entity.EnsureValid();
    }

    private static ClientAppInfoModel Map(ClientAppConfigurationEntity entity)
    {
        return new ClientAppInfoModel
        {
            Id = entity.Id,
            SiteCode = entity.SiteCode,
            FactoryCode = entity.FactoryCode,
            AreaCode = entity.AreaCode,
            ProcedureCode = entity.ProcedureCode,
            EquipmentCode = entity.EquipmentCode,
            ResourceNumber = entity.ResourceNumber,
            PlcProtocolType = entity.PlcProtocolType,
            PlcIpAddress = entity.PlcIpAddress,
            PlcPort = entity.PlcPort,
            ShutdownPointAddress = entity.ShutdownPointAddress,
            SiemensRack = entity.SiemensRack,
            SiemensSlot = entity.SiemensSlot,
            IsStringReverse = entity.IsStringReverse
        };
    }

    private static ClientAppInfoModel CreateDefaultModel(string resourceNumber = "")
    {
        return new ClientAppInfoModel
        {
            ResourceNumber = resourceNumber,
            PlcProtocolType = "SiemensS1500",
            PlcPort = 102,
            SiemensRack = 0,
            SiemensSlot = 0,
            IsStringReverse = true
        };
    }

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserFriendlyException(errorMessage);
        }

        return value.Trim();
    }
}