using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class WearPartReplacementReasonOption
{
    public WearPartReplacementReasonOption(string code, string localizationKey)
    {
        Code = code;
        LocalizationKey = localizationKey;
    }

    public string Code { get; }

    public string LocalizationKey { get; }

    public string DisplayName => LocalizedText.Get(LocalizationKey);
}