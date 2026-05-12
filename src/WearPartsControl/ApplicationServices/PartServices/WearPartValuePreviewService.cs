using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Domain.Repositories;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartValuePreviewService : IWearPartValuePreviewService
{
    private readonly IClientAppConfigurationRepository _clientAppConfigurationRepository;
    private readonly IWearPartRepository _wearPartRepository;
    private readonly IPlcOperationPipeline _plcOperationPipeline;

    public WearPartValuePreviewService(
        IClientAppConfigurationRepository clientAppConfigurationRepository,
        IWearPartRepository wearPartRepository,
        IPlcOperationPipeline plcOperationPipeline)
    {
        _clientAppConfigurationRepository = clientAppConfigurationRepository;
        _wearPartRepository = wearPartRepository;
        _plcOperationPipeline = plcOperationPipeline;
    }

    public async Task<IReadOnlyList<WearPartValuePreviewItem>> GetByResourceNumberAsync(string resourceNumber, CancellationToken cancellationToken = default)
    {
        var normalizedResourceNumber = NormalizeRequired(resourceNumber, LocalizedText.Get("Services.Common.ResourceNumberRequired"));
        var clientAppConfiguration = await _clientAppConfigurationRepository.GetByResourceNumberAsync(normalizedResourceNumber, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(LocalizedText.Format("Services.WearPartManagement.ClientConfigurationNotFoundByResourceNumber", normalizedResourceNumber));

        var definitions = await _wearPartRepository.ListByClientAppConfigurationAsync(clientAppConfiguration.Id, cancellationToken).ConfigureAwait(false);
        if (definitions.Count == 0)
        {
            return [];
        }

        await _plcOperationPipeline.ConnectAsync(
            PlcWearPartValuePreviewOperations.Connect,
            WearPartPlcAccessor.BuildConnectionOptions(clientAppConfiguration),
            cancellationToken).ConfigureAwait(false);

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
                Status = WearPartLifetimeStatusResolver.Resolve(currentValue, warningValue, shutdownValue)
            });
        }

        return items;
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