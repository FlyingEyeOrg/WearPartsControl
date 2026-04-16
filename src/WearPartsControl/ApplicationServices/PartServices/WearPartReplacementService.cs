using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartReplacementService : ApplicationService, IWearPartReplacementService
{
    private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
    private readonly IWearPartRepository _wearPartRepository;
    private readonly IWearPartReplacementRecordRepository _replacementRecordRepository;
    private readonly IPlcService _plcService;

    public WearPartReplacementService(
        ICurrentUserAccessor currentUserAccessor,
        IClientAppConfigurationRepository clientAppConfigurationRepository,
        IWearPartRepository wearPartRepository,
        IWearPartReplacementRecordRepository replacementRecordRepository,
        IPlcService plcService)
        : base(currentUserAccessor)
    {
        _clientAppConfigurationRepository = clientAppConfigurationRepository;
        _wearPartRepository = wearPartRepository;
        _replacementRecordRepository = replacementRecordRepository;
        _plcService = plcService;
    }

    public async Task<WearPartReplacementPreview> GetReplacementPreviewAsync(Guid wearPartDefinitionId, CancellationToken cancellationToken = default)
    {
        var definition = await GetRequiredDefinitionAsync(wearPartDefinitionId, cancellationToken).ConfigureAwait(false);
        var clientAppConfiguration = await GetRequiredClientAppConfigurationAsync(definition.ClientAppConfigurationId, cancellationToken).ConfigureAwait(false);

        _plcService.Connect(WearPartPlcAccessor.BuildConnectionOptions(clientAppConfiguration));

        var latestRecord = await _replacementRecordRepository.GetLatestByDefinitionAsync(definition.Id, cancellationToken).ConfigureAwait(false);

        return new WearPartReplacementPreview
        {
            WearPartDefinitionId = definition.Id,
            ClientAppConfigurationId = clientAppConfiguration.Id,
            ResourceNumber = clientAppConfiguration.ResourceNumber,
            PartName = definition.PartName,
            LastBarcode = latestRecord?.NewBarcode,
            CurrentValue = WearPartPlcAccessor.ReadAsString(_plcService, definition.CurrentValueAddress, definition.CurrentValueDataType),
            WarningValue = WearPartPlcAccessor.ReadAsString(_plcService, definition.WarningValueAddress, definition.WarningValueDataType),
            ShutdownValue = WearPartPlcAccessor.ReadAsString(_plcService, definition.ShutdownValueAddress, definition.ShutdownValueDataType)
        };
    }

    public async Task<WearPartReplacementRecord> ReplaceByScanAsync(WearPartReplacementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentUser = EnsureAccessLevel(1);
        var definition = await GetRequiredDefinitionAsync(request.WearPartDefinitionId, cancellationToken).ConfigureAwait(false);
        var clientAppConfiguration = await GetRequiredClientAppConfigurationAsync(definition.ClientAppConfigurationId, cancellationToken).ConfigureAwait(false);
        var normalizedBarcode = NormalizeRequired(request.NewBarcode, "新条码不能为空。");
        var normalizedReason = NormalizeRequired(request.ReplacementReason, "更换原因不能为空。");

        ValidateBarcode(definition, normalizedBarcode);

        if (await _replacementRecordRepository.ExistsNewBarcodeAsync(definition.Id, normalizedBarcode, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            throw new UserFriendlyException($"易损件 {definition.PartName} 已存在条码 {normalizedBarcode} 的更换记录，不允许重复使用。", code: "WearPartReplacement:BarcodeDuplicated");
        }

        _plcService.Connect(WearPartPlcAccessor.BuildConnectionOptions(clientAppConfiguration));

        var currentValue = WearPartPlcAccessor.ReadAsString(_plcService, definition.CurrentValueAddress, definition.CurrentValueDataType);
        var warningValue = WearPartPlcAccessor.ReadAsString(_plcService, definition.WarningValueAddress, definition.WarningValueDataType);
        var shutdownValue = WearPartPlcAccessor.ReadAsString(_plcService, definition.ShutdownValueAddress, definition.ShutdownValueDataType);
        var latestRecord = await _replacementRecordRepository.GetLatestByDefinitionAsync(definition.Id, cancellationToken).ConfigureAwait(false);

        WearPartPlcAccessor.ClearCounter(_plcService, definition.PlcZeroClearAddress);
        WearPartPlcAccessor.WriteBarcode(_plcService, definition.BarcodeWriteAddress, normalizedBarcode);
        WearPartPlcAccessor.WriteShutdownSignal(_plcService, clientAppConfiguration.ShutdownPointAddress, shutdown: false);

        var entity = new WearPartReplacementRecordEntity
        {
            ClientAppConfigurationId = clientAppConfiguration.Id,
            WearPartDefinitionId = definition.Id,
            SiteCode = clientAppConfiguration.SiteCode,
            PartName = definition.PartName,
            OldBarcode = latestRecord?.NewBarcode,
            NewBarcode = normalizedBarcode,
            CurrentValue = currentValue,
            WarningValue = warningValue,
            ShutdownValue = shutdownValue,
            OperatorWorkNumber = currentUser.WorkId,
            OperatorUserName = currentUser.WorkId,
            ReplacementReason = normalizedReason,
            ReplacementMessage = request.ReplacementMessage?.Trim() ?? string.Empty,
            ReplacedAt = DateTime.UtcNow,
            DataType = definition.CurrentValueDataType,
            DataValue = currentValue
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
            throw new UserFriendlyException("易损件定义主键不能为空。");
        }

        return await _wearPartRepository.GetByIdAsync(wearPartDefinitionId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"未找到主键为 {wearPartDefinitionId} 的易损件定义。");
    }

    private async Task<ClientAppConfigurationEntity> GetRequiredClientAppConfigurationAsync(Guid clientAppConfigurationId, CancellationToken cancellationToken)
    {
        return await _clientAppConfigurationRepository.GetByIdAsync(clientAppConfigurationId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"未找到主键为 {clientAppConfigurationId} 的客户端配置。");
    }

    private static void ValidateBarcode(WearPartDefinitionEntity definition, string barcode)
    {
        if (barcode.Length < definition.CodeMinLength)
        {
            throw new UserFriendlyException($"条码长度不能小于 {definition.CodeMinLength}。", code: "WearPartReplacement:BarcodeTooShort");
        }

        if (barcode.Length > definition.CodeMaxLength)
        {
            throw new UserFriendlyException($"条码长度不能大于 {definition.CodeMaxLength}。", code: "WearPartReplacement:BarcodeTooLong");
        }
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
}