> 由 scope skill 于 2026-09-10 生成
> 状态：已批准 2026-09-10

# Windows 系统托盘常驻小图标

## 目标

Standalone（`CrazyChat.exe`）启动后，在 Windows 通知区出现常驻小图标，表明进程在运行，并提供右键退出；不依赖任务栏按钮，也不要求强制进入「显示隐藏的图标」溢出区。

## 决策基线

### 需求边界

- **支持**：仅 Windows Standalone / 打包 exe；启动成功后通知区出现托盘图标；悬停提示为 `CrazyChat`；右键菜单仅「退出」；左键点击无任何操作；正常退出、会话顶号退出或进程结束时移除图标。
- **退出路径**：托盘「退出」与设置页「退出游戏」一致，调用同一退出路径（Standalone 下 `Application.Quit()`），走现有 Bootstrap / SessionGuard 清理。
- **隐藏图标区**：不强制、不保证图标进入「显示隐藏的图标」（`^`）；是否显式或藏入溢出区由 Windows / 用户偏好决定。只要出现在通知区即满足需求。
- **失败行为**：托盘创建失败不阻断 Overlay / 游戏启动；无托盘时仍可从设置退出。
- **明确不做**：Unity Editor Play 显示托盘；托盘打开设置/聊天/显示隐藏头像；左键气泡提示；强制搬迁到溢出区；改造任务栏按钮行为；新 Manager / Bootstrap / 事件总线 / 新 P2P 通道；改 `SteamManager` / Steamworks.NET；麻袋相关改动。
- **兼容**：装配入口仍为 `OverlayBootstrap`；桌上头像、聊天、互动、A/B、本机互斥与跨设备租约行为不变。

### 技术决策

- **接线**：在 `OverlayBootstrap` 启动路径创建与销毁托盘；新增 Overlay 目录下的小型辅助类（如 `OverlayTrayIcon`），不新建 Manager / 第二套 UI 框架。
- **平台**：仅 `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`（或等价门控）启用；Editor 与非 Windows 构建无托盘代码路径副作用。
- **实现机制**：Windows `NotifyIcon`（WinForms）或等价 `Shell_NotifyIcon`；菜单仅一项退出。
- **图标资源**：优先使用现有应用 / 打包窗口图标；工程未配置专用图标时可用 Unity 默认应用图标；不在本需求中新设计美术资源。
- **生命周期**：本机互斥失败、尚未完成本地启动的实例不创建托盘；已创建的实例在 `OnApplicationQuit` / 销毁路径显式 Dispose，避免幽灵图标。
- **文档**：获批实施时同步更新 `docs/game-ui-rules.md` 增加托盘条目；不更新 wiki（仓库无 `docs/wiki/`）。

## 设计视图

### 功能设计

1. 用户启动 `CrazyChat.exe` 且本机互斥取得成功后，通知区出现托盘图标。
2. 鼠标悬停显示 `CrazyChat`；左键无反应；右键仅「退出」。
3. 选择「退出」后进程结束，图标消失；设置内退出同样移除图标。
4. Editor Play 无托盘；托盘创建失败时游戏仍可照常使用。

### 技术设计

#### 整体方案

- **`OverlayTrayIcon`（新，Overlay 下）**：封装图标加载、NotifyIcon 创建、右键退出、Dispose。
- **`OverlayBootstrap`**：互斥成功并进入正式启动后 `Show`；`OnApplicationQuit` / 销毁时 `Hide`/`Dispose`。
- **退出**：托盘菜单回调复用与 `OverlaySettingsUi.QuitGame` 相同的 Standalone 退出语义，避免第二套退出逻辑。

#### 实现流程

`AutoStart` 取得互斥 → Bootstrap `Awake` 完成必要初始化 → 创建托盘 → 运行中右键退出或设置退出 → `Application.Quit` → Bootstrap 清理 + 托盘 Dispose → 图标消失。互斥失败走 `BeginQuit`，不创建托盘。

### 预估改动面

- `Project/Assets/Scripts/Overlay/OverlayBootstrap.cs`：接线创建/销毁。
- `Project/Assets/Scripts/Overlay/` 新增托盘辅助类（命名以实现为准）。
- 可能补充 `.ico` 或从现有 Player/窗口图标派生的资源（若运行时无法直接取到图标）。
- `docs/game-ui-rules.md`：补充托盘常驻与交互规则。
- 测试：Standalone 启动见图标；右键退出；设置退出图标消失；双开第二实例无残留托盘；Editor Play 无托盘。
- wiki：不更新。

## 验收

- 启动 `Export/*/CrazyChat.exe`（或等价 Standalone）→ 通知区出现图标，悬停为 `CrazyChat` → 目视。
- 左键点击图标 → 无菜单、无气泡、无界面变化 → 目视。
- 右键 → 仅「退出」→ 点击后进程退出且图标消失 → 任务管理器 / 通知区。
- 设置页「退出游戏」→ 进程退出且图标消失 → 同上。
- Unity Editor Play → 无托盘图标 → 目视。
- 本机已有一份 CrazyChat 再开第二份 → 第二份提示后退出，不留下额外托盘图标 → 目视。
- 托盘创建失败（模拟/缺权限等）→ Overlay 仍可启动，可从设置退出 → 日志或断点 + 目视。
- 未新增 Manager / 通道；未改 SteamManager → diff。
- `docs/game-ui-rules.md` 已写明托盘规则 → 文档审阅。
