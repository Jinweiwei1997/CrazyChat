# Desktop Fishing Implementation Plan

> **For agentic workers:** REQUIRED SKILL: Use implement to execute this plan task-by-task. Respect the handed-off git_state: inherit prepared, otherwise prepare; use git-workflow for checkpoint/finalize. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Local self-status fishing on desktop avatar with rod/water, bite schedule, low/high catch + QTE, points via OverlayTapStats, and channel-2 sync of enter/catch/exit to desktop friends.

**Scope Source:** `docs/scope/2026-09-18-desktop-fishing.md`（已批准 2026-09-18）

**Architecture:** `OverlayFishingController` wired from `FriendOverlayView.Build`; self status ring extends `OverlayInteractUi` (not Catalog Play); visuals on Chrome; QTE on WindowLayer; `fish|e|c|x` on Interact channel 2.

**Tech Stack:** Unity uGUI, OverlayConfig fish table, OverlayTapStats, OverlayInteractService IX1|.

**Verification:** Static review of Overlay fishing types; manual Play Mode for ring/bite/QTE/sync; diff excludes SteamManager / bag play UI.

---

### Task 1: Config + fish defs + controller core

- [x] Controller + Config fields + default fish rows

### Task 2: Visuals + QTE

- [x] Visuals + QTE + rod sprite under Resources

### Task 3: Self status ring + local hover

- [x] OverlayInteractUi self mode; Chip/View local hover

### Task 4: Net + desk lifecycle + docs

- [x] fish| receive path, desk add/remove, game-ui-rules

### Task 5: Verify + finalize

- [ ] Static review
- [ ] git-workflow finalize commit/push
