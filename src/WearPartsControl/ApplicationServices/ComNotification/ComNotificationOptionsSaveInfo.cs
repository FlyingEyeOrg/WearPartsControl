using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.ComNotification;

[SaveInfoFile("com-notification")]
public sealed class ComNotificationOptionsSaveInfo
{
    public bool Enabled { get; set; }

    public string PushUrl { get; set; } = "https://ipaas.catl.com/gateway/office/ipaas/MSG/office_MSG_push";

    public string DeIpaasKeyAuth { get; set; } = string.Empty;

    public long AgentId { get; set; } = 1642112457;

    public long GroupTemplateId { get; set; } = 303686603505665;

    public long WorkTemplateId { get; set; } = 303717003821057;

    public string UserType { get; set; } = "ding";

    public string AccessToken { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public string DefaultUserWorkId { get; set; } = string.Empty;

    public int TimeoutMilliseconds { get; set; } = 10000;
}
