# nook-desktop

基于 Avalonia 12 与 .NET 10 的桌面语音聊天客户端，界面、交互和右键菜单 1:1 对标 Oopz。

范围明确：**不做商城、加速器、发现页**。当前阶段界面已按 Oopz 复刻完成，域/频道/成员/消息都在本机 SQLite 里，真实账号、好友、实时语音和后端协议尚未接入。

长期架构约定见 [MEMORY.md](MEMORY.md)，代码代理的工作约定见 [AGENTS.md](AGENTS.md)。

## 当前状态

- 已完成无边框主窗口、边缘缩放、DWM 窗口圆角，窗口尺寸拖动结束后写回配置。
- 已完成域栏、域内频道页、私信/好友页、设置页和标题栏设备浮层。
- 已完成本地 SQLite 存储：域、频道分组、频道、成员、聊天消息；重启后仍在。
- 音频设备、语音会话、设置仍是 `Services/PlaceholderServices.cs` 里的本地占位实现。
- 无测试工程，无真实后端协议。

## 功能现状

- **域栏（左栏 70px）**：置顶的域从上往下排在最上面、未置顶的从下往上贴着底部，中间一条分界线；域多时整块一起滚动，不显示滚动条。底部固定「＋」，弹窗里可选创建域或加入域；右键域图标可置顶/取消置顶/删除，删除后自动切到下一个域，没有域就回私信页。选中竖线贴在窗口左边缘并随切换滑动。
- **域内频道页**：主页固定在顶部，下面是没有归组的主频道和各分组（每组上面一条分界线）。⋮ 是群组设置菜单，频道右键菜单包含邀请其他人、开启免打扰、编辑频道、创建频道、复制频道、删除频道。
- **聊天区**：消息列表按同作者连续消息折叠抬头，输入条回车即发送（展开态回车留给换行）；点展开后输入区铺满聊天区，顶部是 B/S/I/U/旗工具栏，右下角是表情和回车。
- **成员栏**：常态是「在线 - N」+ 放大镜，点放大镜后搜索框和底线窗帘式下拉、成员列表平滑下移；右边三条横线收起/展开整栏；纸飞机按钮手动加成员。
- **私信/好友页**：私信会话列表（含系统通知卡片流）+ 在线/全部/好友请求/黑名单四个筛选标签。
- **标题栏**：logo、搜索框（点击弹出搜索窗口）、话筒/耳机设备气泡面板、设置/最小化/最大化/关闭。
- **未实现**：真实账号与好友体系、语音收发、服务器同步；好友列表目前只有空状态文案。

## 技术栈

- .NET 10
- Avalonia 12.1.2（Fluent Theme、Inter 字体）
- Microsoft.Data.Sqlite
- C# / MVVM，依赖全部构造函数注入

## 运行环境

Windows 10 或更高版本，.NET 10 SDK。

窗口外壳依赖 Windows 专用的 DWM 圆角和 Win32 行为，现阶段以 Windows 为主要运行目标。

## 构建与运行

```powershell
dotnet restore nook-desktop.slnx
dotnet build nook-desktop.slnx
dotnet run --project nook-desktop/nook-desktop.csproj
```

或者直接双击仓库根目录的 `build-run.bat`：关掉正在运行的 nook.exe → 构建 → 启动。

## 本地数据

| 路径 | 内容 |
| --- | --- |
| `%APPDATA%\nook\settings.json` | 窗口宽高，首次启动写入默认值，之后只读、拖动结束写回 |
| `%APPDATA%\nook\nook.db` | 域 / 频道分组 / 频道 / 成员 / 聊天消息 / 元信息 |

演示数据只在建库时写入一次，之后删光了也不会自己长回来；删掉 `nook.db` 即可回到初始状态。配置文件坏掉或尺寸不合法会退回默认值，不影响启动。

## 项目结构

```text
nook-desktop/
├─ Core/
│  ├─ Mvvm/         ObservableObject、RelayCommand、ViewModelBase
│  ├─ Navigation/   导航服务与 ViewModel/View 约定映射
│  └─ Shell/        无边框窗口缩放、DWM 圆角、标题栏浮层、命中测试
├─ Models/          域、频道、成员、聊天等领域模型
├─ Services/        服务接口、SQLite 实现、占位实现与组合根
├─ Themes/          配色、图标、按钮和控件样式
├─ ViewModels/      按页面和标题栏面板划分
├─ Views/           与 ViewModel 对应的 Avalonia 视图
├─ Assets/          应用图片资源
├─ App.axaml        应用资源、主题和 ViewLocator 注册
└─ MainWindow.axaml 主窗口布局、域栏与窗口内浮层
```

## 架构约定

- 组合根是 `AppServices.CreateDefault()`：群组/频道/消息走 `SqliteGroupStore` + `SqliteChatService`，音频、语音、设置仍是占位实现。接真实后端时只改这里，ViewModel 不用动。
- 页面保持 `XxxViewModel` / `XxxView` 同层级配对，由 `ViewLocator` 按命名约定映射。
- 程序集名是 `nook`，所有资源 URI 使用 `avares://nook/...`；改 `AssemblyName` 要同步改 `App.axaml` 和 `MainWindow.axaml`。
- 无边框窗口：缩放逻辑走隧道阶段，圆角用 DWM，标题栏浮层在窗口内渲染、不用 `Flyout`。
- 侧栏、成员栏、标题栏存在互相牵制的固定宽度，改布局时一起改，并重新对照 Oopz 截图核对像素。
- XAML 必须声明正确的 `x:DataType`，颜色和控件样式集中放在 `Themes/`。

## 持续集成

- `.github/workflows/deploy-branch.yml`：推 main 时按文件顶部的开关选择目标平台，构建自包含包并更新 `build` 分支。
- `.github/workflows/build.yml`：手动触发，构建各平台包并上传到 Releases。

## 许可证

本项目使用根目录下的 [LICENSE.txt](LICENSE.txt)。
