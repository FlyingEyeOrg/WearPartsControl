using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class ToolCodeReplacementGuard : IWearPartReplacementGuard
{
    public int Order => 150;

    public Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        if (!RequiresToolCodeValidation(context.ClientAppConfiguration.ProcedureCode))
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

    public static bool RequiresToolCodeValidation(string? procedureCode)
    {
        var normalized = procedureCode?.Trim() ?? string.Empty;
        return string.Equals(normalized, "模切分条", StringComparison.Ordinal)
            || string.Equals(normalized, "DieCutSlitting", StringComparison.OrdinalIgnoreCase);
    }
}