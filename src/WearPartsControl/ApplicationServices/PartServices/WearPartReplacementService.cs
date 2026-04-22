using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartReplacementService : ApplicationService, IWearPartReplacementService
{
    private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
    private readonly IWearPartRepository _wearPartRepository;
    private readonly IWearPartReplacementRecordRepository _replacementRecordRepository;
    private readonly IPlcOperationPipeline _plcOperationPipeline;
    private readonly IReadOnlyList<IWearPartReplacementGuard> _replacementGuards;

    public WearPartReplacementService(
        ICurrentUserAccessor currentUserAccessor,
        IClientAppConfigurationRepository clientAppConfigurationRepository,
        IWearPartRepository wearPartRepository,
        IWearPartReplacementRecordRepository replacementRecordRepository,
        IPlcOperationPipeline plcOperationPipeline,
        IEnumerable<IWearPartReplacementGuard> replacementGuards)
        : base(currentUserAccessor)
    {
        _clientAppConfigurationRepository = clientAppConfigurationRepository;
        _wearPartRepository = wearPartRepository;
        _replacementRecordRepository = replacementRecordRepository;
        _plcOperationPipeline = plcOperationPipeline;
        _replacementGuards = replacementGuards.OrderBy(x => x.Order).ToArray();
    }

    public async Task<WearPartReplacementPreview> GetReplacementPreviewAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
    {
        var definition = await GetRequiredDefinitionAsync(wearPartDefinitionId, cancellationToken).ConfigureAwait(false);
        var clientAppConfiguration = await GetRequiredClientAppConfigurationAsync(definition.ClientAppConfigurationId, cancellationToken).ConfigureAwait(false);

        var latestRecord = await _replacementRecordRepository.GetLatestByDefinitionAsync(definition.Id, cancellationToken).ConfigureAwait(false);

        var previewValues = await _plcOperationPipeline.ExecuteAsync("Replacement/GetPreview", async plcService =>
        {
            await plcService.ConnectAsync(WearPartPlcAccessor.BuildConnectionOptions(clientAppConfiguration), cancellationToken).ConfigureAwait(false);
            return new ReplacementPreviewValues(
                WearPartPlcAccessor.ReadAsString(plcService, definition.CurrentValueAddress, definition.CurrentValueDataType),
                WearPartPlcAccessor.ReadAsString(plcService, definition.WarningValueAddress, definition.WarningValueDataType),
                WearPartPlcAccessor.ReadAsString(plcService, definition.ShutdownValueAddress, definition.ShutdownValueDataType));
        }, cancellationToken).ConfigureAwait(false);

        return new WearPartReplacementPreview
        {
            WearPartDefinitionId = definition.Id,
            ClientAppConfigurationId = clientAppConfiguration.Id,
            ResourceNumber = clientAppConfiguration.ResourceNumber,
            PartName = definition.PartName,
            LastBarcode = latestRecord?.NewBarcode,
            CurrentValue = previewValues.CurrentValue,
            WarningValue = previewValues.WarningValue,
            ShutdownValue = previewValues.ShutdownValue
        };
    }

    public async Task<WearPartReplacementRecord> ReplaceByScanAsync(WearPartReplacementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentUser = EnsureAccessLevel(1);
        var definition = await GetRequiredDefinitionAsync(request.WearPartDefinitionId, cancellationToken).ConfigureAwait(false);
        var clientAppConfiguration = await GetRequiredClientAppConfigurationAsync(definition.ClientAppConfigurationId, cancellationToken).ConfigureAwait(false);
        var normalizedBarcode = NormalizeRequired(request.NewBarcode, LocalizedText.Get("Services.WearPartReplacement.NewBarcodeRequired"));
        var normalizedReason = NormalizeRequired(request.ReplacementReason, LocalizedText.Get("Services.WearPartReplacement.ReasonRequired"));

        var latestRecord = await _replacementRecordRepository.GetLatestByDefinitionAsync(definition.Id, cancellationToken).ConfigureAwait(false);

        var plcResult = await _plcOperationPipeline.ExecuteAsync("Replacement/ReplaceByScan", async plcService =>
        {
            await plcService.ConnectAsync(WearPartPlcAccessor.BuildConnectionOptions(clientAppConfiguration), cancellationToken).ConfigureAwait(false);

            var currentValue = WearPartPlcAccessor.ReadAsString(plcService, definition.CurrentValueAddress, definition.CurrentValueDataType);
            var warningValue = WearPartPlcAccessor.ReadAsString(plcService, definition.WarningValueAddress, definition.WarningValueDataType);
            var shutdownValue = WearPartPlcAccessor.ReadAsString(plcService, definition.ShutdownValueAddress, definition.ShutdownValueDataType);

            var guardContext = new WearPartReplacementGuardContext
            {
                Request = request,
                CurrentUser = currentUser,
                ClientAppConfiguration = clientAppConfiguration,
                Definition = definition,
                NormalizedBarcode = normalizedBarcode,
                NormalizedReason = normalizedReason,
                CurrentValueText = currentValue,
                WarningValueText = warningValue,
                ShutdownValueText = shutdownValue,
                CurrentValue = WearPartReplacementValueParser.ParseDouble(currentValue, definition.CurrentValueDataType, definition.CurrentValueAddress),
                WarningValue = WearPartReplacementValueParser.ParseDouble(warningValue, definition.WarningValueDataType, definition.WarningValueAddress),
                ShutdownValue = WearPartReplacementValueParser.ParseDouble(shutdownValue, definition.ShutdownValueDataType, definition.ShutdownValueAddress),
                LatestRecord = latestRecord,
                PlcWriteValue = 0d
            };

            foreach (var guard in _replacementGuards)
            {
                await guard.ValidateAsync(guardContext, cancellationToken).ConfigureAwait(false);
            }

            if (HasAddress(definition.PlcZeroClearAddress))
            {
                WearPartPlcAccessor.PulseZeroClearSignal(plcService, definition.PlcZeroClearAddress);
                guardContext.PlcWriteValue = 0d;
            }
            else
            {
                WearPartPlcAccessor.WriteCurrentValue(plcService, definition.CurrentValueAddress, definition.CurrentValueDataType, guardContext.PlcWriteValue);
            }

            WearPartPlcAccessor.WriteBarcode(plcService, definition.BarcodeWriteAddress, normalizedBarcode);
            WearPartPlcAccessor.WriteShutdownSignal(plcService, clientAppConfiguration.ShutdownPointAddress, shutdown: false);

            return new ReplacementExecutionResult(guardContext.PlcWriteValue, currentValue, warningValue, shutdownValue);
        }, cancellationToken).ConfigureAwait(false);

        var oldBarcode = latestRecord?.NewBarcode;
        if (latestRecord is null
            && (string.Equals(normalizedReason, WearPartReplacementReason.ChangePosition, StringComparison.Ordinal)
                || string.Equals(normalizedReason, WearPartReplacementReason.Maintenance, StringComparison.Ordinal)))
        {
            oldBarcode = normalizedBarcode;
        }

        var entity = new WearPartReplacementRecordEntity
        {
            ClientAppConfigurationId = clientAppConfiguration.Id,
            WearPartDefinitionId = definition.Id,
            SiteCode = clientAppConfiguration.SiteCode,
            PartName = definition.PartName,
            OldBarcode = oldBarcode,
            NewBarcode = normalizedBarcode,
            CurrentValue = plcResult.CurrentValue,
            WarningValue = plcResult.WarningValue,
            ShutdownValue = plcResult.ShutdownValue,
            OperatorWorkNumber = currentUser.WorkId,
            OperatorUserName = currentUser.WorkId,
            ReplacementReason = normalizedReason,
            ReplacementMessage = request.ReplacementMessage?.Trim() ?? string.Empty,
            ReplacedAt = DateTime.UtcNow,
            DataType = definition.CurrentValueDataType,
            DataValue = plcResult.PlcWriteValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        await _replacementRecordRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _replacementRecordRepository.UnitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToRecord(entity);
    }

    public async Task<IReadOnlyList<WearPartReplacementRecord>> GetReplacementHistoryAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken = default)
    {
        var entities = await _replacementRecordRepository.ListByClientAppConfigurationAsync(clientAppConfigurationId, cancellationToken).ConfigureAwait(false);
        return entities.Select(MapToRecord).ToArray();
    }

    private async Task<WearPartDefinitionEntity> GetRequiredDefinitionAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken)
    {
        if (wearPartDefinitionId == Guid.Empty)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.DefinitionIdRequired"));
        }

        return await _wearPartRepository.GetByIdAsync(wearPartDefinitionId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(LocalizedText.Format("Services.WearPartReplacement.DefinitionNotFoundById", wearPartDefinitionId));
    }

    private async Task<ClientAppConfigurationEntity> GetRequiredClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken)
    {
        return await _clientAppConfigurationRepository.GetByIdAsync(clientAppConfigurationId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(LocalizedText.Format("Services.WearPartReplacement.ClientConfigurationNotFoundById", clientAppConfigurationId));
    }

    private static WearPartReplacementRecord MapToRecord(WearPartReplacementRecordEntity entity)
    {
        return new WearPartReplacementRecord
        {
            Id = entity.Id,
            ClientAppConfigurationId = entity.ClientAppConfigurationId,
            WearPartDefinitionId = entity.WearPartDefinitionId,
            SiteCode = entity.SiteCode,
            PartName = entity.PartName,
            OldBarcode = entity.OldBarcode,
            NewBarcode = entity.NewBarcode,
            CurrentValue = entity.CurrentValue,
            WarningValue = entity.WarningValue,
            ShutdownValue = entity.ShutdownValue,
            OperatorWorkNumber = entity.OperatorWorkNumber,
            OperatorUserName = entity.OperatorUserName,
            ReplacementReason = entity.ReplacementReason,
            ReplacementMessage = entity.ReplacementMessage,
            ReplacedAt = entity.ReplacedAt,
            DataType = WearPartPlcAccessor.ResolvePartDataType(entity.DataType),
            DataValue = entity.DataValue
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

    private static bool HasAddress(string address)
    {
        return !string.IsNullOrWhiteSpace(address) && !string.Equals(address.Trim(), "######", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ReplacementPreviewValues(string CurrentValue, string WarningValue, string ShutdownValue);

    private sealed record ReplacementExecutionResult(double PlcWriteValue, string CurrentValue, string WarningValue, string ShutdownValue);
}