# Overlay 游戏会话顶号 Implementation Plan

> **For agentic workers:** REQUIRED SKILL: Use implement to execute this plan task-by-task. Respect the handed-off git_state: inherit prepared, otherwise prepare; use git-workflow for checkpoint/finalize. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 本机互斥 + 跨设备 Steam 云会话租约，实现后开者顶号、被顶方短提示后退出。

**Scope Source:** `docs/scope/2026-09-04-overlay-session-takeover.md`（已批准 2026-09-04）

**Architecture:** 纯逻辑 `OverlaySessionLease` 负责租约序列化与 `ShouldYield`；`OverlaySessionGuard`（挂在 OverlayBootstrap 同 GO）负责命名互斥、Remote Storage 读写、心跳/轮询与退出提示；`OverlayBootstrap` 仅在启动早期接入，不改 `SteamManager`。

**Tech Stack:** Unity 2022.3、Steamworks.NET Remote Storage、`System.Threading.Mutex`；租约判定用 `tools/session-lease-tests` 控制台做 RED/GREEN（项目无 UTF）。

**Verification:** `dotnet run --project tools/session-lease-tests`；diff 确认未改 SteamManager；手工：本机双开、云降级启动。

---

### Task 1: 租约判定纯逻辑 + 失败测试

**Files:**
- Create: `Project/Assets/Scripts/Overlay/OverlaySessionLease.cs`
- Create: `Project/Assets/Scripts/Overlay/OverlaySessionLease.cs.meta`
- Create: `tools/session-lease-tests/session-lease-tests.csproj`
- Create: `tools/session-lease-tests/Program.cs`

**Acceptance:** 测试在缺少正确 `ShouldYield` 行为时失败；实现后全部通过。

- [x] **Step 1: Write the failing test**
- [x] **Step 2: Run test to verify it fails**
- [x] **Step 3: Write minimal OverlaySessionLease**
- [x] **Step 4: Run test to verify it passes**
- [x] **Step 5: Git checkpoint**（lease + tests）

### Task 2: OverlaySessionGuard（互斥 + 云租约 + 提示退出）

**Files:**
- Create: `Project/Assets/Scripts/Overlay/OverlaySessionGuard.cs`
- Create: `Project/Assets/Scripts/Overlay/OverlaySessionGuard.cs.meta`
- Modify: `Project/Assets/Scripts/Overlay/OverlayBootstrap.cs`

**Acceptance:** 启动先抢互斥；Steam 就绪后写租约并心跳；发现外来新鲜租约则提示约 2–3s 后退出；云失败不阻断启动。

- [x] **Step 1: 扩展测试覆盖心跳所属判定辅助（若有纯函数）或确认 Task1 已覆盖决策**
- [x] **Step 2: 实现 Guard + 接入 Bootstrap**
- [x] **Step 3: `dotnet run` 回归 + diff 确认未碰 SteamManager**
- [ ] **Step 4: Git checkpoint**

### Task 3: 收尾验证与 finalize

**Files:** 本任务产生的全部代码/文档

**Acceptance:** spec 验收项有证据或明确手工步骤；git finalize（commit 已在 checkpoint，push 按偏好）。

- [ ] **Step 1: 对照 spec 验收清单自检**
- [ ] **Step 2: git-workflow finalize**
