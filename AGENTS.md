# AGENTS.md

## 基本约定

- 默认使用中文沟通;代码、命令、路径和错误信息保留原文。
- 只修改用户指定的内容,不顺手重构;不覆盖未提交改动。
- 删除、覆盖、批量修改、上传或操作账号前先确认。
- 先读 `README.md` 和 `MEMORY.md`,再做非简单任务。

## 项目规则

- 技术栈:Avalonia 12.1.2、.NET 10、C# MVVM。
- 命名空间是 `nook_desktop`;程序集名是 `nook`。
- 修改 `AssemblyName` 时,同步修改 `App.axaml` 和 `MainWindow.axaml` 的 `avares://nook/`。
- 依赖通过 `AppServices` 和构造函数注入,不要在 ViewModel 中使用静态服务入口。
- 页面保持 `XxxViewModel` / `XxxView` 命名和同层级配对。
- XAML 必须声明正确的 `x:DataType`;颜色和控件样式放在 `Themes/`。
- 标题栏浮层不要改用 `Flyout`,无边框缩放逻辑必须保留隧道阶段处理。
- 修改主窗口、侧栏或房间页宽度时,同步检查对齐约束。

## 完成标准

- C#、XAML 或项目配置变更后执行 `dotnet build nook-desktop.slnx`。
- UI 或窗口行为变更后启动应用做交互检查。
- 架构、接口或长期约定变化时同步更新 `MEMORY.md`,并删除已被替代的过时记忆;使用方式变化时更新 `README.md`。
