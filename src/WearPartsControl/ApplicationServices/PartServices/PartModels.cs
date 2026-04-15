using System;
using System.Collections.Generic;
using WearPartsControl.ApplicationServices.SaveInfoService;

namespace WearPartsControl.ApplicationServices.PartServices;

[SaveInfoFile("settings/app-setting")]
public sealed class AppSetting
{
    public string ResourceNum { get; set; } = string.Empty;
}

[SaveInfoFile("settings/mysql")]
public sealed class MysqlStr
{
    public string ConnectString { get; set; } = string.Empty;
}

[SaveInfoFile("settings/mhr")]
public sealed class MHR
{
    public string GetTokenUrl { get; set; } = string.Empty;

    public string LoginName { get; set; } = string.Empty;

    public string LoginPassword { get; set; } = string.Empty;

    public string GetListUrl { get; set; } = string.Empty;

    public int UpdateDate { get; set; }
}

public sealed class HMRResult
{
    public bool Success { get; set; }

    public int ErrorCode { get; set; }

    public string Msg { get; set; } = string.Empty;

    public HTMItemData Data { get; set; } = new();
}

public sealed class HTMItemData
{
    public long timestamp { get; set; }

    public List<UserModel> list { get; set; } = new();

    public string device_resource_id { get; set; } = string.Empty;
}

public sealed class UserModel
{
    public string work_id { get; set; } = string.Empty;

    public int access_level { get; set; }

    public string card_id { get; set; } = string.Empty;
}

public enum EDataType
{
    Json,
    String,
    Int,
    Float,
    Double
}

public sealed class BasicModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Site { get; set; } = string.Empty;

    public string Factory { get; set; } = string.Empty;

    public string Area { get; set; } = string.Empty;

    public string Procedure { get; set; } = string.Empty;

    public string EquipmentNum { get; set; } = "000";

    public string DataType { get; set; } = "2";

    public string ResourceNum { get; set; } = string.Empty;

    public string PlcType { get; set; } = string.Empty;

    public string PlcIp { get; set; } = string.Empty;

    public int Port { get; set; }

    public string ShutdownPoint { get; set; } = string.Empty;

    public int SiemensSlot { get; set; }

    public bool IsStringReverse { get; set; }

    public string UniqueKey => $"{Site}/{Factory}/{Area}/{Procedure}";
}

public sealed class EquipentInVersion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ResourceNum { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public DateTime DateTime { get; set; } = DateTime.Now;
}

public sealed class Exceedlimitinfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    public double CurrentValue { get; set; }

    public double ShutdownValue { get; set; }

    public DateTime DateTime { get; set; } = DateTime.Now;

    public string BasicId { get; set; } = string.Empty;
}

public sealed class ReplaceRecordModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string BasicModelId { get; set; } = string.Empty;

    public string Site { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? OldNo { get; set; }

    public string NewNo { get; set; } = string.Empty;

    public string CurrentValue { get; set; } = string.Empty;

    public string WarnValue { get; set; } = string.Empty;

    public string ShutdownValue { get; set; } = string.Empty;

    public string OperatorNo { get; set; } = string.Empty;

    public string OperatorUser { get; set; } = string.Empty;

    public string ReplaceMessage { get; set; } = string.Empty;

    public DateTime DateTime { get; set; } = DateTime.Now;

    public EDataType? DataType { get; set; }

    public string? DataValue { get; set; }
}

public sealed class ToolChange
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string? Name { get; set; }

    public string? Code { get; set; }

    public string CreatTime { get; set; } = string.Empty;
}

public sealed class UserInfoByResourceId
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ResourceId { get; set; } = string.Empty;

    public HMRResult MhrResult { get; set; } = new();

    public DateTime LastUpdateDate { get; set; } = DateTime.Now;
}

public sealed class VersionModel
{
    public string V { get; set; } = string.Empty;
}

public sealed class VulnerablePartsModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string BasicModelId { get; set; } = string.Empty;

    public string ResourceNum { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Input { get; set; } = string.Empty;

    public string CurrentValuePoint { get; set; } = string.Empty;

    public string CurrentValueDataType { get; set; } = string.Empty;

    public string WarnValuePoint { get; set; } = string.Empty;

    public string WarnValueDataType { get; set; } = string.Empty;

    public string ShutdownValuePoint { get; set; } = string.Empty;

    public string ShutdownValueDataType { get; set; } = string.Empty;

    public bool IsShutdown { get; set; }

    public int CodeMinLength { get; set; }

    public int CodeMaxLength { get; set; }

    public string LifeType { get; set; } = string.Empty;

    public string PlcZeroClear { get; set; } = string.Empty;

    public string CodeWritePlcPoint { get; set; } = string.Empty;

    public DateTime? DateTime { get; set; } = System.DateTime.Now;
}
