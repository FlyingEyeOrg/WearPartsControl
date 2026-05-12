using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartThresholdService : ApplicationServiceBase, IWearPartThresholdService
{
    private const int MinimumAccessLevelForThresholdEdit = 4;

    private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
    private readonly IWearPartRepository _wearPartRepository;
    private readonly IPlcOperationPipeline _plcOperationPipeline;

    public WearPartThresholdService(
        ICurrentUserAccessor currentUserAccessor,
        IClientAppConfigurationRepository clientAppConfigurationRepository,
        IWearPartRepository wearPartRepository,
        IPlcOperationPipeline plcOperationPipeline)
        : base(currentUserAccessor)
    {
        _clientAppConfigurationRepository = clientAppConfigurationRepository;
        _wearPartRepository = wearPartRepository;
        _plcOperationPipeline = plcOperationPipeline;
    }

    public async Task<WearPartThresholdProfile> GetProfileAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
    {
        var definition = await GetRequiredDefinitionAsync(wearPartDefinitionId, cancellationToken).ConfigureAwait(false);
        return MapToProfile(definition);
    }

    public async Task<WearPartThresholdProfile> UpdateThresholdsAsync(WearPartThresholdUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureAccessLevel(MinimumAccessLevelForThresholdEdit);
        ValidateThresholds(request.WarningLifetimeThreshold, request.ShutdownLifetimeThreshold);

        var definition = await GetRequiredDefinitionAsync(request.WearPartDefinitionId, cancellationToken).ConfigureAwait(false);
        definition.WarningLifetimeThreshold = request.WarningLifetimeThreshold;
        definition.ShutdownLifetimeThreshold = request.ShutdownLifetimeThreshold;

        await _wearPartRepository.UpdateAsync(definition, cancellationToken).ConfigureAwait(false);
        await _wearPartRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapToProfile(definition);
    }

    public async Task<WearPartThresholdPlcSnapshot> ReadPlcThresholdsAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
    {
        var definition = await GetRequiredDefinitionAsync(wearPartDefinitionId, cancellationToken).ConfigureAwait(false);
        var clientAppConfiguration = await GetRequiredClientAppConfigurationAsync(definition.ClientAppConfigurationId, cancellationToken).ConfigureAwait(false);

        await _plcOperationPipeline.ConnectAsync(
            PlcWearPartThresholdOperations.Connect,
            WearPartPlcAccessor.BuildConnectionOptions(clientAppConfiguration),
            cancellationToken).ConfigureAwait(false);

        return await ReadPlcThresholdsCoreAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WearPartThresholdPlcSnapshot> OverwritePlcThresholdsAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
    {
        EnsureAccessLevel(MinimumAccessLevelForThresholdEdit);

        var definition = await GetRequiredDefinitionAsync(wearPartDefinitionId, cancellationToken).ConfigureAwait(false);
        ValidateThresholds(definition.WarningLifetimeThreshold, definition.ShutdownLifetimeThreshold);

        var clientAppConfiguration = await GetRequiredClientAppConfigurationAsync(definition.ClientAppConfigurationId, cancellationToken).ConfigureAwait(false);
        await _plcOperationPipeline.ConnectAsync(
            PlcWearPartThresholdOperations.Connect,
            WearPartPlcAccessor.BuildConnectionOptions(clientAppConfiguration),
            cancellationToken).ConfigureAwait(false);

        await WearPartPlcAccessor.WriteLifetimeValueAsync(
            _plcOperationPipeline,
            PlcWearPartThresholdOperations.WriteWarningThreshold,
            definition.WarningValueAddress,
            definition.WarningValueDataType,
            definition.WarningLifetimeThreshold,
            cancellationToken).ConfigureAwait(false);

        await WearPartPlcAccessor.WriteLifetimeValueAsync(
            _plcOperationPipeline,
            PlcWearPartThresholdOperations.WriteShutdownThreshold,
            definition.ShutdownValueAddress,
            definition.ShutdownValueDataType,
            definition.ShutdownLifetimeThreshold,
            cancellationToken).ConfigureAwait(false);

        return await ReadPlcThresholdsCoreAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WearPartDefinitionEntity> GetRequiredDefinitionAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken)
    {
        if (wearPartDefinitionId == Guid.Empty)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartThreshold.DefinitionIdRequired"));
        }

        return await _wearPartRepository.GetByIdAsync(wearPartDefinitionId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(LocalizedText.Format("Services.WearPartThreshold.DefinitionNotFoundById", wearPartDefinitionId));
    }

    private async Task<ClientAppConfigurationEntity> GetRequiredClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken)
    {
        return await _clientAppConfigurationRepository.GetByIdAsync(clientAppConfigurationId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(LocalizedText.Format("Services.WearPartThreshold.ClientConfigurationNotFoundById", clientAppConfigurationId));
    }

    private static void ValidateThresholds(double warningLifetimeThreshold, double shutdownLifetimeThreshold)
    {
        if (!double.IsFinite(warningLifetimeThreshold) || warningLifetimeThreshold <= 0d)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartThreshold.WarningThresholdInvalid"));
        }

        if (!double.IsFinite(shutdownLifetimeThreshold) || shutdownLifetimeThreshold <= 0d)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartThreshold.ShutdownThresholdInvalid"));
        }

        if (warningLifetimeThreshold >= shutdownLifetimeThreshold)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartThreshold.ThresholdOrderInvalid"));
        }
    }

    private static WearPartThresholdProfile MapToProfile(WearPartDefinitionEntity definition)
    {
        return new WearPartThresholdProfile
        {
            WearPartDefinitionId = definition.Id,
            ClientAppConfigurationId = definition.ClientAppConfigurationId,
            ResourceNumber = definition.ResourceNumber,
            PartName = definition.PartName,
            LifetimeType = definition.LifetimeType,
            WarningLifetimeThreshold = definition.WarningLifetimeThreshold,
            ShutdownLifetimeThreshold = definition.ShutdownLifetimeThreshold
        };
    }

    private async Task<WearPartThresholdPlcSnapshot> ReadPlcThresholdsCoreAsync(WearPartDefinitionEntity definition, CancellationToken cancellationToken)
    {
        return new WearPartThresholdPlcSnapshot
        {
            WarningLifetimeThreshold = await WearPartPlcAccessor.ReadAsDoubleAsync(
                _plcOperationPipeline,
                PlcWearPartThresholdOperations.ReadWarningThreshold,
                definition.WarningValueAddress,
                definition.WarningValueDataType,
                cancellationToken).ConfigureAwait(false),
            ShutdownLifetimeThreshold = await WearPartPlcAccessor.ReadAsDoubleAsync(
                _plcOperationPipeline,
                PlcWearPartThresholdOperations.ReadShutdownThreshold,
                definition.ShutdownValueAddress,
                definition.ShutdownValueDataType,
                cancellationToken).ConfigureAwait(false)
        };
    }
}