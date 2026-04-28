using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.UserConfig;

[SaveInfoFile("user-config")]
public sealed class UserConfig
{
    public const string DefaultLanguage = "en-US";

    public const bool DefaultComNotificationEnabled = true;

    public const bool DefaultAutoStartEnabled = false;

    public const string DefaultComPushUrl = "https://ipaas.catl.com/gateway/office/ipaas/MSG/office_MSG_push";

    public const string DefaultComDeIpaasKeyAuth = "659JOPEldYL55Mi3sqb38H9Txd1q9EYw";

    public const long DefaultComAgentId = 1642112457;

    public const long DefaultComGroupTemplateId = 303686603505665;

    public const long DefaultComWorkTemplateId = 303717003821057;

    public const string DefaultComUserType = "ding";

    public const int DefaultComTimeoutMilliseconds = 10000;

    public const string DefaultSpacerValidationUrl = "https://172.18.65.139:65530/vulnerable-parts-service/api/v1.0/spacer-validation-data/verify";

    public const string DefaultSpacerValidationUrlRelease = "https://172.18.65.139:65530/vulnerable-parts-service/api/v1.0/spacer-validation-data/verify";

    public const string DefaultSpacerValidationCodeSeparator = "/";

    public const int DefaultSpacerValidationTimeoutMilliseconds = 5000;

    public const int DefaultSpacerValidationExpectedSegmentCount = 8;

    public string MeResponsibleWorkId { get; set; } = string.Empty;

    public string MeResponsibleName { get; set; } = string.Empty;

    public string PrdResponsibleWorkId { get; set; } = string.Empty;

    public string PrdResponsibleName { get; set; } = string.Empty;

    public string ReplacementOperatorName { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public bool AutoStartEnabled { get; set; } = DefaultAutoStartEnabled;

    public string ComAccessToken { get; set; } = string.Empty;

    public string ComSecret { get; set; } = string.Empty;

    public bool ComNotificationEnabled { get; set; } = DefaultComNotificationEnabled;

    public string ComPushUrl { get; set; } = DefaultComPushUrl;

    public string ComDeIpaasKeyAuth { get; set; } = DefaultComDeIpaasKeyAuth;

    public long ComAgentId { get; set; } = DefaultComAgentId;

    public long ComGroupTemplateId { get; set; } = DefaultComGroupTemplateId;

    public long ComWorkTemplateId { get; set; } = DefaultComWorkTemplateId;

    public string ComUserType { get; set; } = DefaultComUserType;

    public int ComTimeoutMilliseconds { get; set; } = DefaultComTimeoutMilliseconds;

    public bool SpacerValidationEnabled { get; set; } = true;

    public string SpacerValidationUrl { get; set; } = DefaultSpacerValidationUrl;

    public string SpacerValidationUrlRelease { get; set; } = DefaultSpacerValidationUrlRelease;

    public int SpacerValidationTimeoutMilliseconds { get; set; } = DefaultSpacerValidationTimeoutMilliseconds;

    public bool SpacerValidationIgnoreServerCertificateErrors { get; set; } = true;

    public string SpacerValidationCodeSeparator { get; set; } = DefaultSpacerValidationCodeSeparator;

    public int SpacerValidationExpectedSegmentCount { get; set; } = DefaultSpacerValidationExpectedSegmentCount;

    public bool EnableCutterMesValidation { get; set; }

    public string CutterMesWsdl { get; set; } = string.Empty;

    public string CutterMesUser { get; set; } = string.Empty;

    public string CutterMesPassword { get; set; } = string.Empty;

    public string CutterMesSite { get; set; } = string.Empty;
}