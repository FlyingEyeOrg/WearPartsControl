using System.Globalization;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.UserConfig;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class CutterKdlRangeReplacementGuard : IWearPartReplacementGuard
{
    private readonly ICutterMesValidationService _cutterMesValidationService;
    private readonly IKdlRecipeManagementService _kdlRecipeManagementService;
    private readonly IUserConfigService _userConfigService;

    public CutterKdlRangeReplacementGuard(
        ICutterMesValidationService cutterMesValidationService,
        IKdlRecipeManagementService kdlRecipeManagementService,
        IUserConfigService userConfigService)
    {
        _cutterMesValidationService = cutterMesValidationService;
        _kdlRecipeManagementService = kdlRecipeManagementService;
        _userConfigService = userConfigService;
    }

    public int Order => 135;

    public async Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        if (!CutterReplacementValidationPolicy.RequiresCutterValidation(context.ClientAppConfiguration.ProcedureCode, context.Definition.WearPartType?.Code))
        {
            return;
        }

        var currentRecipe = await _kdlRecipeManagementService.GetCurrentRecipeAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CutterKdlRecipeNotConfigured"), code: "WearPartReplacement:CutterKdlRecipeNotConfigured");

        var snapshot = context.CutterMesValidationSnapshot;
        if (snapshot is null)
        {
            snapshot = await _cutterMesValidationService.GetValidationSnapshotAsync(await BuildRequiredRequestAsync(context, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            context.CutterMesValidationSnapshot = snapshot;
        }

        if (string.IsNullOrWhiteSpace(snapshot.KdlText))
        {
            throw new UserFriendlyException(LocalizedText.Get("Services.WearPartReplacement.CutterMesKdlMissing"), code: "WearPartReplacement:CutterMesKdlMissing");
        }

        if (!snapshot.KdlValue.HasValue)
        {
            throw new UserFriendlyException(
                LocalizedText.Format("Services.WearPartReplacement.CutterMesKdlInvalid", snapshot.KdlText),
                code: "WearPartReplacement:CutterMesKdlInvalid");
        }

        var kdlValue = snapshot.KdlValue.Value;
        if (kdlValue < currentRecipe.LowerLimit || kdlValue > currentRecipe.UpperLimit)
        {
            throw new UserFriendlyException(
                LocalizedText.Format(
                    "Services.WearPartReplacement.CutterKdlOutOfRange",
                    snapshot.KdlText,
                    currentRecipe.LowerLimit.ToString(CultureInfo.InvariantCulture),
                    currentRecipe.UpperLimit.ToString(CultureInfo.InvariantCulture),
                    currentRecipe.Name),
                code: "WearPartReplacement:CutterKdlOutOfRange");
        }
    }

    private async Task<CutterMesValidationRequest> BuildRequiredRequestAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken)
    {
        var userConfig = await _userConfigService.GetAsync(cancellationToken).ConfigureAwait(false);
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

        return new CutterMesValidationRequest
        {
            Wsdl = wsdl,
            UserName = userName,
            Password = password,
            Site = site,
            RollNumber = context.Request.RollNumber,
            Parameter = parameter
        };
    }
}