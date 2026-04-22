using WearPartsControl.ApplicationServices.Localization;

namespace WearPartsControl.ApplicationServices.PartServices;

public static class WearPartReplacementReason
{
    public const string Normal = "normal";
    public const string ProcessDamage = "process-damage";
    public const string Cutover = "cutover";
    public const string ChangePosition = "change-position";
    public const string Maintenance = "maintenance";

    private const string LegacyNormal = "寿命到期，正常更换";
    private const string LegacyProcessDamage = "过程损坏";
    private const string LegacyCutover = "切拉换型";
    private const string LegacyChangePosition = "寿命到期，更换位置";
    private const string LegacyMaintenance = "寿命到期维保";

    public static readonly IReadOnlyList<WearPartReplacementReasonOption> All =
    [
        new(Normal, "Services.WearPartReplacementReason.Normal"),
        new(ProcessDamage, "Services.WearPartReplacementReason.ProcessDamage"),
        new(Cutover, "Services.WearPartReplacementReason.Cutover"),
        new(ChangePosition, "Services.WearPartReplacementReason.ChangePosition"),
        new(Maintenance, "Services.WearPartReplacementReason.Maintenance")
    ];

    public static bool RequiresWarningLifetime(string replacementReason)
    {
        var normalizedReason = NormalizeCode(replacementReason);
        return string.Equals(normalizedReason, Normal, StringComparison.Ordinal)
            || string.Equals(normalizedReason, ChangePosition, StringComparison.Ordinal)
            || string.Equals(normalizedReason, Maintenance, StringComparison.Ordinal);
    }

    public static bool RequiresBelowShutdownLifetime(string replacementReason)
    {
        return string.Equals(NormalizeCode(replacementReason), ChangePosition, StringComparison.Ordinal);
    }

    public static bool AllowsBarcodeReuseAfterRemoval(string replacementReason)
    {
        var normalizedReason = NormalizeCode(replacementReason);
        return string.Equals(normalizedReason, ProcessDamage, StringComparison.Ordinal)
            || string.Equals(normalizedReason, Cutover, StringComparison.Ordinal);
    }

    public static string NormalizeCode(string replacementReason)
    {
        var normalizedReason = replacementReason?.Trim() ?? string.Empty;

        return normalizedReason switch
        {
            Normal or LegacyNormal => Normal,
            ProcessDamage or LegacyProcessDamage => ProcessDamage,
            Cutover or LegacyCutover => Cutover,
            ChangePosition or LegacyChangePosition => ChangePosition,
            Maintenance or LegacyMaintenance => Maintenance,
            _ => normalizedReason
        };
    }

    public static string GetDisplayName(string replacementReason)
    {
        var normalizedReason = NormalizeCode(replacementReason);
        var option = All.FirstOrDefault(x => string.Equals(x.Code, normalizedReason, StringComparison.Ordinal));
        return option?.DisplayName ?? replacementReason;
    }
}

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