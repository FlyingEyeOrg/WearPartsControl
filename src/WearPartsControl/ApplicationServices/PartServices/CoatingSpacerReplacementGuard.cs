using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.ApplicationServices.SpacerManagement;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class CoatingSpacerReplacementGuard : IWearPartReplacementGuard
{
    private readonly ISpacerManagementService _spacerManagementService;
    private readonly IPlcOperationPipeline _plcOperationPipeline;

    public CoatingSpacerReplacementGuard(ISpacerManagementService spacerManagementService, IPlcOperationPipeline plcOperationPipeline)
    {
        _spacerManagementService = spacerManagementService;
        _plcOperationPipeline = plcOperationPipeline;
    }

    public int Order => 160;

    public async Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        if (!RequiresCoatingValidation(context.ClientAppConfiguration.ProcedureCode))
        {
            return;
        }

        var selectedAbSide = context.Request.SelectedAbSide?.Trim().ToUpperInvariant() ?? string.Empty;
        if (selectedAbSide is not "A" and not "B")
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CoatingAbSideRequired"), code: "WearPartReplacement:CoatingAbSideRequired");
        }

        var info = await _spacerManagementService.ParseCodeAsync(
                context.NormalizedBarcode,
                context.ClientAppConfiguration.SiteCode,
                context.ClientAppConfiguration.ResourceNumber,
                context.CurrentUser.CardId,
                cancellationToken)
            .ConfigureAwait(false);

        var actualAbSide = info.ABSite?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!string.Equals(actualAbSide, selectedAbSide, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException(
                LocalizedText.Format("Services.WearPartReplacement.CoatingAbSideMismatch", selectedAbSide, actualAbSide),
                code: "WearPartReplacement:CoatingAbSideMismatch");
        }

        try
        {
            await _spacerManagementService.VerifyAsync(info, cancellationToken).ConfigureAwait(false);
        }
        catch (UserFriendlyException ex)
        {
            try
            {
                await WearPartPlcAccessor.WriteShutdownSignalAsync(
                        _plcOperationPipeline,
                        PlcReplacementPipelineOperations.WriteShutdownSignal,
                        context.ClientAppConfiguration.ShutdownPointAddress,
                        shutdown: true,
                        cancellationToken)
                    .ConfigureAwait(false);

                throw new UserFriendlyException(
                    LocalizedText.Format("Services.WearPartReplacement.SpacerValidationFailedAndShutdownApplied", ex.Message),
                    code: "WearPartReplacement:SpacerValidationFailedAndShutdownApplied",
                    details: ex.Details,
                    innerException: ex);
            }
            catch (UserFriendlyException)
            {
                throw;
            }
            catch (Exception shutdownEx)
            {
                throw new UserFriendlyException(
                    LocalizedText.Format("Services.WearPartReplacement.SpacerValidationFailedAndShutdownWriteFailed", ex.Message, shutdownEx.Message),
                    code: "WearPartReplacement:SpacerValidationFailedAndShutdownWriteFailed",
                    details: ex.Details,
                    innerException: ex);
            }
        }
    }

    public static bool RequiresCoatingValidation(string? procedureCode)
    {
        var normalized = procedureCode?.Trim() ?? string.Empty;
        return string.Equals(normalized, "涂布", StringComparison.Ordinal)
            || string.Equals(normalized, "Coating", StringComparison.OrdinalIgnoreCase);
    }
}