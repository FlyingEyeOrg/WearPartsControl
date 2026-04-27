using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.ComNotification;

[SaveInfoFile("com-notification")]
public sealed class ComNotificationOptionsSaveInfo
{
    public bool Enabled { get; set; }

    public string PushUrl { get; set; } = UserConfig.UserConfig.DefaultComPushUrl;

    public string DeIpaasKeyAuth { get; set; } = UserConfig.UserConfig.DefaultComDeIpaasKeyAuth;

    public long AgentId { get; set; } = UserConfig.UserConfig.DefaultComAgentId;

    public long GroupTemplateId { get; set; } = UserConfig.UserConfig.DefaultComGroupTemplateId;

    public long WorkTemplateId { get; set; } = UserConfig.UserConfig.DefaultComWorkTemplateId;

    public string UserType { get; set; } = UserConfig.UserConfig.DefaultComUserType;

    public string AccessToken { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public string DefaultUserWorkId { get; set; } = string.Empty;

    public int TimeoutMilliseconds { get; set; } = UserConfig.UserConfig.DefaultComTimeoutMilliseconds;
}