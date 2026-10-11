# nook-desktop

基于 Avalonia 12 + .NET 10 的桌面语音聊天客户端骨架(对标 Oopz)。开源项目,无登录注册、无商城、无发现,开屏直接展示群组。当前只搭建架构与界面框架,不含业务功能。

## 目录结构

    Core/Mvvm/         ObservableObject、RelayCommand(含泛型与异步)、ViewModelBase
    Core/Navigation/   INavigationService、NavigationService、ViewLocator
    Core/Shell/        无边框窗口外壳:边缘缩放、DWM 圆角、标题栏浮层定位
    Models/            Group、Channel、RoomMember、ChatMessage 等领域模型
    Services/          服务接口 + 本地占位实现 + AppServices 组合根
    Themes/            Palette(配色)、Icons(矢量图标)、Controls/ButtonTheme(控件样式)
    ViewModels/        按页面划分:Groups(开屏)、Room、Settings、TitleBar(标题栏弹出面板)
    Views/             对应页面,与 ViewModel 通过 ViewLocator 约定映射

## 约定

- ViewModel 命名 `XxxViewModel`,视图命名 `XxxView`,放在同名的 `ViewModels` / `Views` 命名空间下,`ViewLocator` 会自动解析。
- Avalonia 12 默认开启编译绑定,新增视图必须声明 `x:DataType`。
- 所有样式集中在 `Themes`,界面内不写死颜色与圆角。
- 依赖通过构造函数注入:`AppServices` 在 `App.OnFrameworkInitializationCompleted` 里装配一次,
  再逐层传给 ViewModel。不要在 ViewModel 里引用静态服务入口。
- 标题栏的悬停弹层不使用 `Flyout`:Flyout 是独立顶层窗口,会画到窗口外面。
  浮层作为窗口内容的最后一层渲染,位置由 `Core/Shell/TitlePanelController` 运行时对齐。
- 图标设计约束:几何包围盒**必须是方形**。`Stretch="Uniform"` 对扁长几何(如单条横线)
  不会做垂直居中,会顶到上方。扁平图形改用 `Border` 由布局居中,不依赖几何缩放。

## 无边框窗口

`WindowDecorations="None"` 会连 `WS_THICKFRAME` 一并去掉,`BeginResizeDrag` 会完全失效,
因此缩放由 `Core/Shell/WindowEdgeResizer` 处理:命中四边六像素带,按下后捕获指针,
手动驱动 `Width`/`Height`/`Position`。命中与缩放都注册在隧道阶段 —— 边缘常压在群组卡片
这类 Button 上,冒泡阶段的事件会被子控件标记为已处理。

窗口圆角走 `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE)`(见 `WindowCorners`),
由系统直接裁剪窗口形状,不依赖窗口透明。该属性在最大化/还原后会被系统重置,所以窗口状态
变化时要重新应用。

## 接入真实功能

`AppServices.CreateDefault()` 目前装配的是 `Services/PlaceholderServices.cs` 里的本地实现,
接入真实后端时替换该方法的构造参数即可,ViewModel 层不用改动。

`GroupsViewModel.SeedPlaceholderGroups()` 与 `RoomViewModel.SeedPlaceholderMembers()` 提供
占位数据,仅用于在没有后端时走通界面,接入真实数据源后删除。