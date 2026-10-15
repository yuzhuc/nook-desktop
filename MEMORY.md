# MEMORY

本文件只记录当前有效、无法从局部源码直接看出且会影响后续开发的长期约定，不记录任务过程、待办、提交历史或易变实现细节。仅当相关领域变化时更新；源码与本文件冲突时以源码为准。

## 项目

- nook-desktop 是 Avalonia 12 + .NET 10 的桌面语音聊天客户端，界面、交互、菜单、弹窗与动画按 **Oopz 逐像素复刻**。
- 明确不做：商城、加速器、发现页（设置页导航里的「全球加速」只是照 Oopz 排的一项占位）。
- 现阶段**只做 UI**：布局、悬浮气泡、禁用态、菜单都是静态实现，未接账号、语音与后端。占位实现见 `Services/PlaceholderServices.cs`。
- 目标平台以 Windows 为主，窗口圆角依赖 DWM。

## 分支与仓库

- `main` 稳定，`dev` 开发；日常提交默认推 dev，需要合回 main 时再说。
- `build-run.bat` 是本地脚本，**不进版本库**（已在 `.gitignore`）。它做三件事：关掉旧 nook.exe → `dotnet build nook-desktop.slnx` → 启动 exe。
- 曾有过「推 main 自动构建到 build 分支」的 workflow，已删除；自动构建到 build 分支的远端分支也已删除。保留 `.github/workflows/build.yml`（手动打包发 Releases）。
- 已内置 `.vs/` 忽略；此前误提交的 `.vs` 缓存已移出版本库。

## 关键实现

- 组合根 `AppServices.CreateDefault()`：`SqliteGroupStore` + `SqliteChatService` + `AppConfigStore`，音频/语音/设置仍是占位实现。
- 本地数据：`%APPDATA%\nook\nook.db`（域/频道分组/频道/成员/消息/元信息）、`%APPDATA%\nook\settings.json`（窗口尺寸，拖动结束写回一次）。
- 依赖通过构造函数逐层注入；页面由 `ViewLocator` 按 `XxxViewModel` / `XxxView` 命名映射。
- 程序集名是 `nook`，资源 URI 一律 `avares://nook/...`；改 `AssemblyName` 必须同步改 `App.axaml`、`MainWindow.axaml`。
- `WindowEdgeResizer` 负责无边框缩放（事件必须走隧道阶段）；`WindowCorners` 用 DWM；标题栏浮层在窗口内渲染，**不要改用 Flyout**，位置由 `TitlePanelController` / `AlignGroupSettingsMenu` 这类方法对齐。
- 设置页：每个分栏放在 `Views/Settings/SettingsView.axaml`，用 `IsVisible="{Binding IsXxxSection}"` 包裹，导航项与分栏一一对应；导航区在 `ScrollViewer` 里（宿主行必须是星号行，否则滚不动——这个坑踩过一次）。
- 域设置页同理，在 `RoomView.axaml` 的 `GroupSettingsPage` 里，分栏枚举 `GroupSettingsSection`。

## 与 Oopz 的对照资料

- `docs/oopz-interaction.md`：各区域左右键实测行为与差异。
- `docs/oopz-settings-spec.md`：设置页逐页规格。
- 对照截图与自动化脚本放在 `C:\Users\21220\.codex\visualizations\2026\10\12\01a1303a-4c9f-76b3-ba4b-410384b36d42\`（`ooseq.ps1` 点/滚/截图、`otop2.ps1` 置顶 Oopz、`n-XX-页面-top/mid/bot.png` 是设置页截图）。

## 工作方式约定（踩过的坑）

- 改 XAML/C# 用「逐行读 + 锚点断言 + 按行号插入/替换」的脚本，不要整份重写；写任何文件前后都断言锚点。
- `semantic-cache` 的 write/edit 会把行尾写成 `CR CR LF`，写完后要规整回 CRLF；仓库文件用 UTF-8 无 BOM。
- `bin\Debug\net10.0\nook.exe` 经常被正在运行的实例占用，编译会失败（MSB3021）；要么先 `Stop-Process nook`，要么 `-o <临时目录>` 验证。
- 抓 Oopz 窗口：`PrintWindow` 对它是白屏，必须先用真实鼠标点它自己的标题栏 / `SetWindowPos(TOPMOST)` 把它提到最前，再用 `CopyFromScreen` 截图；自动化点击要先把光标移过去，PostMessage 对 Flutter 窗口不可靠。

## 已知事项

- 设置页里的开关、下拉、按钮只切换外观，没有接服务；`账号设置` 的身份信息（用户名 / ID / IP）是写死的占位。
- 顶栏话筒/耳机禁用态图标（`IconMicOff` / `IconHeadphoneOff`）是「原图标 + 斜杠」合成的，缩放后与原图标有轻微重影，待描成单体图标。
- `更新记录` 页 v1.4.4 的修复条目与底部文章标题在对照截图里被裁掉，是按同版本样式补的，可能与 Oopz 逐字有出入。
- `账号设置` 还缺 Oopz 的账号安全 / 实名认证 / 第三方账号 / 注销账号几段；`语音设置` 还缺「语音改善」模块和真实试麦电平条。
- 目前没有测试工程；`.github/workflows/build.yml` 负责手动发布打包。
