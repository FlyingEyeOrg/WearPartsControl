# VulnerablePartsSys 业务与数据库迁移分析

## 0. 扫描范围

- 源项目：`E:\Projects\DotNet\VulnerablePartsSys\VulnerablePartsSys`
- 重点业务代码：
  - `Common/DbHelper.cs`
  - `Bll/VulnerablePartBLL.cs`
  - `Bll/UserListBll.cs`
  - `FrmBasic.cs`
  - `FrmMain.cs`
  - `FrmVulnerableParts.cs`
  - `FrmVulnerablePartsAdd.cs`
  - `Home.cs`
  - `FrmHistoryReplaceCode.cs`
  - `FrmToolChange.cs`
  - `FrmLogin.cs`
  - `UserConfigForm.cs`

说明：数据库访问以 SqlSugar 为核心，连接模式支持 MySQL（远程）与 SQLite（本地）。

---

## 1. 所有业务逻辑梳理

## 1.1 系统启动与初始化

业务入口：`FrmMain.Load` + `DbHelper.DbInit`

核心逻辑：
- 初始化数据库结构（CodeFirst）
- 读取本地 `AppSetting.ResourceNum`
- 按资源号加载 `BasicModel`
- 同步设备版本 `EquipentInVersion`
- 初始化 PLC 连接
- 启动后台易损件寿命监控与停机线程（`VulnerablePartBLL.ForcedShutdown`）


## 1.2 基础配置维护（基地/工厂/PLC/资源号）

业务入口：`FrmBasic`

核心逻辑：
- 加载并展示 `BasicModel` 配置
- 校验配置输入（必填、PLC参数）
- 按数据库类型执行 upsert：
  - SQLite 模式按 `Id`
  - 远程模式按 `ResourceNum`（存在性确认）
- 同步写入本地 `AppSetting.ResourceNum`


## 1.3 易损件定义维护

业务入口：`FrmVulnerableParts` + `FrmVulnerablePartsAdd`

核心逻辑：
- 查询当前机台（`BasicModelId`）的易损件定义
- 新增/编辑易损件定义（点位、数据类型、条码长度、寿命类型、清零地址等）
- 删除易损件定义
- 跨机台复制定义（按 `ResourceNum` 批量复制到当前机台）


## 1.4 易损件更换作业

业务入口：`Home` 页面扫码流程

核心逻辑：
- 选择易损件，实时读取 PLC 的当前值/预警值/停机值
- 按更换原因执行业务规则：
  - 寿命到期正常更换
  - 寿命到期更换位置
  - 寿命到期维保
  - 过程损坏 / 切拉换型
- 读取上一条更换记录，校验条码重复/复用
- 写 PLC（清零或写入寿命、写入新条码）
- 写入更换记录 `ReplaceRecordModel`


## 1.5 历史更换记录查询与导出

业务入口：`FrmHistoryReplaceCode`

核心逻辑：
- 按易损件名称 + 当前机台分页查询历史
- 导出 CSV（带基础位置信息 + 更换字段）


## 1.6 易损件寿命监控与停机告警

业务入口：`VulnerablePartBLL.ForcedShutdown`（后台循环）

核心逻辑：
- 周期读取所有易损件 PLC 当前值/阈值
- 超预警：内存去重后触发通知（群+个人）
- 超停机：
  - 查询最近超限记录
  - 每日一次入库 `Exceedlimitinfo`
  - 根据配置写停机点位
  - 发送停机通知


## 1.7 用户认证与 MHR 用户列表同步

业务入口：`FrmLogin` + `UserListBll`

核心逻辑：
- 从配置读取 MHR 登录信息
- 远程调用获取 token 与用户列表
- 用户卡号/工号认证
- 缓存用户列表，按更新时间复用
- 持久化 `UserInfoByResourceId`（资源号维度快照）


## 1.8 刀具变更管理（模切分条）

业务入口：`FrmToolChange` + `Home`

核心逻辑：
- 刀具主数据增删查
- 更换时校验刀具编码是否匹配条码
- 记录每个易损件对应选中的刀具类型（本地 SaveInfo）

---

## 2. 每个业务逻辑涉及到的数据库 CRUD

## 2.1 数据模型/表清单

- `v_Basic` (`BasicModel`)
- `v_VulnerableParts` (`VulnerablePartsModel`)
- `v_ReplaceRecord` (`ReplaceRecordModel`)
- `v_exceedlimitinfo` (`Exceedlimitinfo`)
- `v_equipentInVersion` (`EquipentInVersion`)
- `v_UserInfoByResourceId` (`UserInfoByResourceId`)
- `ToolChange` (`ToolChange`)
- `VersionModel`（本地版本表）


## 2.2 CRUD 对照表（按业务）

| 业务 | 读取（R） | 新增（C） | 更新（U） | 删除（D） |
| --- | --- | --- | --- | --- |
| 系统初始化 | `Queryable<VersionModel>().Single()`；`Queryable<BasicModel>().Single(ResourceNum)`；`Queryable<EquipentInVersion>().First(ResourceNum)` | `Insertable<VersionModel>` | `Updateable<VersionModel>`；`Storageable<EquipentInVersion>.WhereColumns(ResourceNum)` | 无 |
| 基础配置维护 | `Queryable<BasicModel>().Any(ResourceNum)` | `Storageable<BasicModel>`（不存在时插入） | `Storageable<BasicModel>`（SQLite: Id，远程: ResourceNum） | 无 |
| 易损件定义维护 | `Queryable<VulnerablePartsModel>().Where(BasicModelId/ResourceNum)`；`Single(Id)` | `Storageable<VulnerablePartsModel>`（新增时）; `Insertable<List<VulnerablePartsModel>>`（复制） | `Storageable<VulnerablePartsModel>.WhereColumns(Id)` | `Deleteable<VulnerablePartsModel>.Where(Id)` |
| 更换作业 | `Queryable<ReplaceRecordModel>().Where(...).First()`；`Queryable<ReplaceRecordModel>().ToPageList()` | `Insertable<ReplaceRecordModel>` | 无 | 无 |
| 历史查询导出 | `Queryable<ReplaceRecordModel>().Where(...).ToPageList/ToList` | 无 | 无 | 无 |
| 寿命监控与停机 | `Queryable<VulnerablePartsModel>().Where(BasicModelId)`；`Queryable<Exceedlimitinfo>().Where(...).First()` | `Insertable<Exceedlimitinfo>` | 无 | 无 |
| 用户认证同步 | 无（用户列表来自 HTTP） | `Storageable<UserInfoByResourceId>`（不存在时插入） | `Storageable<UserInfoByResourceId>.WhereColumns(ResourceId)` | 无 |
| 刀具变更管理 | `Queryable<ToolChange>().Any/ToList` | `Insertable<ToolChange>` | 无 | `Deleteable<ToolChange>.Where(Id)` |


## 2.3 关键事务/一致性风险点

当前旧系统多数写操作未显式事务化，存在以下风险：
- 更换流程：PLC 写成功后数据库写失败（或反向）导致状态不一致
- 寿命监控：并发循环与人工操作可能重复写超限记录
- 跨机台复制定义：批量插入失败缺少补偿

---

## 3. 迁移到当前项目（WearPartsControl）的方案

目标：业务不丢失 + 架构优化 + 可持续扩展。

## 3.1 分层迁移原则

建议按当前项目结构落地：
- `Domain/Entities`：数据库实体（你已建立）
- `Infrastructure`：`WearPartsControlDbContext` + 映射 + UoW
- `ApplicationServices`：业务服务（替代 WinForm 事件代码）
- `ViewModels`/UI：仅做交互，不承载业务规则


## 3.2 业务服务拆分建议

建议建立以下服务（按旧业务一一对应）：
- `BasicConfigurationService`
  - 加载/保存机台基础配置
  - 资源号唯一性校验
- `WearPartDefinitionService`
  - 易损件定义 CRUD
  - 跨资源号复制
- `WearPartReplacementService`
  - 条码更换主流程
  - 更换原因规则引擎
  - PLC 写入与记录写库的一致性处理
- `WearPartMonitorService`
  - 后台轮询
  - 预警去重策略（建议从内存迁移到可持久状态）
  - 超限记录写库与通知
- `ToolChangeService`
  - 刀具主数据 CRUD
  - 与模切分条规则联动
- `UserAuthorizationService`
  - MHR 登录与缓存
  - 用户快照入库


## 3.3 数据库迁移设计建议

- 主外键统一改 `Guid`（你当前项目已开始执行）
- 统一时间字段：`CreatedAt` / `UpdatedAt` / `OccurredAt`
- 为高频查询建立索引：
  - `WearPartDefinition(ClientAppConfigurationId, PartName)`
  - `WearPartReplacementRecord(ClientAppConfigurationId, ReplacedAt DESC)`
  - `ExceedLimitRecord(ClientAppConfigurationId, PartName, OccurredAt DESC)`
  - `EquipmentVersionRecord(ResourceNumber)`（唯一）


## 3.4 业务不丢失迁移清单（必须保留）

- 更换原因规则完整迁移（4种原因 + 条件分支）
- “相同条码重复使用”与“寿命到期更换位置”校验逻辑
- PLC 特殊地址 `######` 与否定停机点 `!` 规则
- 涂布 A/B 面校验与失败锁机流程
- 模切分条刀具编码匹配校验
- 预警通知“每天一次”限制
- 超停机记录“每天一条”限制


## 3.5 推荐优化（迁移时同步做）

- 将业务规则从 UI 事件抽离成可测试的纯服务
- `PLC 写入 + DB 写入` 使用事务边界与补偿策略：
  - 优先写 PLC，DB 持久化失败时记录补偿任务
  - 或引入 outbox 事件记录
- 将 MHR 用户缓存从全局变量迁移为缓存服务（带过期时间）
- 统一异常模型（当前项目已有 `BusinessException` / `UserFriendlyException`）
- 所有文本接入本地化键（当前项目已有 Localization 基础）


## 3.6 分阶段迁移落地路线

1. **实体与表结构阶段**：完成 `Domain` + `DbContext` + 索引/约束
2. **读服务阶段**：先迁移查询类逻辑（历史、列表、配置加载）
3. **写服务阶段**：迁移定义维护、更换记录、刀具管理
4. **高风险流程阶段**：迁移更换作业主流程、寿命监控线程
5. **切换与验收阶段**：双跑校验（旧系统与新系统对账）后切换


## 3.7 验收标准（建议）

- 同一组输入下，新旧系统产生的 PLC 写入行为一致
- 更换记录、超限记录、用户快照数量与字段值对齐
- 关键查询（定义列表/历史记录）结果一致
- 迁移后 1 周内无“漏记记录/重复停机/误告警”问题

---

## 4. 当前项目中的落地位置建议

可将本文档作为迁移总说明，配合以下目录推进：
- `src/WearPartsControl/Domain/Entities`
- `src/WearPartsControl/Infrastructure`
- `src/WearPartsControl/ApplicationServices/PartServices`

建议后续新增：
- `src/WearPartsControl/ApplicationServices/PartServices/Services/*`
- `src/WearPartsControl/ApplicationServices/PartServices/Rules/*`
- `tests/WearPartsControl.Tests/PartServices/*`

---

## 5. 当前映射状态审计（2026-04-22）

基于当前项目 `WearPartsControl` 与源项目 `VulnerablePartsSys` 的代码核对，结论如下：**文档中描述的旧系统能力尚未被当前项目完全映射**。

### 5.1 已完成或基本完成

- 基础配置维护：已迁移到 `ClientAppInfoService` + `ClientAppInfoViewModel`。
- 易损件定义维护：已迁移到 `WearPartManagementService`，支持按资源号复制定义。
- 易损件更换主流程：已迁移到 `WearPartReplacementService`，已覆盖条码长度、条码复用、更换原因寿命规则、PLC 清零/写条码、更换记录落库。
- 历史更换记录查询/导出：已迁移到 `PartUpdateRecordViewModel` / `PartUpdateRecordUserControl`。
- 寿命监控与停机写点：已迁移到 `WearPartMonitorService` / `WearPartMonitoringHostedService`，并保留“每天一条”的超限记录限制。
- MHR 登录与缓存：已迁移到 `LoginService` + `MhrUserDirectoryCache`。
- 模切分条 by 工具校验：本轮已补充“工具编码必填 + 新条码必须包含工具编码”的更换校验，并用本地 SaveInfo 记录每个易损件最近选择的工具编码。

### 5.2 部分映射，仍有缺口

- 历史页分页：
  - 已有分页与导出。
  - 本轮补充了“每页条数可配置”。
- 迁移文档中的 `PLC 写入 + DB 写入` 一致性优化建议：当前仍主要依赖顺序执行，尚未引入补偿任务或 outbox。

### 5.3 尚未完整映射的旧系统能力

- `EquipentInVersion` 设备版本同步逻辑：当前项目中未发现等价实体与同步流程。
- `UserInfoByResourceId` 用户快照入库：当前登录缓存仍是本地 JSON 缓存，未落到数据库表。

### 5.4 结论

- 本文档对**旧系统业务范围**的梳理是完整且可用的。
- 但对**当前项目已迁移完成度**而言，不能视为“已全部映射完成”。
- 当前项目更接近“核心更换/监控/登录能力已迁移完成，且 `ToolChange` 主数据、模切分条换刀校验、涂布 A/B 面 + Spacer 校验闭环已补齐；用户快照入库与设备版本同步仍待继续补齐”的状态。

### 5.5 本轮新增映射结论

- `ToolChange`：已完成数据库实体、EF 映射、仓储、应用服务、主窗口入口和维护页面迁移，可执行增删改查。
- 模切分条换刀校验：已从“自由输入工具编码”升级为“正式主数据下拉选择 + 按易损件记忆最近选择”。
- 涂布工序：已完成 A/B 面选择、条码 `ABSite` 比对、`SpacerManagementService` 远程校验，以及校验失败后的停机点写入保护。
