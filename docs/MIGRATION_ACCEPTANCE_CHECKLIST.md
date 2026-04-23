# 迁移验收清单

## 本次回归结论

- 回归时间：2026-04-23
- 构建结果：通过
- 全量测试结果：155 通过，0 失败，0 跳过
- 本轮提交目标：
  - 设备基础信息页支持测试 PLC 连接
  - 设备基础信息页支持开启/关闭易损件监控
  - 旧项目配置导入能力继续保留并可与本轮功能协同工作

## 旧项目核心业务迁移验收

- 客户端基础信息维护：通过
- 登录认证：通过
  - 默认刷卡登录
  - 导入旧配置后支持工号登录模式迁移
- 易损件定义管理：通过
- 易损件更换主流程：通过
- 换刀类型主数据维护：通过
- 寿命监控与超限记录：通过
- 旧库易损件定义导入：通过
- 旧项目配置导入转换：通过

## 配置迁移验收

- `db/Data.db -> ClientAppConfigurationEntity`：通过
- `Json/AppSetting.json -> app-settings.json`：通过
- `Json/AppConfig.json -> app-settings.json / user-config.json / com-notification.json / spacer-validation.json`：通过
- `Json/MHRInfos.json -> mhrinfo.json`：通过
- `Json/MHR.json -> mhrinfo.json` 回退导入：通过
- `Json/MySql.json`：确认不再需要，不迁移
- `ToolChangeSaveInfo.json`：确认已由新主数据与选择缓存替代，不迁移

## 设备基础信息页验收

- 可保存基地、工厂、区域、工序、设备编号、资源号、PLC 信息：通过
- “导入旧项目配置”按钮可用：通过
- “测试 PLC 连接”按钮在 PLC 未连接时可用：通过
- 测试 PLC 连接成功后会同步更新 PLC 连接状态：通过
- “开启易损件监控 / 关闭易损件监控”按钮可用：通过
- 关闭后后台监控周期任务会跳过执行：通过
- 重新开启后会立即手动执行一次监控：通过

## 回归测试清单

- `dotnet build src/WearPartsControl/WearPartsControl.csproj`
- `dotnet test tests/WearPartsControl.Tests/WearPartsControl.Tests.csproj`

## 现场验收建议

- 使用真实 PLC 参数在设备基础信息页点击“测试 PLC 连接”，确认现场网络和协议配置正确
- 切换“开启/关闭易损件监控”，观察日志中是否出现“跳过后台易损件监控：当前已关闭后台监控。”
- 使用旧版 `VulnerablePartsSys\db\Data.db` 再做一次配置导入，确认资源号、登录模式和 MHR 配置落地正确
- 对至少一条实际易损件执行预览和更换，确认 PLC 连接、扫码、更换记录和监控链路闭环正常