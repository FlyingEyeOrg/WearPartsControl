# PartServices 模型依赖关系

本文档描述从旧系统 `Model` 迁移到 `ApplicationServices/PartServices` 的模型间依赖，不涉及数据库迁移。

## 1. 关系总览

- `BasicConfiguration` 是主配置表模型。
- `WearPartDefinition` 通过 `BasicConfigurationId` 依赖 `BasicConfiguration.Id`。
- `WearPartReplacementRecord` 通过 `BasicConfigurationId` 依赖 `BasicConfiguration.Id`。
- `ExceedLimitRecord` 通过 `BasicConfigurationId` 依赖 `BasicConfiguration.Id`。
- `EquipmentVersionRecord` 通过 `ResourceNumber` 关联 `BasicConfiguration.ResourceNumber`（业务关联）。
- `ResourceUserSnapshot` 通过 `ResourceNumber` 关联 `BasicConfiguration.ResourceNumber`（业务关联）。
- `ToolChangeRecord` 当前为独立表模型。

## 2. 详细关系

### BasicConfiguration (`v_Basic`)

主数据：站点、工厂、工序、PLC 通信配置、资源号等。

被下列表模型引用：
- `WearPartDefinition.BasicConfigurationId`
- `WearPartReplacementRecord.BasicConfigurationId`
- `ExceedLimitRecord.BasicConfigurationId`

### WearPartDefinition (`v_VulnerableParts`)

易损件定义（点位、数据类型、寿命类型、条码约束）。

关键外键语义：
- `BasicConfigurationId -> BasicConfiguration.Id`

业务耦合字段（无强外键）：
- `ResourceNumber` 一般与 `BasicConfiguration.ResourceNumber` 对齐
- `PartName` 常被更换记录与超限记录复用

### WearPartReplacementRecord (`v_ReplaceRecord`)

条码更换历史。

关键外键语义：
- `BasicConfigurationId -> BasicConfiguration.Id`

业务耦合字段：
- `PartName` 对应 `WearPartDefinition.PartName`
- `SiteCode` 通常来自 `BasicConfiguration.SiteCode`

### ExceedLimitRecord (`v_exceedlimitinfo`)

易损件超限记录。

关键外键语义：
- `BasicConfigurationId -> BasicConfiguration.Id`

业务耦合字段：
- `PartName` 对应 `WearPartDefinition.PartName`

### EquipmentVersionRecord (`v_equipentInVersion`)

设备版本记录。

业务关联：
- `ResourceNumber -> BasicConfiguration.ResourceNumber`

### ResourceUserSnapshot (`v_UserInfoByResourceId`)

资源号对应的人机权限快照。

业务关联：
- `ResourceNumber -> BasicConfiguration.ResourceNumber`

### ToolChangeRecord (`ToolChange`)

刀具变更记录，目前独立。

## 3. 配置模型（非表）

下列模型是配置/接口数据，不属于业务表：
- `SiteFactoryMapping` + `SiteFactoryOptionsSaveInfo`（基地工厂映射配置，一个基地下的多个 `FactoryCodes` 用数组保存）
- `AppSettings`
- `MySqlConnectionSettings`
- `MhrApiSettings` / `MhrResult` / `MhrData` / `MhrUser`
- `VersionInfo`

## 4. 建议

- 后续若执行数据库迁移，优先为 `WearPartDefinition`、`WearPartReplacementRecord`、`ExceedLimitRecord` 到 `BasicConfiguration` 增加明确外键。
- 对 `ResourceNumber` 相关业务关联可考虑增加唯一索引与外键约束，减少脏数据。