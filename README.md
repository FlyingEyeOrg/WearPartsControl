# WearPartsControl

## 架构分层

### Domain

- 位置：`src/WearPartsControl/Domain`
- 职责：表达业务概念、规则和契约，不依赖数据库、网络、文件或 EF Core。
- 关键内容：
	- 实体：`ClientAppConfigurationEntity`、`WearPartDefinitionEntity`
	- 值对象：`ResourceNumber`、`PlcEndpoint`
	- 领域服务：`WearPartDefinitionDomainService`
	- 领域事件契约：`IDomainEvent`、`IDomainEventHandler<T>`
	- 仓储接口：`IRepository<TEntity, TId>`、`IClientAppConfigurationRepository`、`IWearPartRepository`
		- `IRepository<TEntity, TId>` 仅保留仓储职责（包含 `SoftDeleteAsync`）。
		- 事务由独立的 `IUnitOfWork<TDbContext>` 负责，仓储内部持有对应 `UnitOfWork`，符合单一职责。
		- `DbContextBase` 不直接实现 `IUnitOfWork`，避免上下文职责膨胀。
	- 领域异常与验证：`DomainBusinessException`、`DomainValidationException`、`DomainValidationRules`

### Infrastructure（EF Core）

- 位置：`src/WearPartsControl/Infrastructure`
- 职责：实现所有 I/O 与持久化细节，依赖 Domain 契约并提供具体实现。
- 当前桌面端 DI 策略中，数据库相关服务按独立实例解析，仓储内部 `UnitOfWork` 始终绑定当前仓储自己的 `DbContext`，避免根容器长期持有同一 `DbContext` 引发并发访问异常。
- 关键内容：
	- `WearPartsControlDbContext`
	- EF Core 映射配置：
		- `EntityFrameworkCore/Configurations/ClientAppConfigurationEntityConfiguration`
		- `EntityFrameworkCore/Configurations/WearPartDefinitionEntityConfiguration`
		- `EntityFrameworkCore/Configurations/ToolChangeEntityConfiguration`
	- 仓储实现：
		- `EntityFrameworkCore/Repositories/ClientAppConfigurationRepository`
		- `EntityFrameworkCore/Repositories/WearPartRepository`
		- `EntityFrameworkCore/Repositories/ToolChangeRepository`
	- 初始化与脚本：
		- `EntityFrameworkCore/SqliteDatabaseInitializer`
		- `EntityFrameworkCore/Migrations/001_initial_wearparts_schema.sql`

## 依赖方向

- `Domain` 不依赖 `Infrastructure`
- `Infrastructure` 可以依赖 `Domain`
- `ApplicationServices` 通过接口（仓储契约）访问领域数据

## ApplicationServices

- `ApplicationServices/ApplicationService`：应用服务基类，统一提供当前登录用户访问能力。
- `ApplicationServices/ApplicationService`：应用服务基类，统一提供当前登录用户访问能力和基于 `access_level` 的权限校验。
- `ApplicationServices/CurrentUserAccessor`：桌面应用登录态上下文，同时实现 `ICurrentUserAccessor` 与 `ICurrentUser`，供应用服务和 EF 审计共用。
- `ApplicationServices/AppSettings/AppSettingsService`：独立的客户端应用设置服务，负责资源号、登录模式与登录输入阈值配置，不再混入 `PartServices`。
- `ApplicationServices/LoginService/LoginService`：支持登录、读取当前用户、注销，并将登录态同步给应用服务与 EF 审计。
- `ApplicationServices/LoginService/MhrUserDirectoryCache`：缓存 MHR 用户目录，优先复用最近一次拉取结果，减少重复访问远程接口。
- `ApplicationServices/PartServices/WearPartManagementService`：易损件定义管理服务，负责定义查询、创建、更新、删除和跨资源号复制。
- `ApplicationServices/PartServices/WearPartReplacementService`：扫码更换应用服务，负责读取 PLC 当前状态、校验条码、执行清零/写码，并写入更换记录。
- `ApplicationServices/PartServices/ToolChangeManagementService`：换刀类型主数据服务，负责换刀类型的查询、创建、更新和删除。
- `ApplicationServices/PartServices/WearPartMonitorService`：寿命监控应用服务，负责读取 PLC 阈值、生成超限记录、发送通知，并执行停机点写入逻辑。
- `ApplicationServices/PartServices/WearPartMonitoringHostedService`：后台监控调度服务，启动后按当前资源号每 5 分钟执行一次寿命监控。
- `ApplicationServices/LegacyImport/LegacyDatabaseImportService`：旧版 SQLite 数据导入服务，负责把旧库配置、易损件、超限记录和更换记录映射到当前库。
- `ApplicationServices/HttpService/HttpJsonService`：统一 HTTP JSON 访问入口，默认使用长生命周期 `HttpClient` 与连接池复用；仅在确有需要忽略证书校验时创建临时处理器，避免为普通超时控制反复创建 `HttpClient`。

## 当前业务迁移进度

- 已完成：易损件定义管理、扫码更换主流程、寿命监控与超限记录主流程。
- 已完成：登录用户上下文与 `access_level` 权限校验。
- 已补齐持久化表：`wear_part_replacement_records`、`exceed_limit_records`。
- 已补齐：后台寿命监控调度、MHR 用户列表缓存、旧版 SQLite 数据导入。
- 已补齐：`ToolChange` 主数据实体、仓储、应用服务和“换刀类型管理”页面，模切分条工序已从自由输入工具编码升级为正式主数据选择。
- 已补齐：涂布工序 A/B 面选择、垫片条码解析与远程校验，并在校验失败时执行停机点写入保护。
- 已补齐：易损件管理页支持直接选择旧版 SQLite 数据库并导入易损件定义，适合生产环境一次性迁移旧系统数据。
- 已补齐：客户端信息页支持直接从旧版 `VulnerablePartsSys\db\Data.db` 导入基础配置、登录模式、MHR、COM 通知和垫片校验配置；旧版 `spacer-validation.json` 现会一并迁移进新的 `user-config.json`。
- 已补齐：客户端信息页支持测试 PLC 连接、彩色展示易损件监控状态，并支持手动开启或关闭易损件后台监控。
- 易损件新增/编辑窗口已按旧版录入习惯调整：输入方式默认 `Manual`，三组 PLC 数据类型默认 `FLOAT`，寿命类型固定为 `记米 / 计次 / 计时`，默认选中 `计次`，条码长度默认 `0-0`，附加清零地址改为可选。

## 数据与配置目录

- 为便于整包拷贝迁移，程序运行时依赖的数据统一放在 `PrivateData` 目录。
- 默认位置：`{应用程序目录}/PrivateData`
- 子目录约定：
	- `Settings`：所有设置与配置 JSON（SaveInfo、登录配置等）
	- `LocalDB`：SQLite 数据库文件（`wear-parts-control.db`）
- 日志为临时运行文件，保留在应用根目录：`{应用程序目录}/logs/app-*.log`
- 普通应用服务统一通过依赖注入获取 `ILogger<T>` 记录日志；静态 `Serilog.Log` 仅保留在应用启动和全局异常等进程级入口。
- 启动性能日志会以 `启动阶段:` 前缀写入同一日志文件，记录每个关键阶段的增量耗时与累计耗时，便于拆分首屏前后性能瓶颈。
- 关停性能日志会以 `关停阶段:` 前缀写入同一日志文件，记录从收到退出请求到 `Exit` 完成的各阶段耗时，便于定位关闭卡顿或资源释放过慢的问题。

## 登录与配置

- 客户端基础信息未配置完成前，主窗口仅保留基础信息页，右上角 `LoginBox` 禁用；保存成功后才会开放其余 tabs 与登录入口。
- 客户端基础信息页之外的 tabs 在未登录时会统一显示登录提示页；提示标题、说明和操作引导都已接入多语言资源，登录成功后会自动恢复到当前 tab 的真实内容。
- 应用启动时会优先完成 Host 构建和本地化初始化；主窗口会先显示，再继续执行视图模型初始化与后台启动流程，这样启动 loading 可见且首屏更早反馈。数据库初始化仍通过 `AppStartupCoordinator` 串行完成，主窗口在真正访问数据前等待该初始化任务结束。
- 启动阶段当前会额外记录这些关键节点：Host 构建、本地化初始化、主窗口解析、主窗口显示、主窗口视图模型初始化、应用设置加载、默认内容装载、数据库初始化、PLC 启动连接完成，可直接通过日志量化首屏前后的耗时拆分。
- 如果已经配置 `ClientApp` 且上次关闭软件时仍开启了易损件监控，应用启动时会按当前资源号自动建立 PLC 连接；如果监控原本处于关闭状态，则启动和后续切换到设备基础信息页都不会隐式触发 PLC 自动连接。若启动时 PLC 临时连接失败，系统当前只保留“断连”运行态，不会擅自把用户的监控开关永久改写为关闭。
- 易损件更换页在更换成功后会立即把新记录刷新到当前页面的数据表；易损件更换历史页在重新进入页面时会主动刷新，确保能看到最新更换记录。
- 更换寿命校验当前按原因区分：
- `寿命到期，正常更换`：必须达到预警寿命。
- `过程损坏`、`切拉换型`：不校验寿命。
- `寿命到期，更换位置`：必须达到预警寿命，且还不能达到停机寿命。
- `寿命到期维保`：必须达到预警寿命，且还不能达到停机寿命。
- 更换易损件时，程序当前只向 PLC 写入“当前寿命”；预警寿命和停机寿命改由设备侧自行维护，不再由客户端回写。执行更换前，系统会先校验当前寿命、预警寿命、停机寿命三项读取结果；任一读取失败，或预警寿命未小于停机寿命时，都会直接禁止更换。
- 旧件重新装回当前设备时，会沿用旧件记录中的当前寿命回写到 PLC；若旧件寿命已达到停机值，则不允许重新更换到当前设备。若某条码曾因“寿命到期，正常更换”被换下，则该旧件会被永久禁止重新更换到当前设备。
- 更换记录中的“当前编码 / 当前寿命 / 预警寿命 / 停机寿命”始终表示本次被换下的当前装机件快照；`DataValue` 仅记录实际回写到 PLC 的新件当前寿命，便于区分拆下件与装回件的数据语义。
- 易损件更换现在会先弹出确认框；如果检测到是当前设备曾使用过的旧件回装，则会先给出旧件回装确认；当该旧件寿命已达到预警值但未到停机值时，还会追加一次风险确认，避免敏感误操作。
- 更换原因在业务层和数据库中统一保存为稳定代码；界面下拉、历史记录和导出时再按当前语言环境转换为本地化文案，同时兼容旧版本已落库的中文原因值。
- 更换记录应用模型当前同时保留 `ReasonCode` 和 `ReasonDisplayName`：前者用于业务判断、筛选和后续统计扩展，后者专供历史页表格、导出和其他显示场景使用，避免显示层继续混用业务代码值。
- 模切分条工序当前通过 `ToolChange` 主数据下拉选择换刀类型；易损件新增/编辑窗口不再绑定换刀类型，更换页按当前工序优先回退到定义关联或最近一次选择的换刀类型；如果都不存在但已有工具编码候选，则默认选中第一项。更换条码仍必须包含所选工具编码。
- 主窗口中的“换刀类型管理” tab 只会在当前客户端工序为 `模切分条` 时显示，其他工序不会展示该页签。
- 涂布工序当前要求在更换页先选择 A/B 面；系统会解析垫片条码中的 `ABSite` 与所选值比对，并调用 `SpacerManagementService` 做远程校验。远程校验失败时会尝试写入客户端配置中的停机点位，形成与旧系统一致的保护闭环。
- 客户端基础信息中的 `区域`、`工序` 由 `PrivateData/Settings/client-app-info.<culture>.json` 提供；会按当前语言环境优先加载对应文件，例如 `client-app-info.zh-CN.json`。
- PLC 相关配置遵循旧系统规则：西门子 PLC 显示并保存机架号与插槽号，两者默认值均为 `0`；`ModbusTcp` 与汇川 PLC 显示字符串反转开关。
- 登录窗口支持两种模式：默认刷卡登录；可在用户配置页的“用户环境配置”中勾选工号认证，导入旧配置后如果 `UseUserNumber=true` 也会切换为工号登录。
- 登录窗口通过刷卡器模拟键盘输入完成登录，窗口打开后会自动聚焦到密码输入框。
- 登录窗口支持回车提交；仅在刷卡模式下，当相邻输入间隔超过 `LoginInputMaxIntervalMilliseconds` 时，才会判定为手工输入并拒绝登录。Debug 构建固定使用 2000 毫秒，Release 构建使用配置值。
- 自动注销相关的 UI 交互统一收敛到一个交互保活工具：登录窗口、添加/编辑易损件弹窗、导出文件选择框等模态交互期间会暂停倒计时；弹窗关闭后会从完整倒计时重新开始。主窗口中的键盘输入、鼠标点击和焦点进入也会重置倒计时，避免正常操作过程中被自动登出并把当前 tab 切回登录提示页。
- 主窗口托盘当前采用显式进入策略：点击最小化时不会自动切换到托盘；只有点击主窗口关闭按钮后选择“最小化到托盘”时，主窗口才会从任务栏隐藏并仅保留托盘入口。首次通过关闭按钮进入托盘时会显示一次气泡提示；双击托盘图标或点击托盘面板里的“恢复窗口”都会强制恢复主窗口并隐藏托盘图标。
- 主窗口切换到托盘前会记录当前窗口布局：普通窗口时保存当前位置和尺寸，最大化窗口时保存最大化状态及其还原边界；从托盘恢复显示时会优先还原原先的坐标尺寸和最大化状态，避免恢复后窗口位置或状态丢失。
- 所有用户主动退出主程序的入口当前统一要求“已登录”后才允许继续；未登录时会阻止退出并给出提示。托盘面板中的“退出程序”会先弹出二次确认，再触发应用关停。
- 应用配置统一使用 `src/WearPartsControl/PrivateData/Settings/app-settings.json`。
- PLC 管线慢调用阈值也放在该文件中，保存应用设置后会刷新到运行中的 PLC 管线，无需重启。
- 当前默认配置示例：`{"ResourceNumber":"","LoginInputMaxIntervalMilliseconds":80,"UseWorkNumberLogin":false,"IsWearPartMonitoringEnabled":true,"PlcPipeline":{"SlowQueueWaitThresholdMilliseconds":100,"SlowExecutionThresholdMilliseconds":500}}`
- PLC 管线操作名常量已按业务域拆分到 `ApplicationServices/PlcService` 目录下的多个常量类，避免单文件持续膨胀。
- `ResourceNumber` 用于在客户端配置中查找资源对应的 `SiteCode`，随后由登录服务完成用户认证。
- 登录成功后，用户信息会同步到 `ICurrentUserAccessor`，主窗口右上角 `LoginBox` 会自动刷新工号、权限与登录按钮状态。
- 登录服务会将 MHR 返回的用户目录缓存到 `PrivateData/Settings/mhr-user-cache.json`，默认缓存 1 天，可通过 `mhrinfo.json` 的 `CacheDays` 调整。
- `user-config.json` 当前统一承载负责人、COM 通知基础参数、COM 凭据以及垫片校验配置；升级后如果目录里仍有旧的 `com-notification.json` 或 `spacer-validation.json`，系统会在首次读取用户配置时自动迁移并清理旧文件。
- 用户配置页中的“测试 COM 通知”现在会先按当前表单保存配置，再使用统一的 Markdown 模板同时发送群组通知和 COM 用户工作通知，正文会带出基地、工厂、区域、工序、设备编号、资源号以及 ME/PRD 负责人。
- 应用图标当前统一使用 `Assets/app.ico`：项目文件中的 `ApplicationIcon` 用于生成 exe 图标，WPF 全局 `Window.Icon` 样式负责主窗口及其任务栏图标显示，避免只改可执行文件图标而窗口仍显示默认图标。
- 客户端信息页首次完成基础信息配置前，不要求用户认证；配置完成后，保存信息、导入旧项目配置、测试 PLC 连接和开启/关闭易损件监控都需要先登录认证。
- 客户端信息页中的“测试 PLC 连接”按钮会按当前表单值直接发起连接测试；仅在未开启易损件监控时允许点击，即使当前已经连通也允许重复测试，连接成功后会同步刷新全局 PLC 连接状态。
- 客户端信息页会以彩色状态框显式展示“易损件监控状态”；监控开关保存设置时不会再重建设备基础信息 tab。
- 客户端信息页在易损件监控开启期间会锁定 PLC 类型、IP、端口、停机地址以及相关西门子/字符串反转参数，避免监控运行中误改 PLC 配置。
- 易损件更换页顶部原“PLC连接状态”已调整为“易损件监控状态”，并随 `IsWearPartMonitoringEnabled` 配置实时刷新。
- “开启/关闭易损件监控”按钮会写入 `IsWearPartMonitoringEnabled`；关闭后后台监控周期任务会跳过执行，重新开启后会立即触发一次监控。
- 易损件后台监控发送的 COM 通知已统一改为 Markdown 模板；预警、停机和测试通知的正文结构保持一致，并额外包含用户配置中的 ME/PRD 负责人，便于直接在 COM 消息里定位责任人。
- 与监控开关相关的设置保存事件、PLC 状态变更事件在 ViewModel 中统一经 `IUiDispatcher` 回到 UI 线程后再更新绑定属性，避免点击开启监控时出现 WPF 跨线程访问异常。
- 换刀类型管理页已回到常规 CRUD 交互：填表后直接点“新增”，选中记录修改后点“编辑”，删除按选中记录执行；同一页面内连续新增后再编辑同一条记录时，也不会再触发 EF Core 的重复跟踪异常。

## 旧库导入

- 生产环境推荐在“易损件管理”页面直接点击“导入旧库易损件”，选择旧版 SQLite 数据库文件后执行导入。
- 生产环境推荐在“客户端信息”页面直接点击“导入旧项目配置”，选择旧版 SQLite 数据库文件后执行导入；程序会自动回溯同级旧目录下的 `Json` 配置文件并做一次性转换导入。
- 旧系统 `VulnerablePartsSys` 的本地 SQLite 默认路径为 `VulnerablePartsSys\db\Data.db`。
- 页面导入范围仅包含旧库中的易损件定义，并写入当前客户端对应资源号下，不导入历史更换记录、超限记录，也不做旧库结构兼容处理。
- 配置导入范围包含：`v_Basic`、`AppSetting.json`、`AppConfig.json`、`MHRInfos.json`，若缺少 `MHRInfos.json` 则回退读取 `MHR.json`。
- 旧系统 `MySql.json` 不再导入。新版本统一使用本地 SQLite 和拆分后的 `PrivateData/Settings/*.json`，不再保留旧版本地/远程双写模式。
- 旧系统 `ToolChangeSaveInfo.json` 也不再导入。新版本已用正式 `ToolChange` 主数据和 `tool-change-selection.json` 替代该运行时缓存文件。
- 启动参数方式 `WearPartsControl.exe --import-legacy-db ...` 仍保留给整库导入场景；如果只是生产环境迁移易损件定义，优先使用页面导入入口。

## 测试

- `tests/WearPartsControl.Tests/DomainValueObjectsTests.cs`
- `tests/WearPartsControl.Tests/WearPartDefinitionDomainServiceTests.cs`
- `tests/WearPartsControl.Tests/WearPartRepositoryTests.cs`