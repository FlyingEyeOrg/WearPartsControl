using System;
using System.Collections.Generic;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 本地应用配置，保存当前资源号。
/// </summary>
[SaveInfoFile("settings/app-setting")]
public sealed class AppSetting
{
    /// <summary>
    /// 当前资源号。
    /// </summary>
    public string ResourceNum { get; set; } = string.Empty;
}

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

/// <summary>
/// MHR 接口配置。
/// </summary>
[SaveInfoFile("settings/mhr")]
public sealed class MHR
{
    /// <summary>
    /// 获取 token 的地址。
    /// </summary>
    public string GetTokenUrl { get; set; } = string.Empty;

    /// <summary>
    /// 登录用户名。
    /// </summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>
    /// 登录密码。
    /// </summary>
    public string LoginPassword { get; set; } = string.Empty;

    /// <summary>
    /// 获取人员列表的地址。
    /// </summary>
    public string GetListUrl { get; set; } = string.Empty;

    /// <summary>
    /// 默认更新时间。
    /// </summary>
    public int UpdateDate { get; set; }
}

/// <summary>
/// MHR 返回结果。
/// </summary>
public sealed class HMRResult
{
    /// <summary>
    /// 请求是否成功。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误码。
    /// </summary>
    public int ErrorCode { get; set; }

    /// <summary>
    /// 错误消息。
    /// </summary>
    public string Msg { get; set; } = string.Empty;

    /// <summary>
    /// 返回的数据体。
    /// </summary>
    public HTMItemData Data { get; set; } = new();
}

/// <summary>
/// MHR 数据体。
/// </summary>
public sealed class HTMItemData
{
    /// <summary>
    /// 时间戳。
    /// </summary>
    public long timestamp { get; set; }

    /// <summary>
    /// 用户列表。
    /// </summary>
    public List<UserModel> list { get; set; } = new();

    /// <summary>
    /// 设备资源号。
    /// </summary>
    public string device_resource_id { get; set; } = string.Empty;
}

/// <summary>
/// MHR 人员信息。
/// </summary>
public sealed class UserModel
{
    /// <summary>
    /// 工号。
    /// </summary>
    public string work_id { get; set; } = string.Empty;

    /// <summary>
    /// 权限等级。
    /// </summary>
    public int access_level { get; set; }

    /// <summary>
    /// 卡号。
    /// </summary>
    public string card_id { get; set; } = string.Empty;
}

/// <summary>
/// 数据值类型枚举。
/// </summary>
public enum EDataType
{
    /// <summary>
    /// JSON。
    /// </summary>
    Json,

    /// <summary>
    /// 字符串。
    /// </summary>
    String,

    /// <summary>
    /// 整型。
    /// </summary>
    Int,

    /// <summary>
    /// 浮点型。
    /// </summary>
    Float,

    /// <summary>
    /// 双精度浮点型。
    /// </summary>
    Double
}

/// <summary>
/// 基础配置模型。
/// </summary>
public sealed class BasicModel
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 基地。
    /// </summary>
    public string Site { get; set; } = string.Empty;

    /// <summary>
    /// 工厂。
    /// </summary>
    public string Factory { get; set; } = string.Empty;

    /// <summary>
    /// 区域。
    /// </summary>
    public string Area { get; set; } = string.Empty;

    /// <summary>
    /// 工序。
    /// </summary>
    public string Procedure { get; set; } = string.Empty;

    /// <summary>
    /// 设备编号。
    /// </summary>
    public string EquipmentNum { get; set; } = "000";

    /// <summary>
    /// 数据保存类型。
    /// </summary>
    public string DataType { get; set; } = "2";

    /// <summary>
    /// 资源号。
    /// </summary>
    public string ResourceNum { get; set; } = string.Empty;

    /// <summary>
    /// PLC 类型。
    /// </summary>
    public string PlcType { get; set; } = string.Empty;

    /// <summary>
    /// PLC IP 地址。
    /// </summary>
    public string PlcIp { get; set; } = string.Empty;

    /// <summary>
    /// PLC 端口。
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// 停机点位。
    /// </summary>
    public string ShutdownPoint { get; set; } = string.Empty;

    /// <summary>
    /// 西门子插槽号。
    /// </summary>
    public int SiemensSlot { get; set; }

    /// <summary>
    /// Modbus 字符串是否反转。
    /// </summary>
    public bool IsStringReverse { get; set; }

    /// <summary>
    /// 唯一业务键。
    /// </summary>
    public string UniqueKey => $"{Site}/{Factory}/{Area}/{Procedure}";
}

/// <summary>
/// 设备版本记录。
/// </summary>
public sealed class EquipentInVersion
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 资源号。
    /// </summary>
    public string ResourceNum { get; set; } = string.Empty;

    /// <summary>
    /// 版本号。
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 最后更新时间。
    /// </summary>
    public DateTime DateTime { get; set; } = DateTime.Now;
}

/// <summary>
/// 超限报警记录。
/// </summary>
public sealed class Exceedlimitinfo
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 易损件名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 当前值。
    /// </summary>
    public double CurrentValue { get; set; }

    /// <summary>
    /// 停机值。
    /// </summary>
    public double ShutdownValue { get; set; }

    /// <summary>
    /// 报警时间。
    /// </summary>
    public DateTime DateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 对应的基础配置主键。
    /// </summary>
    public string BasicId { get; set; } = string.Empty;
}

/// <summary>
/// 条码更换记录。
/// </summary>
public sealed class ReplaceRecordModel
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 基础配置主键。
    /// </summary>
    public string BasicModelId { get; set; } = string.Empty;

    /// <summary>
    /// 基地。
    /// </summary>
    public string Site { get; set; } = string.Empty;

    /// <summary>
    /// 易损件名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 旧条码。
    /// </summary>
    public string? OldNo { get; set; }

    /// <summary>
    /// 新条码。
    /// </summary>
    public string NewNo { get; set; } = string.Empty;

    /// <summary>
    /// 当前值。
    /// </summary>
    public string CurrentValue { get; set; } = string.Empty;

    /// <summary>
    /// 预警值。
    /// </summary>
    public string WarnValue { get; set; } = string.Empty;

    /// <summary>
    /// 停机值。
    /// </summary>
    public string ShutdownValue { get; set; } = string.Empty;

    /// <summary>
    /// 操作工号。
    /// </summary>
    public string OperatorNo { get; set; } = string.Empty;

    /// <summary>
    /// 操作人名称。
    /// </summary>
    public string OperatorUser { get; set; } = string.Empty;

    /// <summary>
    /// 更换说明。
    /// </summary>
    public string ReplaceMessage { get; set; } = string.Empty;

    /// <summary>
    /// 操作时间。
    /// </summary>
    public DateTime DateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 数据类型。
    /// </summary>
    public EDataType? DataType { get; set; }

    /// <summary>
    /// 业务扩展数据。
    /// </summary>
    public string? DataValue { get; set; }
}

/// <summary>
/// 工具更换记录。
/// </summary>
public sealed class ToolChange
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 名称。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 编码。
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public string CreatTime { get; set; } = string.Empty;
}

/// <summary>
/// 资源号对应的用户信息缓存。
/// </summary>
public sealed class UserInfoByResourceId
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 资源号。
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// MHR 返回结果。
    /// </summary>
    public HMRResult MhrResult { get; set; } = new();

    /// <summary>
    /// 最后更新时间。
    /// </summary>
    public DateTime LastUpdateDate { get; set; } = DateTime.Now;
}

/// <summary>
/// 版本号模型。
/// </summary>
public sealed class VersionModel
{
    /// <summary>
    /// 版本字符串。
    /// </summary>
    public string V { get; set; } = string.Empty;
}

/// <summary>
/// 易损件定义模型。
/// </summary>
public sealed class VulnerablePartsModel
{
    /// <summary>
    /// 主键。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 基础配置主键。
    /// </summary>
    public string BasicModelId { get; set; } = string.Empty;

    /// <summary>
    /// 资源号。
    /// </summary>
    public string ResourceNum { get; set; } = string.Empty;

    /// <summary>
    /// 易损件名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 输入方式。
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// 当前值点位。
    /// </summary>
    public string CurrentValuePoint { get; set; } = string.Empty;

    /// <summary>
    /// 当前值点位类型。
    /// </summary>
    public string CurrentValueDataType { get; set; } = string.Empty;

    /// <summary>
    /// 预警值点位。
    /// </summary>
    public string WarnValuePoint { get; set; } = string.Empty;

    /// <summary>
    /// 预警值点位类型。
    /// </summary>
    public string WarnValueDataType { get; set; } = string.Empty;

    /// <summary>
    /// 停机值点位。
    /// </summary>
    public string ShutdownValuePoint { get; set; } = string.Empty;

    /// <summary>
    /// 停机值点位类型。
    /// </summary>
    public string ShutdownValueDataType { get; set; } = string.Empty;

    /// <summary>
    /// 到期是否允许停机。
    /// </summary>
    public bool IsShutdown { get; set; }

    /// <summary>
    /// 条码最低长度。
    /// </summary>
    public int CodeMinLength { get; set; }

    /// <summary>
    /// 条码最长长度。
    /// </summary>
    public int CodeMaxLength { get; set; }

    /// <summary>
    /// 寿命类型。
    /// </summary>
    public string LifeType { get; set; } = string.Empty;

    /// <summary>
    /// PLC 清零点位。
    /// </summary>
    public string PlcZeroClear { get; set; } = string.Empty;

    /// <summary>
    /// 新条码写入 PLC 的地址。
    /// </summary>
    public string CodeWritePlcPoint { get; set; } = string.Empty;

    /// <summary>
    /// 最后更新时间。
    /// </summary>
    public DateTime? DateTime { get; set; } = System.DateTime.Now;
}
