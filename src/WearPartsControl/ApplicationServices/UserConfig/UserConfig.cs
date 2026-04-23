using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.UserConfig;

[SaveInfoFile("user-config")]
public sealed class UserConfig
{
    public const string DefaultSpacerValidationCodeSeparator = "/";

    public const int DefaultSpacerValidationTimeoutMilliseconds = 5000;

    public const int DefaultSpacerValidationExpectedSegmentCount = 8;

    public string MeResponsibleWorkId { get; set; } = string.Empty;

    public string PrdResponsibleWorkId { get; set; } = string.Empty;

    public string ComAccessToken { get; set; } = string.Empty;

    public string ComSecret { get; set; } = string.Empty;

    public bool SpacerValidationEnabled { get; set; } = true;

    public string SpacerValidationUrl { get; set; } = string.Empty;

    public int SpacerValidationTimeoutMilliseconds { get; set; } = DefaultSpacerValidationTimeoutMilliseconds;

    public bool SpacerValidationIgnoreServerCertificateErrors { get; set; } = true;

    public string SpacerValidationCodeSeparator { get; set; } = DefaultSpacerValidationCodeSeparator;

    public int SpacerValidationExpectedSegmentCount { get; set; } = DefaultSpacerValidationExpectedSegmentCount;
}