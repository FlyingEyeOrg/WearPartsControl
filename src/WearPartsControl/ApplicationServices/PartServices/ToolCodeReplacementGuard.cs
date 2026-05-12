using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class ToolCodeReplacementGuard : IWearPartReplacementGuard
{
    public int Order => 150;

    public Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        if (!RequiresToolCodeValidation(context.ClientAppConfiguration.ProcedureCode, context.Definition.WearPartType?.Code))
        {
            return Task.CompletedTask;
        }

        var normalizedToolCode = context.Request.ToolCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedToolCode))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.ToolCodeRequired"), code: "WearPartReplacement:ToolCodeRequired");
        }

        if (!context.NormalizedBarcode.Contains(normalizedToolCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException(LocalizedText.Format("Services.WearPartReplacement.ToolCodeMismatch", normalizedToolCode), code: "WearPartReplacement:ToolCodeMismatch");
        }

        return Task.CompletedTask;
    }

    public static bool RequiresToolCodeValidation(string? procedureCode, string? wearPartTypeCode)
    {
        var normalizedProcedure = procedureCode?.Trim() ?? string.Empty;
        var normalizedWearPartType = wearPartTypeCode?.Trim() ?? string.Empty;
        return (string.Equals(normalizedProcedure, "模切分条", StringComparison.Ordinal)
                || string.Equals(normalizedProcedure, "DieCutSlitting", StringComparison.OrdinalIgnoreCase))
            && string.Equals(normalizedWearPartType, WearPartTypeCodes.Cutter, StringComparison.OrdinalIgnoreCase);
    }
}