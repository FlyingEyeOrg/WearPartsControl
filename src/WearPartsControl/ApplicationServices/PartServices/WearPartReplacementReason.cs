namespace WearPartsControl.ApplicationServices.PartServices;

public static class WearPartReplacementReason
{
    public const string Normal = "寿命到期，正常更换";
    public const string ProcessDamage = "过程损坏";
    public const string Cutover = "切拉换型";
    public const string ChangePosition = "寿命到期，更换位置";
    public const string Maintenance = "寿命到期维保";

    public static readonly IReadOnlyList<string> All =
    [
        Normal,
        ProcessDamage,
        Cutover,
        ChangePosition,
        Maintenance
    ];

    public static bool RequiresLifetimeReached(string replacementReason)
    {
        return string.Equals(replacementReason, Normal, StringComparison.Ordinal)
            || string.Equals(replacementReason, ChangePosition, StringComparison.Ordinal)
            || string.Equals(replacementReason, Maintenance, StringComparison.Ordinal);
    }

    public static bool AllowsBarcodeReuseAfterRemoval(string replacementReason)
    {
        return string.Equals(replacementReason, ProcessDamage, StringComparison.Ordinal)
            || string.Equals(replacementReason, Cutover, StringComparison.Ordinal);
    }
}