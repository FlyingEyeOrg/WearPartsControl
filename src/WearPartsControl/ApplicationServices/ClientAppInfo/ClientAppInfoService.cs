using WearPartsControl.ApplicationServices.AppSettings;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.ClientAppInfo;

public sealed class ClientAppInfoService : IClientAppInfoService
{
    private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
    private readonly IAppSettingsService _appSettingsService;

    public ClientAppInfoService(
        IClientAppConfigurationRepository clientAppConfigurationRepository,
        IAppSettingsService appSettingsService)
    {
        _clientAppConfigurationRepository = clientAppConfigurationRepository;
        _appSettingsService = appSettingsService;
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
        return entity is null ? CreateDefaultModel(resourceNumber) : Map(entity);
    }

    public async Task<ClientAppInfoModel> SaveAsync(ClientAppInfoSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedResourceNumber = NormalizeRequired(request.ResourceNumber, "设备资源号不能为空。");
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
                throw new UserFriendlyException($"资源号 {normalizedResourceNumber} 已存在客户端信息配置。", code: "ClientAppInfo:DuplicatedResourceNumber");
            }
        }
        else
        {
            var duplicated = await _clientAppConfigurationRepository.ExistsByResourceNumberAsync(normalizedResourceNumber, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (duplicated)
            {
                throw new UserFriendlyException($"资源号 {normalizedResourceNumber} 已存在客户端信息配置。", code: "ClientAppInfo:DuplicatedResourceNumber");
            }

            entity = new ClientAppConfigurationEntity();
        }

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

        return Map(entity);
    }

    private static void Apply(ClientAppConfigurationEntity entity, ClientAppInfoSaveRequest request, string normalizedResourceNumber)
    {
        entity.SiteCode = NormalizeRequired(request.SiteCode, "基地不能为空。");
        entity.FactoryCode = NormalizeRequired(request.FactoryCode, "工厂不能为空。");
        entity.AreaCode = NormalizeRequired(request.AreaCode, "区域不能为空。");
        entity.ProcedureCode = NormalizeRequired(request.ProcedureCode, "工序不能为空。");
        entity.EquipmentCode = NormalizeRequired(request.EquipmentCode, "设备编号不能为空。");
        entity.ResourceNumber = normalizedResourceNumber;
        entity.PlcProtocolType = NormalizeRequired(request.PlcProtocolType, "PLC 类型不能为空。");
        entity.PlcIpAddress = NormalizeRequired(request.PlcIpAddress, "PLC IP 地址不能为空。");
        entity.PlcPort = request.PlcPort;
        entity.ShutdownPointAddress = NormalizeRequired(request.ShutdownPointAddress, "停机地址不能为空。");
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
            SiemensSlot = 1,
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