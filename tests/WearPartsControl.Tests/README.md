# WearPartsControl.Tests

SaveInfo 存储模块单元测试：

- 自动类型映射到 json 文件
- 自定义映射文件与目录
- 文件缺失时返回默认实例
- 路径穿越防护

WPF 加载与界面回归测试：

- 主窗口、零件编辑窗口和主要 UserControl 的 InitializeComponent / XAML 加载回归
- 壳层控件加载路径覆盖 `LoginBox`、`NeedLoginUserControl`、`UserTabControl`、`MainWindowTrayContentControl`
