# PartServices 模型迁移映射（VulnerablePartsSys -> WearPartsControl）

本文档用于后续数据库迁移时快速对照旧模型与新模型，包含类型重命名、主外键类型调整与字段语义变化。

## 1. 类型映射

| 旧类型（VulnerablePartsSys/Model） | 新类型（PartServices） | 说明 |
| --- | --- | --- |
| `BaseFactoryModel` | `SiteFactoryMapping` | 基地工厂映射，工厂列表改为数组字段 `FactoryCodes` |
| `BasicModel` | `BasicConfiguration` | 基础配置主模型 |
| `VulnerablePartsModel` | `WearPartDefinition` | 易损件定义 |
| `ReplaceRecordModel` | `WearPartReplacementRecord` | 易损件更换记录 |
| `Exceedlimitinfo` | `ExceedLimitRecord` | 超限记录 |
| `EquipentInVersion` | `EquipmentVersionRecord` | 设备版本记录 |
| `ToolChange` | `ToolChangeRecord` | 刀具变更记录 |
| `UserInfoByResourceId` | `ResourceUserSnapshot` | 资源号用户快照 |
| `MHR` | `MhrApiSettings` | MHR 接口配置 |
| `HMRResult` | `MhrResult` | MHR 返回对象 |
| `HTMItemData` | `MhrData` | MHR 数据载荷 |
| `UserModel` | `MhrUser` | MHR 用户实体 |
| `VersionModel` | `VersionInfo` | 版本信息 |
| `AppSetting` | `AppSettings` | 本地应用配置 |
| `MysqlStr` | `MySqlConnectionSettings` | MySQL 配置 |
| `EDataType` | `PartDataType` | 数据类型枚举 |

## 2. 关键字段映射

### 2.1 BasicConfiguration（原 BasicModel）

| 旧字段 | 新字段 | 类型变化 |
| --- | --- | --- |
| `Id` | `Id` | `string` -> `Guid` |
| `Site` | `SiteCode` | `string` -> `string` |
| `Factory` | `FactoryCode` | `string` -> `string` |
| `Area` | `AreaCode` | `string` -> `string` |
| `Procedure` | `ProcedureCode` | `string` -> `string` |
| `EquipmentNum` | `EquipmentCode` | `string` -> `string` |
| `DataType` | `DataStorageTypeCode` | `string` -> `string` |
| `ResourceNum` | `ResourceNumber` | `string` -> `string` |
| `PlcType` | `PlcProtocolType` | `string` -> `string` |
| `PlcIp` | `PlcIpAddress` | `string` -> `string` |
| `Port` | `PlcPort` | `int` -> `int` |
| `ShutdownPoint` | `ShutdownPointAddress` | `string` -> `string` |

### 2.2 WearPartDefinition（原 VulnerablePartsModel）

| 旧字段 | 新字段 | 类型变化 |
| --- | --- | --- |
| `Id` | `Id` | `string` -> `Guid` |
| `BasicModelId` | `BasicConfigurationId` | `string` -> `Guid` |
| `ResourceNum` | `ResourceNumber` | `string` -> `string` |
| `Name` | `PartName` | `string` -> `string` |
| `Input` | `InputMode` | `string` -> `string` |
| `CurrentValuePoint` | `CurrentValueAddress` | `string` -> `string` |
| `WarnValuePoint` | `WarningValueAddress` | `string` -> `string` |
| `WarnValueDataType` | `WarningValueDataType` | `string` -> `string` |
| `ShutdownValuePoint` | `ShutdownValueAddress` | `string` -> `string` |
| `LifeType` | `LifetimeType` | `string` -> `string` |
| `PlcZeroClear` | `PlcZeroClearAddress` | `string` -> `string` |
| `CodeWritePlcPoint` | `BarcodeWriteAddress` | `string` -> `string` |
| `DateTime` | `UpdatedAt` | `DateTime?` -> `DateTime?` |

### 2.3 WearPartReplacementRecord（原 ReplaceRecordModel）

| 旧字段 | 新字段 | 类型变化 |
| --- | --- | --- |
| `Id` | `Id` | `string` -> `Guid` |
| `BasicModelId` | `BasicConfigurationId` | `string` -> `Guid` |
| `Site` | `SiteCode` | `string` -> `string` |
| `Name` | `PartName` | `string` -> `string` |
| `OldNo` | `OldBarcode` | `string?` -> `string?` |
| `NewNo` | `NewBarcode` | `string` -> `string` |
| `WarnValue` | `WarningValue` | `string` -> `string` |
| `OperatorNo` | `OperatorWorkNumber` | `string` -> `string` |
| `OperatorUser` | `OperatorUserName` | `string` -> `string` |
| `ReplaceMessage` | `ReplacementMessage` | `string` -> `string` |
| `DateTime` | `ReplacedAt` | `DateTime` -> `DateTime` |
| `DataType` | `DataType` | `EDataType?` -> `PartDataType?` |

### 2.4 ExceedLimitRecord（原 Exceedlimitinfo）

| 旧字段 | 新字段 | 类型变化 |
| --- | --- | --- |
| `Id` | `Id` | `string` -> `Guid` |
| `Name` | `PartName` | `string` -> `string` |
| `DateTime` | `OccurredAt` | `DateTime` -> `DateTime` |
| `BasicId` | `BasicConfigurationId` | `string` -> `Guid` |

### 2.5 EquipmentVersionRecord（原 EquipentInVersion）

| 旧字段 | 新字段 | 类型变化 |
| --- | --- | --- |
| `Id` | `Id` | `string` -> `Guid` |
| `ResourceNum` | `ResourceNumber` | `string` -> `string` |
| `DateTime` | `UpdatedAt` | `DateTime` -> `DateTime` |

### 2.6 ToolChangeRecord（原 ToolChange）

| 旧字段 | 新字段 | 类型变化 |
| --- | --- | --- |
| `Id` | `Id` | `string` -> `Guid` |
| `Name` | `ToolName` | `string?` -> `string?` |
| `Code` | `ToolCode` | `string?` -> `string?` |
| `CreatTime` | `CreatedAt` | `string` -> `DateTime` |

### 2.7 ResourceUserSnapshot（原 UserInfoByResourceId）

| 旧字段 | 新字段 | 类型变化 |
| --- | --- | --- |
| `Id` | `Id` | `string` -> `Guid` |
| `ResourceId` | `ResourceNumber` | `string` -> `string` |
| `MhrResult` | `MhrResult` | `HMRResult` -> `MhrResult` |

## 3. 迁移注意事项

- 旧库中保存为字符串的 Guid 字段，在迁移脚本中建议先做合法性校验，再转换为数据库 `uniqueidentifier`。
- `ToolChange.CreatTime` 为字符串，迁移到 `CreatedAt` 时需明确源字符串格式并做失败兜底（如默认当前时间或空值策略）。
- `SiteFactoryOptionsSaveInfo` 使用 `JsonPropertyName("Factories")` 兼容旧 JSON 结构，避免配置文件强制改版。