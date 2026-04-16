# PartServices 模型依赖关系

本文档描述当前 `ApplicationServices/PartServices` 中保留类型的依赖关系，不再覆盖已删除的旧迁移模型。

## 1. 关系总览

- `ClientAppConfiguration` 是主配置模型。
- `WearPartDefinition` 通过 `ClientAppConfigurationId` 依赖 `ClientAppConfiguration.Id`。
- `WearPartReplacementRecord` 通过 `ClientAppConfigurationId` 依赖 `ClientAppConfiguration.Id`。
- `ExceedLimitRecord` 通过 `ClientAppConfigurationId` 依赖 `ClientAppConfiguration.Id`。

## 2. 详细关系

### ClientAppConfiguration (`v_Basic`)

主数据：站点、工厂、工序、PLC 通信配置、资源号等。

被下列表模型引用：
- `WearPartDefinition.ClientAppConfigurationId`
- `WearPartReplacementRecord.ClientAppConfigurationId`
- `ExceedLimitRecord.ClientAppConfigurationId`

### WearPartDefinition (`v_VulnerableParts`)

易损件定义，包含点位、数据类型、寿命类型和条码约束。

关键外键语义：
- `ClientAppConfigurationId -> ClientAppConfiguration.Id`

业务耦合字段：
- `ResourceNumber` 一般与 `ClientAppConfiguration.ResourceNumber` 对齐
- `PartName` 被更换记录与超限记录复用

### WearPartReplacementRecord (`v_ReplaceRecord`)

条码更换历史。

关键外键语义：
- `ClientAppConfigurationId -> ClientAppConfiguration.Id`

业务耦合字段：
- `PartName` 对应 `WearPartDefinition.PartName`
- `SiteCode` 通常来自 `ClientAppConfiguration.SiteCode`

### ExceedLimitRecord (`v_exceedlimitinfo`)

易损件超限记录。

关键外键语义：
- `ClientAppConfigurationId -> ClientAppConfiguration.Id`

业务耦合字段：
- `PartName` 对应 `WearPartDefinition.PartName`

## 3. 配置模型（非表）

下列模型是配置或接口数据，不属于业务表：
- `AppSettings`
- `MhrUser`
- `PartDataType`
- `WearPartReplacementPreview`
- `WearPartReplacementRequest`
- `WearPartMonitorResult`
- `WearPartMonitorStatus`

## 4. 建议

- 后续数据库迁移继续以 `ClientAppConfiguration` 为根，保持应用层与领域层命名一致。
- 对 `ResourceNumber` 相关业务关联可考虑增加唯一索引与外键约束，减少脏数据。