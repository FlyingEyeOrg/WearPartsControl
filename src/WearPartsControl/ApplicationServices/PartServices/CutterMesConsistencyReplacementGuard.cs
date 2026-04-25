using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class CutterMesConsistencyReplacementGuard : IWearPartReplacementGuard
{
    private readonly ICutterMesValidationService _cutterMesValidationService;

    public CutterMesConsistencyReplacementGuard(ICutterMesValidationService cutterMesValidationService)
    {
        _cutterMesValidationService = cutterMesValidationService;
    }

    public int Order => 130;

    public async Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        if (!CutterReplacementValidationPolicy.RequiresCutterValidation(context.ClientAppConfiguration.ProcedureCode, context.Definition.WearPartType?.Code)
            || !context.ClientAppConfiguration.EnableCutterMesValidation)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(context.ClientAppConfiguration.CutterMesWsdl)
            || string.IsNullOrWhiteSpace(context.ClientAppConfiguration.CutterMesUser)
            || string.IsNullOrWhiteSpace(context.ClientAppConfiguration.CutterMesPassword)
            || string.IsNullOrWhiteSpace(context.ClientAppConfiguration.CutterMesSite))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CutterMesConfigurationMissing"), code: "WearPartReplacement:CutterMesConfigurationMissing");
        }

        var parameter = CutterReplacementValidationPolicy.ResolveMesParameter(context.Definition.PartName, context.Request.ToolCode);
        if (string.IsNullOrWhiteSpace(parameter))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CutterMesPositionUnresolved"), code: "WearPartReplacement:CutterMesPositionUnresolved");
        }

        var expectedCutterCode = await _cutterMesValidationService.GetExpectedCutterCodeAsync(new CutterMesValidationRequest
        {
            Wsdl = context.ClientAppConfiguration.CutterMesWsdl,
            UserName = context.ClientAppConfiguration.CutterMesUser,
            Password = context.ClientAppConfiguration.CutterMesPassword,
            Site = context.ClientAppConfiguration.CutterMesSite,
            RollNumber = context.Request.RollNumber,
            Parameter = parameter
        }, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(expectedCutterCode, context.NormalizedBarcode, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CutterMesCodeMismatch"), code: "WearPartReplacement:CutterMesCodeMismatch");
        }
    }
}