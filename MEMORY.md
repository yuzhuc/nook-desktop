# MEMORY

本文件只记录当前有效、无法从局部源码直接看出且会影响后续开发的长期约定,不记录任务过程、待办、提交历史或易变实现细节。仅在相关领域变化时更新;普通代码改动无需读取或更新。源码与本文件冲突时以源码为准。

## 项目

- nook-desktop 是 Avalonia 12 + .NET 10 的桌面语音聊天客户端。
- 当前只完成界面和架构骨架,没有真实后端、账号、语音或聊天能力。
- 目标平台以 Windows 为主,窗口圆角依赖 DWM。

## 关键实现

- `AppServices.CreateDefault()` 装配全部服务,当前使用 `PlaceholderServices.cs` 的占位实现。
- 依赖通过构造函数逐层注入 ViewModel;页面由 `ViewLocator` 按命名约定映射。
- `WindowEdgeResizer` 负责无边框缩放,事件必须走隧道阶段。
- `WindowCorners` 使用 DWM 设置窗口圆角,窗口状态变化后要重新应用。
- 标题栏面板在窗口内部渲染,不使用 `Flyout`;位置由 `TitlePanelController` 对齐。
- 程序集名是 `nook`,所有资源 URI 使用 `avares://nook/...`。

## 已知事项

- `GroupsViewModel.SeedPlaceholderGroups()` 和 `RoomViewModel.SeedPlaceholderMembers()` 是演示数据。
- `Assets/logo.png` 可能仍显示旧的 `boop` 字标。
- 主窗口标题栏和房间页存在同步的固定宽度,改布局时要一起改。
- 当前没有测试工程;`.github/workflows/build.yml` 负责发布打包,`.github/workflows/deploy-branch.yml` 负责部署到 `build` 分支。
