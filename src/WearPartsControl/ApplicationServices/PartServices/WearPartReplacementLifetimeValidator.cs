using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

internal static class WearPartReplacementLifetimeValidator
{
    public static string? GetValidationError(string? currentValueText, string? warningValueText, string? shutdownValueText, double? currentValue, double? warningValue, double? shutdownValue)
    {
        if (string.IsNullOrWhiteSpace(currentValueText)
            || string.IsNullOrWhiteSpace(warningValueText)
            || string.IsNullOrWhiteSpace(shutdownValueText)
            || !currentValue.HasValue
            || !warningValue.HasValue
            || !shutdownValue.HasValue)
        {
            return LocalizedText.Get("Services.WearPartReplacement.LifetimeValuesUnavailable");
        }

        if (warningValue.Value >= shutdownValue.Value)
        {
            return LocalizedText.Get("Services.WearPartReplacement.LifetimeThresholdInvalid");
        }

        return null;
    }

    public static void ValidateOrThrow(string? currentValueText, string? warningValueText, string? shutdownValueText, double? currentValue, double? warningValue, double? shutdownValue)
    {
        var validationError = GetValidationError(currentValueText, warningValueText, shutdownValueText, currentValue, warningValue, shutdownValue);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            throw new UserFriendlyException(validationError);
        }
    }
}