# AGENTS.md

## 项目约定

- 默认使用中文沟通;代码、命令、路径和错误信息保留原文。
- 默认先读取相关源码、配置和最近的局部改动;只有任务涉及项目背景、构建运行方式、架构、接口或长期约定时,才按需读取 `README.md` 或 `MEMORY.md` 的相关部分。

## Semantic Cache MCP

- 每个用户请求开始后,先调用 `semantic-cache` 的 `stats`;简单问答也必须完成这次调用,用于确认 MCP 可用并记录本回合使用。
- 涉及项目文件时,优先使用 `semantic-cache` 的 `glob`、`warm`、`grep`、`search`、`read` 和 `batch_read` 完成检索与读取;写入、修改优先使用 `write`、`edit` 或 `batch_edit`。
- 如果 `semantic-cache` 未注入、启动失败或调用超时,不得静默跳过;必须明确告知用户不可用,再使用普通工具完成任务。
- `clear` 和 `delete` 仅在用户明确要求或确认缓存异常时调用,不得例行调用。

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
- 只有用户明确要求时才更新 `MEMORY.md` 或 `README.md`,不要自行写入。