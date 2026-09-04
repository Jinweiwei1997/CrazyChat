> 由 scope skill 于 2026-09-04 生成
> 状态：已批准 2026-09-04

# Overlay 游戏会话顶号

## 目标

同一 Steam 账号在任意时刻只保留一份正在运行的 CrazyChat 游戏会话：后开者接管，先开者被踢下线并短提示后退出。别处仅登录 Steam、未开本游戏时不影响本机会话。不处理、不承诺修复 Steam 客户端的 `Logged In Elsewhere` 挤线。

## 决策基线

### 需求边界

- **支持**：跨设备游戏会话顶号（后开者获胜）；本机进程互斥（含 Unity Editor Play 与 Standalone/Export）；被顶方短提示后退出进程（Editor 则结束 Play）；Steam 云不可用时降级放行（跨设备顶号暂时失效，本机互斥仍生效）。
- **正常行为**：仅本机一份进程可持有互斥；跨设备后开实例覆盖会话租约，先开实例在轮询中发现后提示并退出；心跳超时后旧租约失效，不误踢新实例。
- **失败行为**：云读写失败 → 不阻断启动；抢不到本机互斥 → 提示后退出/停 Play。
- **明确不做**：阻止或改写 Steam 客户端多端登录挤线；自建鉴权服务器；新增 P2P/聊天通道；修改 `SteamManager` / `com.rlabrecque.steamworks.net`；开启或依赖设置项 `OverlayConfig.SteamCloud`（会话文件单独走 Remote Storage）。
- **兼容**：现有 Overlay 装配入口仍为 `OverlayBootstrap`；好友/聊天/互动行为不因顶号逻辑改变协议。

### 技术决策

- **本机互斥**：Windows 命名互斥；Editor 与 Standalone 共用同一逻辑；第二实例抢锁失败即退出路径。
- **跨设备租约**：Steam Remote Storage 独立会话文件（不绑定 `OverlayConfig.SteamCloud`）；启动生成 `sessionId` 并覆盖租约（后开者获胜）；周期性心跳续期；周期性读租约，发现「外来且更新的有效租约」则本端退出；对端崩溃靠心跳超时判定租约失效。
- **延迟预期**：跨设备顶号约数秒到十几秒（受 Steam 云同步影响）。
- **职责归属**：挂在现有 Overlay 启动链（`OverlayBootstrap` 及其邻近 Overlay 脚本）；禁止新 Bootstrap/Manager/通道；不改 Steamworks.NET 与 `SteamManager.cs`。
- **云降级**：Remote Storage 不可用时跳过跨设备租约，仅保留本机互斥。

## 设计视图

### 功能设计

用户启动 CrazyChat（Editor Play 或导出包）时：

1. 先尝试获取本机互斥。失败 → 提示「已在本机运行」类文案 → 退出/停 Play。
2. 互斥成功后走既有 Steam / Overlay 启动。若 Steam 已初始化且云可用，写入/覆盖会话租约并开始心跳与轮询。
3. 运行中若发现租约被其他 `sessionId` 以更新会话接管 → 提示「已在其他设备登录」类文案 → 约 2–3 秒后退出/停 Play。
4. 正常退出时尽量结束心跳；不要求强保证释放云租约（依赖超时）。
5. 他机仅开 Steam、未开本游戏 → 无租约竞争 → 本机不受影响。

### 技术设计

#### 整体方案

在 `OverlayBootstrap` 启动早期接入会话守卫（可同文件或 Overlay 目录下单一小类，避免新 Manager）：本机互斥 → 既有 `EnsureSteamManager` → 条件允许则启动租约 Writer/Poller。租约 payload 至少含 `sessionId`、启动时间、最近心跳时间。轮询比较：若文件中 `sessionId` 非本机且心跳仍新鲜（或启动时间晚于本机会话）→ 判定被顶。后开者启动时直接覆盖写入自己的租约。

#### 关键结构（按需）

- 租约文件：Remote Storage 上的固定文件名（实现选定，与现有 prefs/layout 文件名区分）。
- 互斥名：固定进程级名称（实现选定），Editor/Standalone 一致。

#### 实现流程

`AutoStart`/`Awake` → 获取互斥（失败则提示退出）→ 现有 Overlay 装配 → Steam 就绪后写租约并注册心跳/轮询 → 被顶则 UI 短提示 → `Application.Quit` 或 Editor 停 Play → `OnApplicationQuit`/`EndSteamSession` 路径保持清理 Rich Presence 等既有逻辑。

### 预估改动面

- `Project/Assets/Scripts/Overlay/`：`OverlayBootstrap` 及必要的会话守卫/提示辅助；可能极少量配置常量。
- 测试：本机双开（Editor↔Export 或双 exe）；单机云降级（无 Steam/写失败仍能进）；跨设备需双机或模拟租约文件覆盖的手工/半自动验证。
- wiki：不更新。

## 验收

- 本机已有一份 CrazyChat 在跑时再开第二份（含 Editor Play 与 Export 互换）→ 后开者提示后退出/停 Play，先开者继续 → 观察进程与提示文案。
- 仅本机一份、他处只登录 Steam 未开游戏 → 本机会话保持，不因「仅 Steam 在线」退出 → 观察进程持续运行。
- 两台设备先后打开本游戏且 Steam 云可用 → 后开者继续运行，先开者在可接受延迟内提示并退出 → 观察两端进程与提示。
- Steam 云不可用或写租约失败 → 本机仍可启动并受互斥约束；不因云失败拒启 → 断网/无 Init 时启动观察。
- 被顶方 → 可见短提示后退出，而非无提示闪退 → 观察 UI/日志。
- 未改 `SteamManager.cs` 与 `com.rlabrecque.steamworks.net/`；未新增 P2P 通道 → diff 审查。
