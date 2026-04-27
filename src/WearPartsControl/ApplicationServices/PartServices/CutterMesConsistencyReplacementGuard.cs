using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.Exceptions;
using UserConfigModel = WearPartsControl.ApplicationServices.UserConfig.UserConfig;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class CutterMesConsistencyReplacementGuard : IWearPartReplacementGuard
{
    private readonly ICutterMesValidationService _cutterMesValidationService;
    private readonly IUserConfigService _userConfigService;

    public CutterMesConsistencyReplacementGuard(ICutterMesValidationService cutterMesValidationService, IUserConfigService userConfigService)
    {
        _cutterMesValidationService = cutterMesValidationService;
        _userConfigService = userConfigService;
    }

    public int Order => 130;

    public async Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        if (!CutterReplacementValidationPolicy.RequiresCutterValidation(context.ClientAppConfiguration.ProcedureCode, context.Definition.WearPartType?.Code))
        {
            return;
        }

        var userConfig = await _userConfigService.GetAsync(cancellationToken).ConfigureAwait(false);
        var enableValidation = userConfig.EnableCutterMesValidation
            || (!HasAnyUserConfigValue(userConfig) && context.ClientAppConfiguration.EnableCutterMesValidation);

        if (!enableValidation)
        {
            return;
        }

        var wsdl = string.IsNullOrWhiteSpace(userConfig.CutterMesWsdl) ? context.ClientAppConfiguration.CutterMesWsdl : userConfig.CutterMesWsdl;
        var userName = string.IsNullOrWhiteSpace(userConfig.CutterMesUser) ? context.ClientAppConfiguration.CutterMesUser : userConfig.CutterMesUser;
        var password = string.IsNullOrWhiteSpace(userConfig.CutterMesPassword) ? context.ClientAppConfiguration.CutterMesPassword : userConfig.CutterMesPassword;
        var site = string.IsNullOrWhiteSpace(userConfig.CutterMesSite) ? context.ClientAppConfiguration.CutterMesSite : userConfig.CutterMesSite;

        if (string.IsNullOrWhiteSpace(wsdl)
            || string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(site))
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
            Wsdl = wsdl,
            UserName = userName,
            Password = password,
            Site = site,
            RollNumber = context.Request.RollNumber,
            Parameter = parameter
        }, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(expectedCutterCode, context.NormalizedBarcode, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CutterMesCodeMismatch"), code: "WearPartReplacement:CutterMesCodeMismatch");
        }
    }

    private static bool HasAnyUserConfigValue(UserConfigModel userConfig)
    {
        return userConfig.EnableCutterMesValidation
            || !string.IsNullOrWhiteSpace(userConfig.CutterMesWsdl)
            || !string.IsNullOrWhiteSpace(userConfig.CutterMesUser)
            || !string.IsNullOrWhiteSpace(userConfig.CutterMesPassword)
            || !string.IsNullOrWhiteSpace(userConfig.CutterMesSite);
    }
}