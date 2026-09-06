# Chat Bubble Compact Implementation Plan

> **For agentic workers:** REQUIRED SKILL: Use implement to execute this plan task-by-task. Respect the handed-off git_state: inherit prepared, otherwise prepare; use git-workflow for checkpoint/finalize. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Compact chat above avatar (peer-last→now) + side panel full history via OverlayChatUi modes.

**Scope Source:** `docs/scope/2026-09-06-chat-bubble-compact.md`

**Architecture:** Extend `OverlayChatUi` with Compact (anchor to chip bubble) vs Full (existing side panel). View opens Compact by default; Compact has「全部消息」→ Full; close Full returns Compact.

**Tech Stack:** Unity uGUI, existing OverlayChatService/Store.

**Verification:** Manual flow + batch Win64 build to Export/0.2.

---

### Task 1: Spec checkpoint + helpers

- [ ] Checkpoint approved spec
- [ ] Add message slice helper (peer last → end) testable static method on OverlayChatStore or OverlayChatUi

### Task 2: OverlayChatUi Compact/Full

- [ ] Mode enum; OpenCompact / OpenFull / CloseFullToCompact / Hide
- [ ] Compact layout at bubble anchor; Full keeps side panel
- [ ] 「全部消息」left of send; Escape/close per mode
- [ ] Keep input focus after send

### Task 3: View wiring + bubble preview

- [ ] OpenChat → Compact; targeting Enter → Compact
- [ ] Chip bubble always shows latest or "…"
- [ ] Selection refresh with modes

### Task 4: Build Export/0.2 + finalize

- [ ] Batch build; commit code+spec+plan+Export; push
