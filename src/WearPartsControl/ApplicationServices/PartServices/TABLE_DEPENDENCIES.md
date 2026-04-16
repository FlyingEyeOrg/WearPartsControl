# PartServices 模型依赖关系

本文档描述从旧系统 `Model` 迁移到 `ApplicationServices/PartServices` 的模型间依赖，不涉及数据库迁移。

## 1. 关系总览

- `BasicModel` 是主配置表模型。
- `VulnerablePartsModel` 通过 `BasicModelId` 依赖 `BasicModel.Id`。
- `ReplaceRecordModel` 通过 `BasicModelId` 依赖 `BasicModel.Id`。
- `Exceedlimitinfo` 通过 `BasicId` 依赖 `BasicModel.Id`。
- `EquipentInVersion` 通过 `ResourceNum` 依赖 `BasicModel.ResourceNum`（业务关联）。
- `UserInfoByResourceId` 通过 `ResourceId` 依赖 `BasicModel.ResourceNum`（业务关联）。
- `ToolChange` 当前为独立表模型。

## 2. 详细关系

### BasicModel (`v_Basic`)

主数据：站点、工厂、工序、PLC 通信配置、资源号等。

被下列表模型引用：
- `VulnerablePartsModel.BasicModelId`
- `ReplaceRecordModel.BasicModelId`
- `Exceedlimitinfo.BasicId`

### VulnerablePartsModel (`v_VulnerableParts`)

易损件定义（点位、数据类型、寿命类型、条码约束）。

关键外键语义：
- `BasicModelId -> BasicModel.Id`

业务耦合字段（无强外键）：
- `ResourceNum` 一般与 `BasicModel.ResourceNum` 对齐
- `Name` 常被更换记录与超限记录复用

### ReplaceRecordModel (`v_ReplaceRecord`)

条码更换历史。

关键外键语义：
- `BasicModelId -> BasicModel.Id`

业务耦合字段：
- `Name` 对应 `VulnerablePartsModel.Name`
- `Site` 通常来自 `BasicModel.Site`

### Exceedlimitinfo (`v_exceedlimitinfo`)

易损件超限记录。

关键外键语义：
- `BasicId -> BasicModel.Id`

业务耦合字段：
- `Name` 对应 `VulnerablePartsModel.Name`

### EquipentInVersion (`v_equipentInVersion`)

设备版本记录。

业务关联：
- `ResourceNum -> BasicModel.ResourceNum`

### UserInfoByResourceId (`v_UserInfoByResourceId`)

资源号对应的人机权限快照。

业务关联：
- `ResourceId -> BasicModel.ResourceNum`

### ToolChange (`ToolChange`)

刀具变更记录，目前独立。

## 3. 配置模型（非表）

下列模型是配置/接口数据，不属于业务表：
- `SiteFactoryModel` + `SiteFactoryOptionsSaveInfo`（基地工厂映射配置，按 `Base` 和 `BaseName` 分组，一个基地下的多个 `FactoryNames` 用数组保存）
- `AppSetting`
- `MysqlStr`
- `MHR` / `HMRResult` / `HTMItemData` / `UserModel`
- `VersionModel`

## 4. 建议

- 后续若执行数据库迁移，优先为 `VulnerablePartsModel`、`ReplaceRecordModel`、`Exceedlimitinfo` 到 `BasicModel` 增加明确外键。
- 对 `ResourceNum` 相关业务关联可考虑增加唯一索引与外键约束，减少脏数据。