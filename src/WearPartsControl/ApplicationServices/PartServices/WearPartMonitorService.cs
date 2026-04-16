using WearPartsControl.ApplicationServices.ComNotification;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartMonitorService : ApplicationService, IWearPartMonitorService
{
    private const string WarningSeverity = "Warning";
    private const string ShutdownSeverity = "Shutdown";

    private readonly IBasicConfigurationRepository _basicConfigurationRepository;
    private readonly IWearPartRepository _wearPartRepository;
    private readonly IExceedLimitRecordRepository _exceedLimitRecordRepository;
    private readonly IPlcService _plcService;
    private readonly IComNotificationService _notificationService;

    public WearPartMonitorService(
        ICurrentUserAccessor currentUserAccessor,
        IBasicConfigurationRepository basicConfigurationRepository,
        IWearPartRepository wearPartRepository,
        IExceedLimitRecordRepository exceedLimitRecordRepository,
        IPlcService plcService,
        IComNotificationService notificationService)
        : base(currentUserAccessor)
    {
        _basicConfigurationRepository = basicConfigurationRepository;
        _wearPartRepository = wearPartRepository;
        _exceedLimitRecordRepository = exceedLimitRecordRepository;
        _plcService = plcService;
        _notificationService = notificationService;
    }

    public async Task<IReadOnlyList<WearPartMonitorResult>> MonitorByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
    {
        var normalizedResourceNumber = NormalizeRequired(resourceNumber, "资源号不能为空。");
        var basicConfiguration = await _basicConfigurationRepository.GetByResourceNumberAsync(normalizedResourceNumber, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"未找到资源号为 {normalizedResourceNumber} 的基础配置。");

        var definitions = await _wearPartRepository.ListByBasicConfigurationAsync(basicConfiguration.Id, cancellationToken).ConfigureAwait(false);
        if (definitions.Count == 0)
        {
            return [];
        }

        _plcService.Connect(WearPartPlcAccessor.BuildConnectionOptions(basicConfiguration));

        var now = DateTime.UtcNow;
        var shouldSaveChanges = false;
        var results = new List<WearPartMonitorResult>(definitions.Count);

        foreach (var definition in definitions)
        {
            var currentValue = WearPartPlcAccessor.ReadAsDouble(_plcService, definition.CurrentValueAddress, definition.CurrentValueDataType);
            var warningValue = WearPartPlcAccessor.ReadAsDouble(_plcService, definition.WarningValueAddress, definition.WarningValueDataType);
            var shutdownValue = WearPartPlcAccessor.ReadAsDouble(_plcService, definition.ShutdownValueAddress, definition.ShutdownValueDataType);

            var status = ResolveStatus(currentValue, warningValue, shutdownValue);
            var notificationTriggered = false;

            if (status == WearPartMonitorStatus.Warning)
            {
                notificationTriggered = await HandleEventAsync(basicConfiguration, definition, currentValue, warningValue, shutdownValue, now, WarningSeverity, cancellationToken).ConfigureAwait(false);
                shouldSaveChanges |= notificationTriggered;
            }
            else if (status == WearPartMonitorStatus.Shutdown)
            {
                notificationTriggered = await HandleEventAsync(basicConfiguration, definition, currentValue, warningValue, shutdownValue, now, ShutdownSeverity, cancellationToken).ConfigureAwait(false);
                shouldSaveChanges |= notificationTriggered;
                WearPartPlcAccessor.WriteShutdownSignal(_plcService, basicConfiguration.ShutdownPointAddress, shutdown: definition.IsShutdown);
            }

            results.Add(new WearPartMonitorResult
            {
                WearPartDefinitionId = definition.Id,
                BasicConfigurationId = basicConfiguration.Id,
                ResourceNumber = basicConfiguration.ResourceNumber,
                PartName = definition.PartName,
                CurrentValue = currentValue,
                WarningValue = warningValue,
                ShutdownValue = shutdownValue,
                Status = status,
                NotificationTriggered = notificationTriggered
            });
        }

        if (shouldSaveChanges)
        {
            await _exceedLimitRecordRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    public async Task<IReadOnlyList<ExceedLimitRecord>> GetExceedLimitRecordsAsync(Guid basicConfigurationId, CancellationToken cancellationToken = default)
    {
        var records = await _exceedLimitRecordRepository.ListByBasicConfigurationAsync(basicConfigurationId, cancellationToken).ConfigureAwait(false);
        return records.Select(MapToRecord).ToArray();
    }

    private async Task<bool> HandleEventAsync(
        BasicConfigurationEntity basicConfiguration,
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
            return false;
        }

        var message = $"资源号 {basicConfiguration.ResourceNumber} 的易损件 {definition.PartName} 当前值 {currentValue}，预警值 {warningValue}，停机值 {shutdownValue}，级别 {severity}。";
        var entity = new ExceedLimitRecordEntity
        {
            BasicConfigurationId = basicConfiguration.Id,
            WearPartDefinitionId = definition.Id,
            PartName = definition.PartName,
            CurrentValue = currentValue,
            WarningValue = warningValue,
            ShutdownValue = shutdownValue,
            Severity = severity,
            OccurredAt = occurredAt,
            NotificationMessage = message
        };

        await _exceedLimitRecordRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        if (severity == ShutdownSeverity)
        {
            await _notificationService.NotifyWorkAsync("易损件寿命停机", message, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _notificationService.NotifyGroupAsync("易损件寿命预警", message, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return true;
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
            BasicConfigurationId = entity.BasicConfigurationId,
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
}