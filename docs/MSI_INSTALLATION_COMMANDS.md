# MSI 安装包控制台使用说明

本文档说明如何通过控制台命令安装、更新、修复和卸载以下安装包，并说明 zip 包首次启动语言规则：

- `E:\Projects\DotNet\WearPartsControl\artifacts\installer\zh-CN\WearPartsControl-1.0.0-x64-zh-CN.msi`
- `E:\Projects\DotNet\WearPartsControl\artifacts\installer\en-US\WearPartsControl-1.0.0-x64-en-US.msi`
- `E:\Projects\DotNet\WearPartsControl\artifacts\installer\zh-CN\WearPartsControl-1.0.0-x86-zh-CN.msi`
- `E:\Projects\DotNet\WearPartsControl\artifacts\installer\en-US\WearPartsControl-1.0.0-x86-en-US.msi`

## 1. 安装包选择

| 系统/需求 | 推荐安装包 |
| --- | --- |
| 64 位 Windows，中文安装界面 | `zh-CN\WearPartsControl-1.0.0-x64-zh-CN.msi` |
| 64 位 Windows，英文安装界面 | `en-US\WearPartsControl-1.0.0-x64-en-US.msi` |
| 32 位 Windows，中文安装界面 | `zh-CN\WearPartsControl-1.0.0-x86-zh-CN.msi` |
| 32 位 Windows，英文安装界面 | `en-US\WearPartsControl-1.0.0-x86-en-US.msi` |

> 建议在目标系统架构与安装包架构一致时安装。64 位系统优先使用 x64 安装包。

## 2. 首次运行语言规则

- MSI 安装模式：软件首次运行时优先使用安装包语言。`zh-CN.msi` 首次启动为中文，`en-US.msi` 首次启动为英文。
- zip 解压模式：软件首次运行时根据当前 Windows 系统语言文化选择语言；如果系统语言不是软件支持的语言，则默认使用英文。
- 用户在“用户环境配置”页面手动修改语言后，后续启动以用户配置为准，不再重新按安装包或系统语言切换。

## 3. 图形界面安装

在 PowerShell 或 CMD 中执行：

```powershell
msiexec /i "E:\Projects\DotNet\WearPartsControl\artifacts\installer\zh-CN\WearPartsControl-1.0.0-x64-zh-CN.msi"
```

安装向导中可以选择：

- 安装目录
- 是否创建桌面快捷方式
- 是否开机自启动

## 4. 静默安装

### 4.1 默认静默安装

```powershell
msiexec /i "E:\Projects\DotNet\WearPartsControl\artifacts\installer\zh-CN\WearPartsControl-1.0.0-x64-zh-CN.msi" /qn /norestart /L*v "%TEMP%\WearPartsControl-install.log"
```

如果安装命令由系统账号、远程部署工具或受限服务执行，`%TEMP%` 可能指向 `C:\Windows\Temp` 等目录。为便于排查，建议提前创建日志目录并使用绝对路径，例如 `C:\Temp\WearPartsControl-install.log`。

默认行为：

- 安装到默认 Program Files 目录
- 创建桌面快捷方式
- 不开启开机自启动

### 4.2 指定安装目录

```powershell
msiexec /i "E:\Projects\DotNet\WearPartsControl\artifacts\installer\zh-CN\WearPartsControl-1.0.0-x64-zh-CN.msi" INSTALLFOLDER="D:\Apps\Wear Parts Control\" /qn /norestart /L*v "%TEMP%\WearPartsControl-install.log"
```

注意：安装目录建议以反斜杠结尾。

### 4.3 不创建桌面快捷方式

```powershell
msiexec /i "E:\Projects\DotNet\WearPartsControl\artifacts\installer\zh-CN\WearPartsControl-1.0.0-x64-zh-CN.msi" CREATE_DESKTOP_SHORTCUT="0" /qn /norestart /L*v "%TEMP%\WearPartsControl-install.log"
```

### 4.4 开启开机自启动

```powershell
msiexec /i "E:\Projects\DotNet\WearPartsControl\artifacts\installer\zh-CN\WearPartsControl-1.0.0-x64-zh-CN.msi" START_ON_LOGIN="1" /qn /norestart /L*v "C:\Temp\WearPartsControl-install.log"
```

安装器会写入当前执行安装命令用户的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 注册表项。该配置只影响当前用户，多用户设备需要分别在对应用户上下文中配置。软件首次启动后，会读取该状态并同步到“用户环境配置”页面中的“开启自启动”选项。

### 4.5 组合静默安装示例

```powershell
msiexec /i "E:\Projects\DotNet\WearPartsControl\artifacts\installer\zh-CN\WearPartsControl-1.0.0-x64-zh-CN.msi" INSTALLFOLDER="D:\Apps\Wear Parts Control\" CREATE_DESKTOP_SHORTCUT="0" START_ON_LOGIN="1" /qn /norestart /L*v "%TEMP%\WearPartsControl-install.log"
```

## 5. 更新安装

当后续生成更高版本 MSI 时，直接使用新版本安装包执行安装命令即可触发升级：

```powershell
msiexec /i "E:\Projects\DotNet\WearPartsControl\artifacts\installer\zh-CN\WearPartsControl-1.0.0-x64-zh-CN.msi" /qn /norestart /L*v "%TEMP%\WearPartsControl-upgrade.log"
```

说明：

- 安装器配置了 Major Upgrade，会阻止安装低版本覆盖高版本。
- `PrivateData` 下的用户配置、客户端信息和本地数据库目录会保留。
- 升级时建议使用与原安装相同架构的安装包，例如 x64 升级 x64。

## 6. 修复安装

如果程序文件损坏，可以执行修复：

```powershell
msiexec /fa "E:\Projects\DotNet\WearPartsControl\artifacts\installer\zh-CN\WearPartsControl-1.0.0-x64-zh-CN.msi" /qn /norestart /L*v "%TEMP%\WearPartsControl-repair.log"
```

常用修复参数：

- `/fa`：强制重新安装所有文件。
- `/fomus`：按需修复缺失/旧版本文件、快捷方式和注册表项。

## 7. 卸载

### 7.1 使用 MSI 文件卸载

```powershell
msiexec /x "E:\Projects\DotNet\WearPartsControl\artifacts\installer\zh-CN\WearPartsControl-1.0.0-x64-zh-CN.msi" /qn /norestart /L*v "%TEMP%\WearPartsControl-uninstall.log"
```

### 7.2 使用“应用和功能”卸载

也可以在 Windows “设置” → “应用” → “已安装的应用”中找到 Wear Parts Control 后卸载。

## 8. zip 包使用

CI/CD 打包脚本会同时生成 x64 和 x86 的 zip 包：

- `artifacts\zip\WearPartsControl-1.0.0-x64.zip`
- `artifacts\zip\WearPartsControl-1.0.0-x86.zip`

zip 包无需安装，解压到目标目录后运行 `WearPartsControl.exe` 即可。首次启动语言按当前 Windows 系统语言自动选择；如果系统语言不受支持，则默认使用英文。

## 9. 常用参数说明

| 参数 | 说明 |
| --- | --- |
| `/i` | 安装或升级 MSI |
| `/x` | 卸载 MSI |
| `/fa` | 修复安装，重新安装所有文件 |
| `/qn` | 静默安装，无界面 |
| `/norestart` | 安装后不自动重启 |
| `/L*v <日志路径>` | 生成详细安装日志 |
| `INSTALLFOLDER` | 指定安装目录 |
| `CREATE_DESKTOP_SHORTCUT` | `1` 创建桌面快捷方式，`0` 不创建 |
| `START_ON_LOGIN` | `1` 开机自启动，空值或 `0` 不自启动 |

## 10. 故障排查

1. 如果安装失败，先查看 `/L*v` 指定的日志文件。
2. 如果安装目录无权限，请以管理员身份打开 PowerShell 或 CMD。
3. 如果静默安装没有创建桌面快捷方式，请确认没有传入 `CREATE_DESKTOP_SHORTCUT="0"`。
4. 如果开机自启动状态与软件页面不一致，重新打开软件后进入“用户环境配置”页面确认；软件会以当前用户 Run 注册表项为准同步状态。
