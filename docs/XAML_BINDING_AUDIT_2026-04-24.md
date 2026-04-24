# XAML Binding Audit 2026-04-24

## Scope

- Repository: `WearPartsControl`
- Scope: `src/WearPartsControl/**/*.xaml`
- Focus: explicit `Mode=TwoWay` and implicit default two-way bindings on editable targets such as `TextBox.Text`, editable `ComboBox.Text`, `SelectedItem`, `SelectedValue`, and `CheckBox.IsChecked`

## Summary

| Category | Count | Notes |
| --- | ---: | --- |
| Confirmed safe | 48 | Input bindings backed by writable source properties or intentionally one-way display flows |
| Recommend explicit OneWay | 0 | All identified display-only candidates were tightened |
| Manual review | 0 | The two former review items were resolved |
| High risk | 0 | No additional read-only source bound to a potentially two-way target was found |

## Applied Fixes

The following display bindings were updated to explicit `Mode=OneWay` to remove ambiguous default binding semantics and reduce the chance of repeating the `LoginWindow` crash pattern:

- [src/WearPartsControl/Views/MainWindow.xaml](../src/WearPartsControl/Views/MainWindow.xaml#L85) `LoadingText`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L227) `StatusMessage`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L269) `WearPartMonitoringButtonText`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L288) `WearPartMonitoringButtonText`
- [src/WearPartsControl/UserControls/PartManagementUserControl.xaml](../src/WearPartsControl/UserControls/PartManagementUserControl.xaml#L42) `StatusMessage`
- [src/WearPartsControl/UserControls/ReplacePartUserControl.xaml](../src/WearPartsControl/UserControls/ReplacePartUserControl.xaml#L43) `StatusMessage`
- [src/WearPartsControl/UserControls/ToolChangeManagementUserControl.xaml](../src/WearPartsControl/UserControls/ToolChangeManagementUserControl.xaml#L41) `StatusMessage`
- [src/WearPartsControl/UserControls/PartUpdateRecordUserControl.xaml](../src/WearPartsControl/UserControls/PartUpdateRecordUserControl.xaml#L41) `StatusMessage`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L27) `StatusMessage`
- [src/WearPartsControl/Views/MainWindow.xaml](../src/WearPartsControl/Views/MainWindow.xaml#L69) `SelectedContent`

The following input binding was made explicit and paired with stricter command validation:

- [src/WearPartsControl/UserControls/PartUpdateRecordUserControl.xaml](../src/WearPartsControl/UserControls/PartUpdateRecordUserControl.xaml#L166) `RequestedPageNumber` changed to explicit `Mode=TwoWay`
- [src/WearPartsControl/ViewModels/PartUpdateRecordViewModel.cs](../src/WearPartsControl/ViewModels/PartUpdateRecordViewModel.cs) `GoToPageCommand` now requires a positive integer page number

## Confirmed Safe

These bindings target editable controls and resolve to source properties with writable setters.

### ClientAppInfo

- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L43) `SelectedValue -> SiteCode`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L57) `SelectedItem -> ProcedureCode`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L72) `SelectedItem -> PlcProtocolType`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L86) `Text -> ShutdownPointAddress`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L103) `SelectedItem -> FactoryCode`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L116) `Text -> ResourceNumber`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L130) `Text -> PlcIpAddress`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L145) `Text -> SiemensRack`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L160) `Text -> SiemensSlot`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L176) `SelectedItem -> AreaCode`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L189) `Text -> EquipmentCode`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L203) `Text -> PlcPort`
- [src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml](../src/WearPartsControl/UserControls/ClientAppInfoUserControl.xaml#L220) `IsChecked -> IsStringReverse`

### PartInfo

- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L58) `Text -> PartName`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L70) `Text -> CurrentValueAddress`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L82) `Text -> WarningValueAddress`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L94) `Text -> ShutdownValueAddress`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L107) `Text -> LifetimeType`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L121) `IsChecked -> IsShutdown`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L132) `Text -> PlcZeroClearAddress`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L149) `Text -> CurrentValueDataType`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L163) `Text -> WarningValueDataType`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L177) `Text -> ShutdownValueDataType`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L191) `Text -> InputMode`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L204) `Text -> CodeMinLength`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L216) `Text -> CodeMaxLength`
- [src/WearPartsControl/UserControls/PartInfoUserControl.xaml](../src/WearPartsControl/UserControls/PartInfoUserControl.xaml#L228) `Text -> BarcodeWriteAddress`

### UserConfig

- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L37) `SelectedValue -> SelectedLanguage`
- `LanguageOptions` 刷新时使用整体替换集合而不是对现有集合执行 `Clear/Add`，避免保存并切换语言后 HandyControl `ComboBox` 出现选项显示被清空的问题。
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L53) `Text -> MeResponsibleWorkId`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L66) `Text -> MeResponsibleName`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L79) `Text -> PrdResponsibleWorkId`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L92) `Text -> PrdResponsibleName`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L105) `Text -> ReplacementOperatorName`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L118) `IsChecked -> ComNotificationEnabled`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L208) `Text -> ComAccessToken`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L221) `Text -> ComSecret`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L240) `IsChecked -> SpacerValidationEnabled`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L252) `Text -> SpacerValidationUrl`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L265) `Text -> SpacerValidationTimeoutMilliseconds`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L278) `IsChecked -> SpacerValidationIgnoreServerCertificateErrors`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L290) `Text -> SpacerValidationCodeSeparator`
- [src/WearPartsControl/UserControls/UserConfigUserControl.xaml](../src/WearPartsControl/UserControls/UserConfigUserControl.xaml#L303) `Text -> SpacerValidationExpectedSegmentCount`

### ReplacePart

- [src/WearPartsControl/UserControls/ReplacePartUserControl.xaml](../src/WearPartsControl/UserControls/ReplacePartUserControl.xaml#L260) `Text -> NewBarcode`
- [src/WearPartsControl/UserControls/ReplacePartUserControl.xaml](../src/WearPartsControl/UserControls/ReplacePartUserControl.xaml#L274) `Text -> ReplacementMessage`

### ToolChangeManagement

- [src/WearPartsControl/UserControls/ToolChangeManagementUserControl.xaml](../src/WearPartsControl/UserControls/ToolChangeManagementUserControl.xaml#L53) `Text -> ToolName`
- [src/WearPartsControl/UserControls/ToolChangeManagementUserControl.xaml](../src/WearPartsControl/UserControls/ToolChangeManagementUserControl.xaml#L66) `Text -> ToolCode`
- [src/WearPartsControl/UserControls/ToolChangeManagementUserControl.xaml](../src/WearPartsControl/UserControls/ToolChangeManagementUserControl.xaml#L79) `Text -> Keyword`

## Manual Review

No remaining manual-review items are open after the follow-up tightening pass.

## Notes

- The regression fixed on 2026-04-24 came from a read-only source property being bound to a target property that WPF treated as a write-back candidate during XAML loading.
- No additional binding matching that failure mode was found in this audit.
- A dedicated WPF regression test now covers `LoginWindow` loading: [tests/WearPartsControl.Tests/LoginWindowTests.cs](../tests/WearPartsControl.Tests/LoginWindowTests.cs)