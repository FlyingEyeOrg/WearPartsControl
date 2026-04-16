using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.SpacerManagement;

[SaveInfoFile("spacer-validation")]
public sealed class SpacerValidationOptionsSaveInfo
{
    public bool Enabled { get; set; } = true;

    public string ValidationUrl { get; set; } = string.Empty;

    public int TimeoutMilliseconds { get; set; } = 5000;

    public bool IgnoreServerCertificateErrors { get; set; } = true;

    public string CodeSeparator { get; set; } = "/";

    public int ExpectedSegmentCount { get; set; } = 8;
}
