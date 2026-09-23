# CrazyChat — 给 AI

改代码前**必须先读**：

1. **[docs/ai-code-guide.md](docs/ai-code-guide.md)** — 框架
2. **[docs/game-ui-rules.md](docs/game-ui-rules.md)** — 现行界面规则
3. **[.cursor/rules/overlay-window-focus.mdc](.cursor/rules/overlay-window-focus.mdc)** — 透明窗焦点/穿透硬约束（禁止 `AttachThreadInput`、软抢时机、切应用关聊天）

不要把 `docs/桌面互动伙伴需求.md` 当现行需求。

- 只改 `Project/Assets/Scripts/Overlay/`。先扩展现有类，不要新 Manager / 新框架 / 新通道。
- UI：设置使用 `Resources/Prefab/UI/SettingsMenu.prefab`，聊天使用 `Resources/Prefab/UI/ChatPanel.prefab`；其他界面保持现状。统一使用 uGUI + `Text`，挂已有 Canvas 层。
- 麻袋保留现有头像、名字、最后消息摘要、未读角标和点击聊天；互动、A/B、按键反应及桌面气泡仍只做桌上头像。
- 新互动：`IOverlayInteractAction` → Catalog → Fx → 通道 2。聊天走通道 1。
- `FriendOverlayView` 只接线；能进现有子系统就不要往 View 堆。
- 动 `TransparentOverlayWindow` / 聊天焦点前再读一遍 overlay-window-focus 硬约束，禁止为「能输入」重新引入 hardSteal。
