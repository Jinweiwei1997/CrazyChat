# Avatar A/B Presence Implementation Plan

> **For agentic workers:** REQUIRED SKILL: Use implement to execute this plan task-by-task. Respect the handed-off git_state: inherit prepared, otherwise prepare; use git-workflow for checkpoint/finalize. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Local A/B avatar images with key/mouse presence, settings upload, and channel-2 sync to desktop friends.

**Scope Source:** `docs/scope/2026-09-05-avatar-ab-presence.md`（已批准 2026-09-05）

**Architecture:** Local PNG files + prefs version; `OverlayInputWatcher.IsAnyDown` drives local Chip; `OverlayAvatarPresence` on View GO syncs `ab|p` / chunked `ab|c` over existing `OverlayInteractService` channel 2; Chip skips bounce when presence skins active.

**Tech Stack:** Unity uGUI, SteamNetworkingMessages IX1|, Win file dialog / Editor panel.

**Verification:** `tools/avatar-codec-tests` (csc) for resize/enable rules; manual Play for UI/sync; diff excludes SteamManager.

---

### Task 1: Codec + protocol helpers + RED/GREEN

**Files:**
- Create: `Project/Assets/Scripts/Overlay/OverlayAvatarCodec.cs`
- Create: `Project/Assets/Scripts/Overlay/Interact/OverlayAvatarSync.cs`
- Create: `tools/avatar-codec-tests/*`

**Acceptance:** Enable only when A+B present; chunk encode/decode roundtrip; max dimension/size constants match spec.

- [ ] Step 1–4: tests + codec/sync helpers
- [ ] Step 5: checkpoint docs/code helpers

### Task 2: Settings + Input + Chip + Presence wiring

**Files:**
- Modify: `OverlayUserSettings.cs`, `OverlaySettingsUi.cs`, `OverlayInputWatcher.cs`, `FriendAvatarChip.cs`, `FriendOverlayView.cs`
- Create: `OverlayAvatarPresence.cs` (+ meta)

**Acceptance:** Upload/clear in settings; local A/B switch on press/release; sync to desktop friends; bag unchanged.

- [ ] Implement + wire
- [ ] Regression codec tests
- [ ] checkpoint + finalize
