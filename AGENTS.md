# CrazyChat — 给 AI

改代码前读 **[docs/ai-code-guide.md](docs/ai-code-guide.md)**。那是框架约定，不是需求说明书。

- 只改 `Project/Assets/Scripts/Overlay/`。先扩展现有类，不要新 Manager / 新框架 / 新通道。
- UI：`Create()` 运行时拼，uGUI + `Text`，挂已有 Canvas 层。
- 新互动：`IOverlayInteractAction` → Catalog → Fx → 通道 2。聊天走通道 1。
- `FriendOverlayView` 只接线；能进现有子系统就不要往 View 堆。
