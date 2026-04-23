# PartServices 模型迁移映射（VulnerablePartsSys -> WearPartsControl）

本文档记录当前保留的迁移映射。随着领域模型稳定，客户端配置已收敛到 Domain/Infrastructure，PartServices 中仅保留直接参与应用服务编排的 DTO 与服务类型。

## 1. 类型映射

| 旧类型（VulnerablePartsSys/Model） | 新类型 | 说明 |
| --- | --- | --- |
| `BasicModel` | `ClientAppConfigurationEntity` | 客户端软件基础配置实体，现位于 Domain |
| `VulnerablePartsModel` | `WearPartDefinition` | 易损件定义 DTO |
| `ReplaceRecordModel` | `WearPartReplacementRecord` | 易损件更换记录 DTO |
| `Exceedlimitinfo` | `ExceedLimitRecord` | 超限记录 DTO |
| `AppSetting` | `AppSettings` | 本地应用配置 |
| `EDataType` | `PartDataType` | 数据类型枚举 |
| `UserModel` | `MhrUser` | 登录用户对象 |

以下旧迁移类型已删除，不再在 PartServices 中保留镜像模型：

- `BaseFactoryOptionsSaveInfo`
- `BasicModel`
- `EquipentInVersion`
- `HMRResult`
- `HTMItemData`
- `IPartModelService`
- `MHR`
- `MysqlStr`
- `PartModelService`
- `SiteFactory`
- `ToolChange`
- `UserInfoByResourceId`
- `VersionModel`

## 2. 关键字段映射

### 2.1 ClientAppConfiguration（原 BasicModel）

| 旧字段 | 新字段 | 类型变化 |
| --- | --- | --- |
| `Id` | `Id` | `string` -> `Guid` |
| `Site` | `SiteCode` | `string` -> `string` |
| `Factory` | `FactoryCode` | `string` -> `string` |
| `Area` | `AreaCode` | `string` -> `string` |
| `Procedure` | `ProcedureCode` | `string` -> `string` |
| `EquipmentNum` | `EquipmentCode` | `string` -> `string` |
| `ResourceNum` | `ResourceNumber` | `string` -> `string` |
| `PlcType` | `PlcProtocolType` | `string` -> `string` |
| `PlcIp` | `PlcIpAddress` | `string` -> `string` |
| `Port` | `PlcPort` | `int` -> `int` |
| `ShutdownPoint` | `ShutdownPointAddress` | `string` -> `string` |

### 2.2 WearPartDefinition（原 VulnerablePartsModel）

| 旧字段 | 新字段 | 类型变化 |
| --- | --- | --- |
| `Id` | `Id` | `string` -> `Guid` |
| `BasicModelId` | `ClientAppConfigurationId` | `string` -> `Guid` |
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
| `BasicModelId` | `ClientAppConfigurationId` | `string` -> `Guid` |
| `Site` | `SiteCode` | `string` -> `string` |
| `Name` | `PartName` | `string` -> `string` |
| `OldNo` | `CurrentBarcode` | `string?` -> `string?` |
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
| `BasicId` | `ClientAppConfigurationId` | `string` -> `Guid` |

## 3. 迁移注意事项

- 旧库中保存为字符串的 Guid 字段，在迁移脚本中建议先做合法性校验，再转换为数据库 `uniqueidentifier`。
- 客户端配置不再在 PartServices 中保留副本类型，迁移时应直接落到 `ClientAppConfigurationEntity`。