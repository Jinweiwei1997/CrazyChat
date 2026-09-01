# CrazyChat 框架约定（给 AI）

先扩展现有类，不要新开一层。目标是多个 AI 改同一套 Overlay 时，结构不膨胀、不重复。

业务代码只在 `Project/Assets/Scripts/Overlay/`。不要改 `Steamworks.NET/SteamManager.cs` 和 `com.rlabrecque.steamworks.net/`。

---

## 禁止把框架做厚

- 不要新 Bootstrap、新 Manager、新 Facade、新 EventBus、新 UI 框架。入口永远是 `OverlayBootstrap`，装配永远在 `FriendOverlayView.Build`。
- `FriendOverlayView` 只接线：Rebuild、桌上/麻袋分配、把事件交给已有子系统。能进 Chip / *Ui / *Service / *Store 的，不要再堆进 View。
- 同类职责已经有类就改它，不要平行再写一个。例如聊天不要第二套 P2P，设置不要第二套 JSON，特效不要第二套 Canvas。
- 不要为「以后扩展」预留抽象。接口只保留 `IOverlayInteractAction` 这一个插件点。
- 新文件的门槛：现有文件加进去会明显乱。否则加进现有文件。
- 列表用 `Dictionary<id, 实例>` + stale 销毁，不要每帧拆重建，也不要再包一套对象池框架。

---

## 现有骨架（往里填，别在旁边另搭）

```
OverlayBootstrap          自动拉起，只组装一次
  PlayingFriendsService   Steam 好友列表
  FriendOverlayView       中枢
    FriendAvatarChip      桌上头像
    OverlayBagUi          麻袋
    OverlayChat*          聊天（通道 1）
    OverlayInteract*      互动（通道 2，目录 Interact/）
    OverlaySettings*      用户设置 UI
    OverlayInput*         按键监听 / 图标
    OverlayLayoutStore    头像位置
    OverlayTapStats       敲击计数
    OverlayConfig         策划常量
  TransparentOverlayWindow 透明置顶 + 点击穿透
OverlaySprites            圆/圆角/字体，UI 共用
```

策划常量 → `OverlayConfig`。用户能改的 → `OverlayUserSettings`。不要把开关同时写进两处。

---

## 加功能时走哪条路

| 要做的 | 做法 |
|--------|------|
| 新互动 | `IOverlayInteractAction` + `OverlayInteractCatalog` 登记一行 + `OverlayInteractFx` 出视觉 + `OverlayInteractService.Send`。不要改聊天。 |
| 新 UI 面板 | 静态 `Create(parent, view)`，挂到已有层上（见下）。照抄 `OverlayChatUi` / `OverlaySettingsUi` 的拼法。 |
| 新用户选项 | 字段加进 `OverlayUserSettings` + 一行加进 `OverlaySettingsUi`。 |
| 新持久化字段 | 加进已有 JSON（layout / prefs / stats / chat），不要新文件。SteamId 必须存 string。 |
| 新联网消息 | 聊天走通道 1（`CC1\|`），互动/同步走通道 2（`IX1\|`）。不要开通道 3，不要复用聊天文本当协议。 |

点击反应不是菜单互动：用已有 `OverlayTapSync`，不要登记进 Catalog。

---

## UI 写法（保持同构）

运行时拼，不用 Prefab / TMP / UI Toolkit。

```csharp
public static XxxUi Create(Transform parent, FriendOverlayView view)
{
    var root = new GameObject("XxxUi", typeof(RectTransform));
    root.transform.SetParent(parent, false);
    var ui = root.AddComponent<XxxUi>();
    ui.Build();
    return ui;
}
```

- `sealed class`，私有 `_camelCase`，命名空间 `CrazyChat.Overlay`（互动用 `.Interact`）
- 图：`OverlaySprites.Circle` / `RoundedRect`；字：`OverlaySprites.UiFont`
- 装饰 `raycastTarget = false`（否则桌面点击穿透会坏）
- 坐标：屏幕像素，原点左下；跟随用 Chip 的 `FollowPosition`，贴边翻到另一侧
- 时间：`Time.unscaledTime`
- Steam 调用包在 `#if !DISABLESTEAMWORKS`；Win 窗口相关只在 `STANDALONE_WIN && !EDITOR`

Canvas 已有层，按类型挂，不要新建 Canvas：

`BagLayer` → 麻袋；`FriendLayer` → 头像；`FxLayer` → 飞行特效；`ChromeLayer` → 小按钮/图标；`WindowLayer` → 聊天卡/菜单；`ModalLayer` → 遮罩面板。

---

## 需求不清时的保底

用户没说清楚时按这些做，不要发明更大的产品范围：

- 无房间。好友来自 Steam 在线列表。
- 玩法、聊天、动效、设置只做**已经拿出来的桌上头像**。麻袋默认展示 + 拖进拖出；没点名就不动 `OverlayBagUi`。
- 对方能看到互动/聊天的前提：对方开着本游戏。不要做离线队列、房间、匹配、云端聊天。
- 数字（头像边长、桌上人数、冷却）读 `OverlayConfig`，不要在新代码里再写一套。
