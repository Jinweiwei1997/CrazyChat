# Windows System Tray Icon Implementation Plan

> **For agentic workers:** REQUIRED SKILL: Use implement to execute this plan task-by-task. Respect the handed-off git_state: inherit prepared, otherwise prepare; use git-workflow for checkpoint/finalize. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Standalone Windows exe shows a notification-area tray icon with tooltip `CrazyChat` and right-click Quit only.

**Scope Source:** `docs/scope/2026-09-10-system-tray-icon.md` (已批准 2026-09-10)

**Architecture:** Add `OverlayTrayIcon` under Overlay; wire from `OverlayBootstrap` after mutex success. Win32 `Shell_NotifyIcon` + message-only HWND (no WinForms; project is .NET Standard). Editor / non-Windows: no component.

**Tech Stack:** Unity 2022 Standalone Mono, P/Invoke (`shell32` / `user32`), existing Bootstrap lifecycle.

**Verification:** Spec acceptance is visual Standalone-only. No Unity test harness for NotifyIcon. Substitute (confirmed by approved spec 验收): Unity batch compile if available + manual Standalone checklist from spec. No Export rebuild unless user asks.

---

### Task 1: OverlayTrayIcon + Bootstrap wiring

**Files:**
- Create: `Project/Assets/Scripts/Overlay/OverlayTrayIcon.cs`
- Modify: `Project/Assets/Scripts/Overlay/OverlayBootstrap.cs`
- Verify: Unity batchmode compile (if Editor present) or static review of `#if` gates

**Acceptance:** Win Standalone creates tray after Awake; Dispose/NIM_DELETE on destroy/quit; Editor does not add component; failures do not throw out of Bootstrap.

- [x] **Step 1: Document failing observable (no automated RED harness)**

Observable missing today: Standalone has no notification-area icon. Gate: `OverlayTrayIcon` type does not exist.

- [x] **Step 2: Confirm compile / type absence**

Run: search repo for `OverlayTrayIcon` — expect no production type.

- [x] **Step 3: Minimal implementation**

Implement `OverlayTrayIcon` MonoBehaviour:
- Message-only HWND + `Shell_NotifyIcon(NIM_ADD/DELETE)`
- Tip `CrazyChat`; icon from exe via `ExtractIcon` / fallback `LoadIcon` IDI_APPLICATION
- Callback: left button ignore; right button popup menu with only「退出」→ `Application.Quit()`
- `Update` PeekMessage/DispatchMessage for tray HWND
- Guarded try/catch so add failure logs and continues
- `#if UNITY_STANDALONE_WIN` for native path; runtime skip when `Application.isEditor`

Wire in `OverlayBootstrap.Awake` after successful instance setup under `#if UNITY_STANDALONE_WIN` + `!Application.isEditor`.

Ensure `OnDestroy` / quit path deletes icon (component destroy on application quit is enough; keep explicit delete idempotent).

- [x] **Step 4: Compile check**

Unity batchmode exit 0; `Export/tray-compile2.log` — no `error CS`; `OverlayTrayIcon.cs` imported.

- [x] **Step 5: Regression**

Confirm `OverlaySessionGuard` mutex-fail path still returns before Bootstrap Awake (no tray). Confirm Settings Quit still `Application.Quit()`.

- [ ] **Step 6: Git checkpoint**

Commit tray code files only.

---

### Task 2: Update game-ui-rules

**Files:**
- Modify: `docs/game-ui-rules.md`
- Optional mention: `docs/ai-code-guide.md` skeleton line for OverlayTrayIcon (only if keeps guide accurate without scope creep)

**Acceptance:** Rules document tray presence, tooltip, left no-op, right quit, Standalone-only, no force overflow.

- [x] **Step 1: Add section under 桌面主界面或全局 / 系统**

Add concise bullets matching spec 需求边界.

- [x] **Step 2: Review wording vs spec**

No Editor tray; no force `^` overflow; quit same as 退出游戏.

- [ ] **Step 3: Git checkpoint**

Commit docs with code or separate checkpoint if code already committed.

---

### Task 3: Finalize

**Files:** none new

- [ ] Manual checklist handed to user (spec 验收)
- [ ] `git-workflow` finalize: commit remaining, rebase/push `main`
