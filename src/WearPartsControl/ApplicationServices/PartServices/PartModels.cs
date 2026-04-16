using System;
using System.Collections.Generic;

namespace WearPartsControl.ApplicationServices.PartServices;

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
