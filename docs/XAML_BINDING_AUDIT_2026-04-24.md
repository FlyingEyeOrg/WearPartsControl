# XAML Binding Audit 2026-04-24

## Scope

- Repository: `WearPartsControl`
- Scope: `src/WearPartsControl/**/*.xaml`
- Focus: explicit `Mode=TwoWay` and implicit default two-way bindings on editable targets such as `TextBox.Text`, editable `ComboBox.Text`, `SelectedItem`, `SelectedValue`, and `CheckBox.IsChecked`

## Summary

| Category | Count | Notes |
| --- | ---: | --- |
| Confirmed safe | 46 | Input bindings backed by writable source properties |
| Recommend explicit OneWay | 8 | Display-oriented bindings backed by computed or privately set properties |
| Manual review | 2 | Writable properties in more complex navigation or paging flows |
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

- [src/WearPartsControl/Views/MainWindow.xaml](../src/WearPartsControl/Views/MainWindow.xaml#L69) `SelectedContent -> SelectedContent`
  - Writable source property, but it participates in custom tab content navigation and is better reviewed together with `UserTabControl` semantics.
- [src/WearPartsControl/UserControls/PartUpdateRecordUserControl.xaml](../src/WearPartsControl/UserControls/PartUpdateRecordUserControl.xaml#L166) `Text -> RequestedPageNumber`
  - Writable source property, but invalid user input and paging validation behavior should be checked end-to-end.

## Notes

- The regression fixed on 2026-04-24 came from a read-only source property being bound to a target property that WPF treated as a write-back candidate during XAML loading.
- No additional binding matching that failure mode was found in this audit.
- A dedicated WPF regression test now covers `LoginWindow` loading: [tests/WearPartsControl.Tests/LoginWindowTests.cs](../tests/WearPartsControl.Tests/LoginWindowTests.cs)