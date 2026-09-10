# Desktop Stealth Implementation Plan

> **For agentic workers:** REQUIRED SKILL: Use implement to execute this plan task-by-task. Respect the handed-off git_state: inherit prepared, otherwise prepare; use git-workflow for checkpoint/finalize. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Global RMB hold 3s fades/hides desktop overlay chrome; hold again or tray reveals.

**Scope Source:** `docs/scope/2026-09-10-desktop-stealth.md` (已批准 2026-09-10)

**Architecture:** `OverlayInputWatcher` exposes right-button held; `OverlayStealthController` drives CanvasGroup + cursor ring HUD; `FriendOverlayView` wraps hideable layers; `OverlayTrayIcon` adds「取消隐藏」when hidden.

**Tech Stack:** Unity uGUI CanvasGroup, Image Filled Radial360, GetAsyncKeyState via existing watcher.

**Verification:** Unity batch compile + manual Editor/Standalone checklist from spec (no NotifyIcon-style unit harness).

---

### Task 1: Input + Stealth controller/HUD + View wiring

**Files:**
- Modify: `Project/Assets/Scripts/Overlay/OverlayInputWatcher.cs`
- Create: `Project/Assets/Scripts/Overlay/OverlayStealthController.cs` (+ meta)
- Modify: `Project/Assets/Scripts/Overlay/FriendOverlayView.cs`
- Modify: `Project/Assets/Scripts/Overlay/OverlayBootstrap.cs`
- Modify: `Project/Assets/Scripts/Overlay/OverlayTrayIcon.cs`

**Acceptance:** Hold RMB 3s hides content with ring「隐藏」; release early snaps visible; when hidden hold shows「取消隐藏」and fades in; tray reveal immediate.

- [x] Implement + compile
- [x] Git checkpoint

### Task 2: Docs

**Files:**
- Modify: `docs/game-ui-rules.md`
- Modify: `docs/ai-code-guide.md` (skeleton line)

- [x] Update rules
- [ ] Finalize commit/push
