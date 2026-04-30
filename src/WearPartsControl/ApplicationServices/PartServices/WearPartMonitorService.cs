using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.MonitoringLogs;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;
using UserConfigModel = WearPartsControl.ApplicationServices.UserConfig.UserConfig;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartMonitorService : ApplicationServiceBase, IWearPartMonitorService
{
    private const string WarningSeverity = "Warning";
    private const string ShutdownSeverity = "Shutdown";

    private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
    private readonly IWearPartRepository _wearPartRepository;
    private readonly IWearPartReplacementRecordRepository _replacementRecordRepository;
    private readonly IExceedLimitRecordRepository _exceedLimitRecordRepository;
    private readonly IPlcOperationPipeline _plcOperationPipeline;
    private readonly IComNotificationService _notificationService;
    private readonly IWearPartAlertPopupService _wearPartAlertPopupService;
    private readonly IUserConfigService _userConfigService;
    private readonly IWearPartMonitoringLogPipeline? _monitoringLogPipeline;

    public WearPartMonitorService(
        ICurrentUserAccessor currentUserAccessor,
        IClientAppConfigurationRepository clientAppConfigurationRepository,
        IWearPartRepository wearPartRepository,
        IWearPartReplacementRecordRepository replacementRecordRepository,
        IExceedLimitRecordRepository exceedLimitRecordRepository,
        IPlcOperationPipeline plcOperationPipeline,
        IComNotificationService notificationService,
        IWearPartAlertPopupService wearPartAlertPopupService,
        IUserConfigService userConfigService,
        IWearPartMonitoringLogPipeline? monitoringLogPipeline = null)
        : base(currentUserAccessor)
    {
        _clientAppConfigurationRepository = clientAppConfigurationRepository;
        _wearPartRepository = wearPartRepository;
        _replacementRecordRepository = replacementRecordRepository;
        _exceedLimitRecordRepository = exceedLimitRecordRepository;
        _plcOperationPipeline = plcOperationPipeline;
        _notificationService = notificationService;
        _wearPartAlertPopupService = wearPartAlertPopupService;
        _userConfigService = userConfigService;
        _monitoringLogPipeline = monitoringLogPipeline;
    }

    public async Task<IReadOnlyList<WearPartMonitorResult>> MonitorByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
    {
        var normalizedResourceNumber = NormalizeRequired(resourceNumber, LocalizedText.Get("Services.Common.ResourceNumberRequired"));
        var clientAppConfiguration = await _clientAppConfigurationRepository.GetByResourceNumberAsync(normalizedResourceNumber, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(LocalizedText.Format("Services.WearPartMonitor.ClientConfigurationNotFoundByResourceNumber", normalizedResourceNumber));

        var definitions = await _wearPartRepository.ListByClientAppConfigurationAsync(clientAppConfiguration.Id, cancellationToken).ConfigureAwait(false);
        if (definitions.Count == 0)
        {
            PublishServiceLog(WearPartMonitoringLogLevel.Information, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorDefinitionsEmpty", normalizedResourceNumber), normalizedResourceNumber);
            return [];
        }

        PublishServiceLog(WearPartMonitoringLogLevel.Information, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorDefinitionsLoaded", normalizedResourceNumber, definitions.Count), normalizedResourceNumber);

        var connectionOptions = WearPartPlcAccessor.BuildConnectionOptions(clientAppConfiguration);
        await _plcOperationPipeline.ConnectAsync(PlcMonitoringPipelineOperations.Connect, connectionOptions, cancellationToken).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var userConfig = await _userConfigService.GetAsync(cancellationToken).ConfigureAwait(false);
        var shouldSaveChanges = false;
        var plcResults = new List<MonitorSnapshot>(definitions.Count);
        foreach (var definition in definitions)
        {
            var currentValue = await WearPartPlcAccessor.ReadAsDoubleAsync(_plcOperationPipeline, PlcMonitoringPipelineOperations.ReadCurrentValue, definition.CurrentValueAddress, definition.CurrentValueDataType, cancellationToken).ConfigureAwait(false);
            var warningValue = await WearPartPlcAccessor.ReadAsDoubleAsync(_plcOperationPipeline, PlcMonitoringPipelineOperations.ReadWarningValue, definition.WarningValueAddress, definition.WarningValueDataType, cancellationToken).ConfigureAwait(false);
            var shutdownValue = await WearPartPlcAccessor.ReadAsDoubleAsync(_plcOperationPipeline, PlcMonitoringPipelineOperations.ReadShutdownValue, definition.ShutdownValueAddress, definition.ShutdownValueDataType, cancellationToken).ConfigureAwait(false);
            var status = ResolveStatus(currentValue, warningValue, shutdownValue);

            if (status == WearPartMonitorStatus.Shutdown)
            {
                await WearPartPlcAccessor.WriteShutdownSignalAsync(_plcOperationPipeline, PlcMonitoringPipelineOperations.WriteShutdownSignal, clientAppConfiguration.ShutdownPointAddress, shutdown: definition.IsShutdown, cancellationToken).ConfigureAwait(false);
                PublishServiceLog(WearPartMonitoringLogLevel.Warning, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorShutdownSignalWritten", definition.PartName), normalizedResourceNumber);
            }

            PublishServiceLog(
                status == WearPartMonitorStatus.Normal ? WearPartMonitoringLogLevel.Information : WearPartMonitoringLogLevel.Warning,
                LocalizedText.Format("Services.WearPartMonitoringLog.MonitorItemRead", definition.PartName, currentValue, warningValue, shutdownValue, status),
                normalizedResourceNumber,
                operationName: PlcMonitoringPipelineOperations.ReadCurrentValue,
                address: definition.CurrentValueAddress);

            plcResults.Add(new MonitorSnapshot(definition, currentValue, warningValue, shutdownValue, status));
        }

        var results = new List<WearPartMonitorResult>(plcResults.Count);
        foreach (var snapshot in plcResults)
        {
            var notificationTriggered = false;

            if (snapshot.Status == WearPartMonitorStatus.Warning)
            {
                notificationTriggered = await HandleEventAsync(clientAppConfiguration, userConfig, snapshot.Definition, snapshot.CurrentValue, snapshot.WarningValue, snapshot.ShutdownValue, now, WarningSeverity, cancellationToken).ConfigureAwait(false);
                shouldSaveChanges |= notificationTriggered;
            }
            else if (snapshot.Status == WearPartMonitorStatus.Shutdown)
            {
                notificationTriggered = await HandleEventAsync(clientAppConfiguration, userConfig, snapshot.Definition, snapshot.CurrentValue, snapshot.WarningValue, snapshot.ShutdownValue, now, ShutdownSeverity, cancellationToken).ConfigureAwait(false);
                shouldSaveChanges |= notificationTriggered;
            }

            results.Add(new WearPartMonitorResult
            {
                WearPartDefinitionId = snapshot.Definition.Id,
                ClientAppConfigurationId = clientAppConfiguration.Id,
                ResourceNumber = clientAppConfiguration.ResourceNumber,
                PartName = snapshot.Definition.PartName,
                CurrentValue = snapshot.CurrentValue,
                WarningValue = snapshot.WarningValue,
                ShutdownValue = snapshot.ShutdownValue,
                Status = snapshot.Status,
                NotificationTriggered = notificationTriggered
            });
        }

        if (shouldSaveChanges)
        {
            await _exceedLimitRecordRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    public async Task<IReadOnlyList<ExceedLimitRecord>> GetExceedLimitRecordsAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
    {
        var records = await _exceedLimitRecordRepository.ListByClientAppConfigurationAsync(clientAppConfigurationId, cancellationToken).ConfigureAwait(false);
        return records.Select(MapToRecord).ToArray();
    }

    private async Task<bool> HandleEventAsync(
        ClientAppConfigurationEntity clientAppConfiguration,
        UserConfigModel userConfig,
        WearPartDefinitionEntity definition,
        double currentValue,
        double warningValue,
        double shutdownValue,
        DateTime occurredAt,
        string severity,
        CancellationToken cancellationToken)
    {
        if (await _exceedLimitRecordRepository.ExistsForDayAsync(definition.Id, severity, occurredAt, cancellationToken).ConfigureAwait(false))
        {
            PublishServiceLog(WearPartMonitoringLogLevel.Information, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorNotificationSkippedDaily", definition.PartName, severity), clientAppConfiguration.ResourceNumber);
            return false;
        }

        var latestReplacementRecord = await _replacementRecordRepository.GetLatestByDefinitionAsync(definition.Id, cancellationToken).ConfigureAwait(false);
        var replacementOperator = ResolveReplacementOperator(latestReplacementRecord, userConfig, CurrentUser?.WorkId);

        var message = ComNotificationMessageFactory.CreateWearPartAlertMessage(
            clientAppConfiguration,
            latestReplacementRecord?.NewBarcode,
            definition.LifetimeType,
            severity,
            definition.PartName,
            currentValue,
            warningValue,
            shutdownValue,
            userConfig.MeResponsibleName,
            userConfig.MeResponsibleWorkId,
            userConfig.PrdResponsibleName,
            userConfig.PrdResponsibleWorkId,
            replacementOperator.Name,
            replacementOperator.WorkNumber,
            occurredAt);
        var entity = new ExceedLimitRecordEntity
        {
            ClientAppConfigurationId = clientAppConfiguration.Id,
            WearPartDefinitionId = definition.Id,
            PartName = definition.PartName,
            CurrentValue = currentValue,
            WarningValue = warningValue,
            ShutdownValue = shutdownValue,
            Severity = severity,
            OccurredAt = occurredAt,
            NotificationMessage = message.Markdown
        };

        await _exceedLimitRecordRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        if (severity == ShutdownSeverity)
        {
            await _notificationService.NotifyWorkAsync(message.Title, message.Markdown, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _notificationService.NotifyGroupAsync(message.Title, message.Markdown, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await _wearPartAlertPopupService.ShowIfNeededAsync(message.Title, message.Markdown, occurredAt, cancellationToken).ConfigureAwait(false);

        PublishServiceLog(WearPartMonitoringLogLevel.Warning, LocalizedText.Format("Services.WearPartMonitoringLog.MonitorNotificationTriggered", definition.PartName, severity), clientAppConfiguration.ResourceNumber);

        return true;
    }

    private static ReplacementOperator ResolveReplacementOperator(WearPartReplacementRecordEntity? latestReplacementRecord, UserConfigModel userConfig, string? currentUserWorkId)
    {
        var recordName = NormalizeOptional(latestReplacementRecord?.OperatorUserName);
        var recordWorkNumber = NormalizeOptional(latestReplacementRecord?.OperatorWorkNumber);
        var configuredName = NormalizeOptional(userConfig.ReplacementOperatorName);
        var normalizedCurrentUserWorkId = NormalizeOptional(currentUserWorkId);

        var name = recordName ?? configuredName ?? normalizedCurrentUserWorkId;
        var workNumber = recordWorkNumber
            ?? ResolveReplacementOperatorWorkNumberFallback(recordName, configuredName, normalizedCurrentUserWorkId);

        return new ReplacementOperator(name, workNumber);
    }

    private static string? ResolveReplacementOperatorWorkNumberFallback(string? recordName, string? configuredName, string? currentUserWorkId)
    {
        if (!string.IsNullOrWhiteSpace(currentUserWorkId)
            && (string.Equals(recordName, currentUserWorkId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(configuredName, currentUserWorkId, StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrWhiteSpace(recordName) && string.IsNullOrWhiteSpace(configuredName))))
        {
            return currentUserWorkId;
        }

        if (LooksLikeWorkNumber(recordName))
        {
            return recordName;
        }

        return LooksLikeWorkNumber(configuredName)
            ? configuredName
            : null;
    }

    private static bool LooksLikeWorkNumber(string? value)
    {
        var normalized = NormalizeOptional(value);
        return normalized is not null
            && normalized.Any(char.IsDigit)
            && normalized.All(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');
    }

    private void PublishServiceLog(
        WearPartMonitoringLogLevel level,
        string message,
        string? resourceNumber = null,
        string? operationName = null,
        string? address = null,
        Exception? exception = null)
    {
        _monitoringLogPipeline?.Publish(
            level,
            WearPartMonitoringLogCategory.Service,
            message,
            operationName: operationName ?? nameof(WearPartMonitorService),
            resourceNumber: resourceNumber,
            address: address,
            details: exception?.Message,
            exception: exception);
    }

    private static WearPartMonitorStatus ResolveStatus(double currentValue, double warningValue, double shutdownValue)
    {
        if (shutdownValue > 0d && currentValue >= shutdownValue)
        {
            return WearPartMonitorStatus.Shutdown;
        }

        if (warningValue > 0d && currentValue >= warningValue)
        {
            return WearPartMonitorStatus.Warning;
        }

        return WearPartMonitorStatus.Normal;
    }

    private static ExceedLimitRecord MapToRecord(ExceedLimitRecordEntity entity)
    {
        return new ExceedLimitRecord
        {
            Id = entity.Id,
            PartName = entity.PartName,
            WearPartDefinitionId = entity.WearPartDefinitionId,
            CurrentValue = entity.CurrentValue,
            ShutdownValue = entity.ShutdownValue,
            Severity = entity.Severity,
            OccurredAt = entity.OccurredAt,
            ClientAppConfigurationId = entity.ClientAppConfigurationId,
            NotificationMessage = entity.NotificationMessage
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record MonitorSnapshot(
        WearPartDefinitionEntity Definition,
        double CurrentValue,
        double WarningValue,
        double ShutdownValue,
        WearPartMonitorStatus Status);

    private sealed record ReplacementOperator(string? Name, string? WorkNumber);
}
