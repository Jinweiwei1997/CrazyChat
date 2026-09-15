# Resident Chat Bubble Implementation Plan

> **For agentic workers:** REQUIRED SKILL: Use implement to execute this plan task-by-task. Respect the handed-off git_state: inherit prepared, otherwise prepare; use git-workflow for checkpoint/finalize. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a permanent `…` bubble on desktop friend avatars when there is no unread; keep unread carousel as today.

**Scope Source:** `docs/scope/2026-09-15-resident-chat-bubble.md`

**Architecture:** Extend `FriendAvatarChip` bubble visibility/content only; chat open/close and compact rules unchanged.

**Tech Stack:** Unity uGUI Overlay scripts

**Verification:** Batch rebuild Export/0.4; smoke that process starts; manual bubble cases listed in spec acceptance.

---

### Task 1: Chip idle bubble

**Files:**
- Modify: `Project/Assets/Scripts/Overlay/FriendAvatarChip.cs`

- [x] When unread == 0, set bubble content to `…` and show bubble if chat-capable and not expanded
- [x] When unread > 0, keep carousel + badge behavior
- [x] Do not show on local self chip

### Task 2: Rules sync

**Files:**
- Modify: `docs/game-ui-rules.md`
- Modify: `.cursor/rules/game-ui-rules.mdc`

- [x] Replace “有未读时才显示气泡” with resident `…` / unread carousel rules

### Task 3: Verify and ship

- [x] Rebuild `Export/0.4`
- [x] Smoke exe stays alive
- [x] Commit spec/plan/code/docs/export; push `main`
