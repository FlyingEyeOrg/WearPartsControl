using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.LoginService;
using WearPartsControl.ApplicationServices.MonitoringLogs;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartValuePreviewService : ApplicationServiceBase, IWearPartValuePreviewService
{
    private const int MinimumAccessLevelForThresholdSync = 4;

    private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
    private readonly IWearPartRepository _wearPartRepository;
    private readonly IPlcOperationPipeline _plcOperationPipeline;
    private readonly IWearPartMonitoringLogPipeline? _monitoringLogPipeline;

    public WearPartValuePreviewService(
        ICurrentUserAccessor currentUserAccessor,
        IClientAppConfigurationRepository clientAppConfigurationRepository,
        IWearPartRepository wearPartRepository,
        IPlcOperationPipeline plcOperationPipeline,
        IWearPartMonitoringLogPipeline? monitoringLogPipeline = null)
        : base(currentUserAccessor)
    {
        _clientAppConfigurationRepository = clientAppConfigurationRepository;
        _wearPartRepository = wearPartRepository;
        _plcOperationPipeline = plcOperationPipeline;
        _monitoringLogPipeline = monitoringLogPipeline;
    }

    public async Task<IReadOnlyList<WearPartValuePreviewItem>> GetByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
    {
        var context = await LoadPreviewContextAsync(resourceNumber, cancellationToken).ConfigureAwait(false);
        if (context.Definitions.Count == 0)
        {
            return [];
        }

        await ConnectAsync(PlcWearPartValuePreviewOperations.Connect, context.ClientAppConfiguration, cancellationToken).ConfigureAwait(false);
        return await BuildPreviewItemsAsync(context.ClientAppConfiguration, context.Definitions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WearPartValuePreviewItem>> SyncConfiguredThresholdsToDeviceAsync(string resourceNumber, CancellationToken cancellationToken = default)
    {
        var currentUser = EnsureAccessLevel(MinimumAccessLevelForThresholdSync);

        var context = await LoadPreviewContextAsync(resourceNumber, cancellationToken).ConfigureAwait(false);
        if (context.Definitions.Count == 0)
        {
            return [];
        }

        try
        {
            await ConnectAsync(PlcWearPartThresholdOperations.Connect, context.ClientAppConfiguration, cancellationToken).ConfigureAwait(false);

            foreach (var definition in context.Definitions)
            {
                ValidateThresholds(definition);
            }

            await EnsureAllThresholdValuesReadableAsync(context.Definitions, cancellationToken).ConfigureAwait(false);
            await WriteConfiguredThresholdsAsync(context.Definitions, cancellationToken).ConfigureAwait(false);

            var previews = await BuildPreviewItemsAsync(context.ClientAppConfiguration, context.Definitions, cancellationToken).ConfigureAwait(false);
            PublishThresholdSyncLog(
                WearPartMonitoringLogLevel.Information,
                LocalizedText.Format(
                    "Services.WearPartMonitoringLog.ThresholdSyncSucceeded",
                    context.ClientAppConfiguration.ResourceNumber,
                    context.Definitions.Count,
                    GetOperatorWorkId(currentUser)),
                context.ClientAppConfiguration.ResourceNumber);
            return previews;
        }
        catch (Exception ex)
        {
            PublishThresholdSyncLog(
                WearPartMonitoringLogLevel.Error,
                LocalizedText.Format(
                    "Services.WearPartMonitoringLog.ThresholdSyncFailed",
                    context.ClientAppConfiguration.ResourceNumber,
                    GetOperatorWorkId(currentUser),
                    ex.Message),
                context.ClientAppConfiguration.ResourceNumber,
                ex);
            throw;
        }
    }

    private void PublishThresholdSyncLog(
        WearPartMonitoringLogLevel level,
        string message,
        string resourceNumber,
        Exception? exception = null)
    {
        _monitoringLogPipeline?.Publish(
            level,
            WearPartMonitoringLogCategory.Service,
            message,
            operationName: nameof(SyncConfiguredThresholdsToDeviceAsync),
            resourceNumber: resourceNumber,
            details: exception?.Message,
            exception: exception);
    }

    private static string GetOperatorWorkId(MhrUser currentUser)
    {
        return string.IsNullOrWhiteSpace(currentUser.WorkId)
            ? string.Empty
            : currentUser.WorkId.Trim();
    }

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserFriendlyException(errorMessage);
        }

        return value.Trim();
    }

    private async Task<(ClientAppConfigurationEntity ClientAppConfiguration, IReadOnlyList<WearPartDefinitionEntity> Definitions)> LoadPreviewContextAsync(string resourceNumber, CancellationToken cancellationToken)
    {
        var normalizedResourceNumber = NormalizeRequired(resourceNumber, LocalizedText.Get("Services.Common.ResourceNumberRequired"));
        var clientAppConfiguration = await _clientAppConfigurationRepository.GetByResourceNumberAsync(normalizedResourceNumber, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(LocalizedText.Format("Services.WearPartManagement.ClientConfigurationNotFoundByResourceNumber", normalizedResourceNumber));

        var definitions = await _wearPartRepository.ListByClientAppConfigurationAsync(clientAppConfiguration.Id, cancellationToken).ConfigureAwait(false);
        return (clientAppConfiguration, definitions);
    }

    private async Task ConnectAsync(string operationName, ClientAppConfigurationEntity clientAppConfiguration, CancellationToken cancellationToken)
    {
        await _plcOperationPipeline.ConnectAsync(
            operationName,
            WearPartPlcAccessor.BuildConnectionOptions(clientAppConfiguration),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<WearPartValuePreviewItem>> BuildPreviewItemsAsync(
        ClientAppConfigurationEntity clientAppConfiguration,
        IReadOnlyList<WearPartDefinitionEntity> definitions,
        CancellationToken cancellationToken)
    {
        var items = new List<WearPartValuePreviewItem>(definitions.Count);
        foreach (var definition in definitions)
        {
            var currentValue = await WearPartPlcAccessor.ReadAsDoubleAsync(
                _plcOperationPipeline,
                PlcWearPartValuePreviewOperations.ReadCurrentValue,
                definition.CurrentValueAddress,
                definition.CurrentValueDataType,
                cancellationToken).ConfigureAwait(false);
            var warningValue = await WearPartPlcAccessor.ReadAsDoubleAsync(
                _plcOperationPipeline,
                PlcWearPartValuePreviewOperations.ReadWarningValue,
                definition.WarningValueAddress,
                definition.WarningValueDataType,
                cancellationToken).ConfigureAwait(false);
            var shutdownValue = await WearPartPlcAccessor.ReadAsDoubleAsync(
                _plcOperationPipeline,
                PlcWearPartValuePreviewOperations.ReadShutdownValue,
                definition.ShutdownValueAddress,
                definition.ShutdownValueDataType,
                cancellationToken).ConfigureAwait(false);

            items.Add(new WearPartValuePreviewItem
            {
                WearPartDefinitionId = definition.Id,
                ClientAppConfigurationId = clientAppConfiguration.Id,
                ResourceNumber = clientAppConfiguration.ResourceNumber,
                PartName = definition.PartName,
                WearPartTypeName = definition.WearPartType?.Name ?? string.Empty,
                LifetimeType = definition.LifetimeType,
                CurrentValue = currentValue,
                WarningValue = warningValue,
                ShutdownValue = shutdownValue,
                ConfiguredWarningLifetimeThreshold = definition.WarningLifetimeThreshold,
                ConfiguredShutdownLifetimeThreshold = definition.ShutdownLifetimeThreshold,
                Status = WearPartLifetimeStatusResolver.Resolve(currentValue, warningValue, shutdownValue)
            });
        }

        return items;
    }

    private async Task EnsureAllThresholdValuesReadableAsync(IReadOnlyList<WearPartDefinitionEntity> definitions, CancellationToken cancellationToken)
    {
        foreach (var definition in definitions)
        {
            await WearPartPlcAccessor.ReadAsDoubleAsync(
                _plcOperationPipeline,
                PlcWearPartThresholdOperations.ReadWarningThreshold,
                definition.WarningValueAddress,
                definition.WarningValueDataType,
                cancellationToken).ConfigureAwait(false);
            await WearPartPlcAccessor.ReadAsDoubleAsync(
                _plcOperationPipeline,
                PlcWearPartThresholdOperations.ReadShutdownThreshold,
                definition.ShutdownValueAddress,
                definition.ShutdownValueDataType,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteConfiguredThresholdsAsync(IReadOnlyList<WearPartDefinitionEntity> definitions, CancellationToken cancellationToken)
    {
        foreach (var definition in definitions)
        {
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
        }
    }

    private static void ValidateThresholds(WearPartDefinitionEntity definition)
    {
        if (!double.IsFinite(definition.WarningLifetimeThreshold) || definition.WarningLifetimeThreshold <= 0d)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartThreshold.WarningThresholdInvalid"));
        }

        if (!double.IsFinite(definition.ShutdownLifetimeThreshold) || definition.ShutdownLifetimeThreshold <= 0d)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartThreshold.ShutdownThresholdInvalid"));
        }

        if (definition.WarningLifetimeThreshold >= definition.ShutdownLifetimeThreshold)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartThreshold.ThresholdOrderInvalid"));
        }
    }
}