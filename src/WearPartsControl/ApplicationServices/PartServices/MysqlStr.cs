using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// MySQL 连接字符串配置。
/// </summary>
[SaveInfoFile("settings/mysql")]
public sealed class MysqlStr
{
    /// <summary>
    /// 数据库连接字符串。
    /// </summary>
    public string ConnectString { get; set; } = string.Empty;
}
