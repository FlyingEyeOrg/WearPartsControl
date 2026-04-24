本项目的本地化生成器与使用说明

概述

本项目采用 JSON 作为翻译源（可表达对象、数组、嵌套等结构）。构建期间运行一个生成器，将 JSON 展平成 `.resx` 并生成强类型访问代码 `LocalizationCatalog.g.cs`，然后将这些文件作为编译产物与嵌入资源处理。生成产物只存在于 `obj` 目录，不应提交到版本控制。

位置

- 生成器工具：tools/LocalizationResourceGenerator
- 构建期 targets：build/Localization.targets
- JSON 源目录：src/WearPartsControl/Resources/Localization
- 生成输出：src/WearPartsControl/obj/<Configuration>/GeneratedLocalization/

如何工作（简要）

- MSBuild 在编译前会调用 `GenerateLocalizationArtifacts` 目标（由 `build/Localization.targets` 提供）。
- 目标以 JSON 源作为输入（Items），并以一个 `stamp` 文件作为输出标记，从而支持增量构建。
- 生成器会写出 `.resx` 文件到 `obj/.../GeneratedLocalization/Resources/`，并生成 `LocalizationCatalog.g.cs` 到 `obj/.../GeneratedLocalization/`。
- 构建目标会将生成的 `.resx` 作为 `EmbeddedResource`（通过 `Link` 指向 `Resources/` 路径）并把生成的 `.g.cs` 包含到编译中。

手动运行生成器（调试）

在项目根目录下运行：

```powershell
# 以 Debug 配置为例，手动生成到 obj/Debug 路径
dotnet run --project tools/LocalizationResourceGenerator/LocalizationResourceGenerator.csproj -- src/WearPartsControl/Resources/Localization src/WearPartsControl/obj/Debug/GeneratedLocalization/Resources src/WearPartsControl/obj/Debug/GeneratedLocalization/LocalizationCatalog.g.cs
```

增量构建注意事项

- 生成目标使用 `Inputs`（JSON 文件 + 生成器代码）和 `Outputs`（stamp 文件）判断是否需要重新执行。如果 JSON 没有变化，生成器不会再次运行。
- 如需强制重新生成，删除 `src/WearPartsControl/obj/<Configuration>/GeneratedLocalization/LocalizationResourceGenerator.stamp`，或直接删除整个 `GeneratedLocalization` 目录，然后重新构建。

提交策略

- 不要提交 `obj/*/GeneratedLocalization` 下生成的 `.resx` 或 `.g.cs` 文件。
- 仓库中保留 JSON 源文件（`src/WearPartsControl/Resources/Localization/*.json`）和生成器源码（`tools/LocalizationResourceGenerator`）。

故障排查

- 若构建时报 MissingManifestResourceException，请先确保生成器成功运行并生成了 `.resx` 到 obj 目录，再检查生成的 `EmbeddedResource` 是否被正确包含。
- 若生成器报编码错误，确认环境支持 UTF-8，或手动运行生成器查看详细错误。

维护建议

- 若需要进一步拆分或增强生成器逻辑（例如支持更多数据类型或生成不同格式），在 `tools/LocalizationResourceGenerator` 中扩展 `LocalizationArtifactGenerator` 的实现并补充对应单元测试。
- 避免在不同层级使用同名对象节，否则生成器可能产生命名冲突的 `Section` 类型。ViewModel 专用资源建议使用 `*Vm` 后缀，例如 `ViewModels.MainWindowVm`。
- PLC 相关用户可见异常、状态文案和日志模板现已分别放在 `PlcService.Errors`、`Services.PlcStartupConnection`、`Services.PlcConfigurationMonitor`、`Services.PlcPipeline` 节下；新增资源时避免再次创建同名节。
- `App.xaml.cs` 中的用户可见文本也应放入本地化资源，当前已使用 `FriendlyErrorTitle`、`UnexpectedError` 和 `App.LegacyImportCompletedTitle`。
- 语言首选项当前以 `user-config.json` 中的 `Language` 字段为主保存与恢复；启动时仍兼容读取旧的 `localization-options.json`，并会自动迁移后删除旧文件。
- `LocalizationOptionsSaveInfo` 现在仅保留为旧版 `localization-options.json` 的迁移模型，不再作为运行期语言设置的写回目标。
- 已显示界面如果绑定的是 ViewModel 缓存属性而不是 `loc:Loc`，切换语言后还需要监听 `LocalizationBindingSource.Refreshed` 并主动触发 `PropertyChanged`，否则 UI 会停留在旧语言。
- 运行期切换语言时，应在 UI 线程应用 `CurrentCulture` / `CurrentUICulture` 并触发 `LocalizationBindingSource.Refresh()`；不要依赖重建当前 Tab 的 `UserControl` 来“刷新语言”，否则容易带来页面整页重载与状态丢失。
- `ILocalizationService` 需要维护稳定的服务级 `CurrentCulture`，不能把调用线程的 `CultureInfo.CurrentUICulture` 直接当作服务状态；否则后台线程或跨线程回调里即使用户语言已切到英文，也可能重新取到中文文案。
- `LocalizedText.Get/Format(...)` 也必须读取同一套服务级文化状态，不能再单独依赖当前线程 `CurrentUICulture`；否则 ViewModel、异常消息和 XAML `loc:Loc` 会出现切换后语言来源分叉。
- 本地化相关单元测试不要再写死中文或英文文案；优先使用 `LocalizedText.Get/Format(...)` 对齐当前资源，或显式包裹 `TestCultureScope` 指定文化，否则测试结果会受执行环境的 UI 语言影响而出现假失败。
- 主窗口首屏如果依赖持久化语言决定默认 Tab、标题或缓存文案，应在 `Show()` 前完成首屏初始化，避免先以默认语言渲染再切换造成闪烁。
- `MainWindowViewModel.RefreshLocalizedShellState(...)` 这类壳层缓存文本刷新入口，在生成标题、品牌名、Tab 文案前必须先确认 `ILocalizationService.CurrentCulture` 与 `user-config.json` 中的 `Language` 一致；否则即使启动链路已初始化，壳层仍可能沿用旧文化生成首屏文本。
- 多语言表单行不要再使用固定 `80` / `160` 标签宽度；优先使用 `Grid.IsSharedSizeScope="True"` + `SharedSizeGroup` + `TextBlock.TextWrapping="Wrap"` 的布局，让标签列在同一分组内对齐且可换行。
- 多语言导航和操作区不要再依赖 `TextTrimming=CharacterEllipsis` 或固定按钮宽度来“挤下”英文文案；优先增加容器宽度、允许页签标题换行，并把操作按钮改为 `MinWidth`。

如需我把这段内容合并回根 README.md，我可以替你把这部分追加进去并提交。