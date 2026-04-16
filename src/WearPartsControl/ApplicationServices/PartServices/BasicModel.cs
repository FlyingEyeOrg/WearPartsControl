using System;
using System.Collections.Generic;

namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 基础配置模型。
/// </summary>
public sealed class BasicConfiguration
{
    /// <summary>
    /// 主键。
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 基地。
    /// </summary>
    public string SiteCode { get; set; } = string.Empty;

    /// <summary>
    /// 工厂。
    /// </summary>
    public string FactoryCode { get; set; } = string.Empty;

    /// <summary>
    /// 区域。
    /// </summary>
    public string AreaCode { get; set; } = string.Empty;

    /// <summary>
    /// 工序。
    /// </summary>
    public string ProcedureCode { get; set; } = string.Empty;

    /// <summary>
    /// 设备编号。
    /// </summary>
    public string EquipmentCode { get; set; } = "000";

    /// <summary>
    /// 数据保存类型。
    /// </summary>
    public string DataStorageTypeCode { get; set; } = "2";

    /// <summary>
    /// 资源号。
    /// </summary>
    public string ResourceNumber { get; set; } = string.Empty;

    /// <summary>
    /// PLC 类型。
    /// </summary>
    public string PlcProtocolType { get; set; } = string.Empty;

    /// <summary>
    /// PLC IP 地址。
    /// </summary>
    public string PlcIpAddress { get; set; } = string.Empty;

    /// <summary>
    /// PLC 端口。
    /// </summary>
    public int PlcPort { get; set; }

    /// <summary>
    /// 停机点位。
    /// </summary>
    public string ShutdownPointAddress { get; set; } = string.Empty;

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
    public string UniqueKey => $"{SiteCode}/{FactoryCode}/{AreaCode}/{ProcedureCode}";
}
