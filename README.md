# nook-desktop

基于 Avalonia 12 与 .NET 10 的桌面语音聊天客户端，界面、交互、右键菜单和弹窗都按 Oopz 逐像素复刻。

范围：**不做商城、加速器、发现页**（设置页里的「全球加速」只作为 OOPZ 设置导航的一项占位）。当前阶段**只做 UI**：页面布局、动画、悬浮气泡、禁用态都是静态实现，未接后端与真实语音/账号能力。

对照资料与长期约定分别见 [docs/](docs/) 与 [MEMORY.md](MEMORY.md)，代码代理的工作约定见 [AGENTS.md](AGENTS.md)。

## 分支

- `main`：稳定版
- `dev`：开发分支，日常提交默认推这里

## 当前状态

- 无边框主窗口 + 边缘缩放 + DWM 圆角；窗口尺寸拖动结束写回配置。
- 左栏域栏、域内频道页、私信/好友页、设置页、标题栏设备浮层均已按 Oopz 排版。
- 域、频道分组、频道、成员、聊天消息走本地 SQLite，重启后仍在。
- 顶栏话筒/耳机气泡：点话筒开关麦克风、点耳机开关静音听，禁用态换成红色带斜杠图标。
- 设置页 12 个分栏（账号、隐私、语音、按键、外观、通知、屏幕覆盖、全球加速、主播模式、更新记录、系统设置、帮助反馈）都有内容，左栏可滚动，底部固定版本号与「退出登录」。
- 音频设备、语音会话、设置仍是 `Services/PlaceholderServices.cs` 的占位实现；界面里的开关只切换外观，不改真实状态。

## 技术栈

- .NET 10
- Avalonia 12.1.2（Fluent Theme、Inter 字体）
- Microsoft.Data.Sqlite
- C# / MVVM，依赖全部构造函数注入

## 运行环境

Windows 10 或更高版本，.NET 10 SDK。窗口外壳依赖 Windows 专用的 DWM 圆角与 Win32 行为。

## 构建与运行

```powershell
dotnet restore nook-desktop.slnx
dotnet build nook-desktop.slnx
dotnet run --project nook-desktop/nook-desktop.csproj
```

本地也可以用仓库根目录的 `build-run.bat`（关掉旧进程 → 构建 → 启动）。这个脚本**不进版本库**，已写进 `.gitignore`。

> 提示：`bin\Debug\net10.0\nook.exe` 正在运行时 `dotnet build` 会写不进 bin，需要先关掉应用，或 `dotnet build ... -o <临时目录>` 验证编译。

## 本地数据

| 路径 | 内容 |
| --- | --- |
| `%APPDATA%\nook\settings.json` | 窗口宽高；首次启动写默认值，拖动结束写回 |
| `%APPDATA%\nook\nook.db` | 域 / 频道分组 / 频道 / 成员 / 聊天消息 / 元信息 |

演示数据只在建库时写入一次；删掉 `nook.db` 即回到初始状态。

## 项目结构

```text
nook-desktop/
├─ Core/
│  ├─ Mvvm/         ObservableObject、RelayCommand、ViewModelBase
│  ├─ Navigation/   导航服务与 ViewModel/View 约定映射
│  └─ Shell/        无边框缩放、DWM 圆角、标题栏浮层、命中测试
├─ Models/          域、频道、成员、聊天等领域模型
├─ Services/        服务接口、SQLite 实现、占位实现与组合根
├─ Themes/          配色、图标、按钮和控件样式
├─ ViewModels/      按页面、标题栏面板、设置分栏划分
├─ Views/           与 ViewModel 对应的 Avalonia 视图
├─ Assets/          应用图片资源
├─ App.axaml        应用资源、主题和 ViewLocator 注册
└─ MainWindow.axaml 主窗口布局、域栏与窗口内浮层

docs/               与 Oopz 的对照记录
```

## 架构约定

- 组合根是 `AppServices.CreateDefault()`：域/频道/消息走 `SqliteGroupStore` + `SqliteChatService`，音频、语音、设置是占位实现。接真实后端只改这里。
- 页面保持 `XxxViewModel` / `XxxView` 同层级配对，由 `ViewLocator` 按命名映射。
- 程序集名是 `nook`，资源 URI 用 `avares://nook/...`；改 `AssemblyName` 要同步改 `App.axaml`、`MainWindow.axaml`。
- 无边框窗口：缩放走隧道阶段，圆角用 DWM，标题栏浮层在窗口内渲染、不用 `Flyout`。
- 设置页每个分栏用 `IsVisible="{Binding IsXxxSection}"` 包裹，导航项与分栏一一对应。
- XAML 必须声明 `x:DataType`，颜色与控件样式集中放 `Themes/`。

## 与 Oopz 的对照资料

- [docs/oopz-interaction.md](docs/oopz-interaction.md)：顶栏、左栏、频道栏、聊天区、成员栏、域设置的左右键实测行为。
- [docs/oopz-settings-spec.md](docs/oopz-settings-spec.md)：设置页逐页规格（分组、每一行、滚动内容）。

## 许可证

本项目使用根目录下的 [LICENSE.txt](LICENSE.txt)。
