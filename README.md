# nook-desktop

nook-desktop 是一个基于 Avalonia 12 与 .NET 10 的桌面语音聊天客户端骨架,界面和信息架构对标 Oopz。

项目定位是开源、直接可用的桌面客户端:当前阶段聚焦于应用外壳、导航、MVVM 架构和各页面布局,真实账号、实时语音、聊天后端等能力尚未接入。启动后直接进入群组页,不提供登录注册、商城或发现页。

本文件只维护项目简介、运行方式和用户可感知的使用说明。常规代码改动无需同步更新;长期架构约定记录在 `MEMORY.md`。

## 当前状态

- 已完成无边框主窗口、边缘缩放、DWM 窗口圆角与标题栏设备浮层。
- 已完成群组页、群组内房间页、设置页、好友页、消息页和标题栏面板的界面骨架。
- 已建立 MVVM、导航和服务分层,页面通过构造函数获取依赖。
- 音频设备、语音会话、聊天和设置目前使用 `Services/PlaceholderServices.cs` 中的本地占位实现。
- 项目尚无测试工程,也未接入真实后端协议与持久化。

## 技术栈

- .NET 10
- Avalonia 12.1.2
- Fluent Theme
- C# / MVVM

## 运行环境

- Windows 10 或更高版本
- .NET 10 SDK

当前窗口外壳包含 Windows 专用的 DWM 圆角和 Win32 窗口行为,因此现阶段以 Windows 为主要运行目标。

## 构建与运行

```powershell
dotnet restore nook-desktop.slnx
dotnet build nook-desktop.slnx
dotnet run --project nook-desktop/nook-desktop.csproj
```

## 项目结构

```text
nook-desktop/
├─ Core/
│  ├─ Mvvm/         ObservableObject、RelayCommand、ViewModelBase
│  ├─ Navigation/   导航服务与 ViewModel/View 约定映射
│  └─ Shell/        无边框窗口缩放、圆角、标题栏浮层控制
├─ Models/          群组、频道、成员、聊天等领域模型
├─ Services/        服务接口、占位实现与组合根
├─ Themes/          配色、图标、按钮和控件样式
├─ ViewModels/      按页面和标题栏面板划分
├─ Views/           与 ViewModel 对应的 Avalonia 视图
├─ Assets/          应用图片资源
├─ App.axaml        应用资源、主题和 ViewLocator 注册
└─ MainWindow.axaml 主窗口布局与窗口内浮层
```

## 接入真实功能

业务接口位于 `nook-desktop/Services/`。接入真实后端时,在 `AppServices.CreateDefault()` 中替换占位实现即可,ViewModel 层无需感知具体实现。

`GroupsViewModel.SeedPlaceholderGroups()` 和 `RoomViewModel.SeedPlaceholderMembers()` 仅用于在没有后端时展示页面,接入真实数据后应删除。

## 开发文档

- [MEMORY.md](MEMORY.md):仅在涉及架构决策或长期约束时按需查阅。
- [AGENTS.md](AGENTS.md):代码代理在本仓库中的工作约定。

## 许可证

本项目使用根目录下的 [LICENSE.txt](LICENSE.txt)。
