using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class CutterRollValidationReplacementGuard : IWearPartReplacementGuard
{
    public int Order => 120;

    public Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        if (!CutterReplacementValidationPolicy.RequiresCutterValidation(context.ClientAppConfiguration.ProcedureCode, context.Definition.WearPartType?.Code))
        {
            return Task.CompletedTask;
        }

        var rollNumber = context.Request.RollNumber?.Trim() ?? string.Empty;
        if (rollNumber.Length < 16 || rollNumber.Length > 21)
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CutterRollNumberInvalid"), code: "WearPartReplacement:CutterRollNumberInvalid");
        }

        if (string.IsNullOrWhiteSpace(context.Request.BurrResult))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CutterBurrResultRequired"), code: "WearPartReplacement:CutterBurrResultRequired");
        }

        return Task.CompletedTask;
    }
}