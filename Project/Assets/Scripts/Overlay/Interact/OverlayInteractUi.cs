using CrazyChat.Overlay.Fishing;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay.Interact
{
    public sealed class OverlayInteractUi : MonoBehaviour
    {
        const int SlotCount = 4;
        const float ButtonSize = 36f;
        const float ButtonGap = 4f;
        const float AvatarGap = 6f;
        const int SelfFishSlot = 0;
        const int SelfQteSlot = 1;

        FriendOverlayView _view;
        OverlayInteractService _service;
        OverlayInteractFx _fx;
        OverlayFishingController _fishing;
        RectTransform _root;
        GameObject _ring;
        RectTransform _ringRt;
        readonly Image[] _slotBg = new Image[SlotCount];
        readonly Text[] _slotLabels = new Text[SlotCount];
        readonly IOverlayInteractAction[] _slotActions = new IOverlayInteractAction[SlotCount];
        int _hoverSlot = -1;
        ulong _openFor;
        float _nextUse;
        bool _hoverChip;
        bool _hoverRing;
        float _hideAt = -1f;
        float _showAt = -1f;
        ulong _pendingId;
        bool _selfMode;

        public static OverlayInteractUi Create(
            Transform chrome,
            Transform windows,
            FriendOverlayView view,
            OverlayInteractService service,
            OverlayInteractFx fx)
        {
            var root = new GameObject("InteractUi", typeof(RectTransform));
            root.transform.SetParent(chrome, false);
            var ui = root.AddComponent<OverlayInteractUi>();
            ui._view = view;
            ui._service = service;
            ui._fx = fx;
            ui.Build(windows != null ? windows : chrome);
            return ui;
        }

        public void BindFishing(OverlayFishingController fishing) => _fishing = fishing;

        void Build(Transform layer)
        {
            _root = (RectTransform)transform;
            Stretch(_root);

            _ring = new GameObject("InteractionButtons", typeof(RectTransform));
            _ring.transform.SetParent(layer, false);
            _ringRt = (RectTransform)_ring.transform;
            _ringRt.anchorMin = _ringRt.anchorMax = new Vector2(0f, 0f);
            _ringRt.pivot = new Vector2(0.5f, 0.5f);

            _ringRt.sizeDelta = new Vector2(ButtonSize, ButtonSize);

            for (var i = 0; i < SlotCount; i++)
            {
                BuildSlot(i, _ringRt);
            }

            RefreshSlots();
            _ring.SetActive(false);
        }

        void BuildSlot(int index, RectTransform parent)
        {
            var slot = CreateImage("Button_" + index, parent, Color.white, null);
            slot.type = Image.Type.Simple;
            slot.raycastTarget = true;
            var rt = slot.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * ButtonSize;
            rt.anchoredPosition = Vector2.zero;
            _slotBg[index] = slot;

            var capturedIndex = index;
            slot.gameObject.AddComponent<Button>().onClick.AddListener(() => UseSlot(capturedIndex));
            OverlayHoverRelay.Bind(slot.gameObject,
                () => HoverEnterSlot(capturedIndex),
                () => HoverLeaveSlot(capturedIndex));

            var label = FillLabel(parent, string.Empty, 12, OverlaySkin.Text);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 9;
            label.resizeTextMaxSize = 12;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            _slotLabels[index] = label;
        }

        void PlaceSlot(int index, int count)
        {
            var height = count * ButtonSize + Mathf.Max(0, count - 1) * ButtonGap;
            _ringRt.sizeDelta = new Vector2(ButtonSize, height);
            var position = new Vector2(0f, (height - ButtonSize) * 0.5f - index * (ButtonSize + ButtonGap));
            if (_slotBg[index] != null) _slotBg[index].rectTransform.anchoredPosition = position;
            if (_slotLabels[index] != null) _slotLabels[index].rectTransform.anchoredPosition = position;
        }

        void RefreshSlots()
        {
            if (_selfMode)
            {
                RefreshSelfSlots();
                return;
            }

            var actions = OverlayInteractCatalog.All;
            var theme = _view != null && _view.Settings != null ? _view.Settings.SettingsTheme : 1;
            var count = Mathf.Min(actions.Count, SlotCount);
            for (var i = 0; i < SlotCount; i++)
            {
                _slotActions[i] = i < count ? actions[i] : null;
                if (i < count)
                {
                    PlaceSlot(i, count);
                    ApplySlotVisual(i, true, ShortSlotLabel(actions[i].Label), selected: false, theme);
                }
                else
                {
                    HideSlot(i);
                }
            }
        }

        void RefreshSelfSlots()
        {
            var theme = _view != null && _view.Settings != null ? _view.Settings.SettingsTheme : 1;
            var fishingOn = _fishing != null && _fishing.IsFishing;
            var testMode = _view != null && _view.Settings != null && _view.Settings.TestMode;
            var count = testMode ? 2 : 1;
            for (var i = 0; i < SlotCount; i++)
            {
                _slotActions[i] = null;
                if (i == SelfFishSlot)
                {
                    PlaceSlot(i, count);
                    ApplySlotVisual(i, true, "钓鱼", fishingOn, theme);
                }
                else if (i == SelfQteSlot && testMode)
                {
                    PlaceSlot(i, count);
                    ApplySlotVisual(i, true, "QTE", selected: false, theme);
                }
                else
                {
                    HideSlot(i);
                }
            }
        }

        void HideSlot(int index)
        {
            if (_slotBg[index] != null) _slotBg[index].gameObject.SetActive(false);
            if (_slotLabels[index] != null) _slotLabels[index].gameObject.SetActive(false);
        }

        void ApplySlotVisual(int i, bool filled, string label, bool selected, int theme)
        {
            var bg = _slotBg[i];
            if (bg == null) return;

            bg.gameObject.SetActive(true);
            bg.sprite = null;
            bg.type = Image.Type.Simple;
            if (selected)
            {
                var accent = OverlaySkin.ThemeAccent(theme);
                accent.a = 0.85f;
                bg.color = accent;
            }
            else if (filled && _hoverSlot == i)
            {
                var accent = OverlaySkin.ThemeAccent(theme);
                accent.a = 0.6f;
                bg.color = accent;
            }
            else if (filled)
            {
                var normal = OverlaySkin.ThemeControl(theme);
                normal.a = 0.92f;
                bg.color = normal;
            }
            else
            {
                var disabled = OverlaySkin.ThemeControl(theme);
                disabled.a = 0.45f;
                bg.color = disabled;
            }

            var button = bg.GetComponent<Button>();
            if (button != null) button.interactable = filled;

            if (_slotLabels[i] != null)
            {
                _slotLabels[i].gameObject.SetActive(true);
                _slotLabels[i].text = label;
                _slotLabels[i].color = OverlaySkin.SettingsThemeText(theme);
            }
        }

        static string ShortSlotLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return string.Empty;
            if (label.Length <= 3) return label;
            if (label.StartsWith("测试")) return "测试";
            return label.StartsWith("扔") && label.Length > 1 ? label.Substring(1) : label;
        }

        public void ApplySkin() => RefreshSlots();

        public void Sync()
        {
            if (_openFor != 0 && (_view == null || !_view.TryGetChip(_openFor, out var chip) || chip == null))
                HideMenu();
            else if (_ring != null && _ring.activeSelf && _selfMode)
                RefreshSelfSlots();
        }

        public void HideMenu()
        {
            _openFor = 0;
            _selfMode = false;
            _hoverChip = false;
            _hoverRing = false;
            _hoverSlot = -1;
            _hideAt = -1f;
            _showAt = -1f;
            _pendingId = 0;
            if (_ring != null) _ring.SetActive(false);
        }

        public void NotifyChipHoverEnter(ulong friendId)
        {
            if (friendId == 0) return;
            _hoverChip = true;
            _hideAt = -1f;
            if (_ring != null && _ring.activeSelf && _openFor == friendId)
            {
                _showAt = -1f;
                _pendingId = 0;
                return;
            }

            ScheduleShow(friendId);
        }

        public void NotifyChipHoverExit(ulong friendId)
        {
            if (_pendingId == friendId)
            {
                _showAt = -1f;
                _pendingId = 0;
            }

            if (_openFor != 0 && friendId != _openFor) return;

            _hoverChip = false;
            ScheduleHide();
        }

        void LateUpdate()
        {
            if (_view == null) return;

            if (_ring != null && _ring.activeSelf && _view.TryGetChip(_openFor, out var openChip) && openChip != null)
            {
                var scale = _view.Settings != null ? _view.Settings.Scale : 1f;
                PlaceRing(openChip.FollowPosition, scale);
            }

            if (_showAt > 0f && Time.unscaledTime >= _showAt)
            {
                var id = _pendingId;
                _showAt = -1f;
                _pendingId = 0;
                if (_hoverChip && id != 0) Show(id);
            }

            if (_ring != null && _ring.activeSelf && _hideAt > 0f && Time.unscaledTime >= _hideAt)
            {
                if (!_hoverChip && !_hoverRing) HideMenu();
            }
        }

        void HoverEnterSlot(int index)
        {
            _hoverRing = true;
            _hideAt = -1f;
            if (_hoverSlot == index) return;
            _hoverSlot = index;
            RefreshSlots();
        }

        void HoverLeaveSlot(int index)
        {
            _hoverRing = false;
            if (_hoverSlot == index)
            {
                _hoverSlot = -1;
                RefreshSlots();
            }

            ScheduleHide();
        }

        void ScheduleShow(ulong friendId)
        {
            var delay = _view != null && _view.Config != null
                ? Mathf.Max(0f, _view.Config.hoverOpenSeconds)
                : 0.2f;
            if (delay <= 0f)
            {
                Show(friendId);
                return;
            }

            _pendingId = friendId;
            _showAt = Time.unscaledTime + delay;
        }

        void Show(ulong friendId)
        {
            _showAt = -1f;
            _pendingId = 0;
            _view?.HideSettings();
            _openFor = friendId;
            _selfMode = _view != null && _view.LocalChip != null && _view.LocalChip.SteamId == friendId;
            _hoverSlot = -1;
            RefreshSlots();
            if (_ring != null)
            {
                if (_view.TryGetChip(friendId, out var chip) && chip != null)
                    PlaceRing(chip.FollowPosition, _view.Settings != null ? _view.Settings.Scale : 1f);
                _ring.SetActive(true);
                _ring.transform.SetAsLastSibling();
            }
        }

        void ScheduleHide()
        {
            if (_hoverChip || _hoverRing) return;
            _hideAt = Time.unscaledTime + 0.22f;
        }

        void UseSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return;
            if (_selfMode)
            {
                UseSelfSlot(index);
                return;
            }

            Use(_slotActions[index]);
        }

        void UseSelfSlot(int index)
        {
            if (_fishing == null || _view == null || Time.unscaledTime < _nextUse)
                return;

            var cooldown = _view.Config != null ? Mathf.Max(0f, _view.Config.interactCooldown) : 0.1f;
            if (index == SelfFishSlot)
            {
                _nextUse = Time.unscaledTime + cooldown;
                _fishing.ToggleLocal();
                HideMenu();
                return;
            }

            if (index == SelfQteSlot
                && _view.Settings != null
                && _view.Settings.TestMode)
            {
                _nextUse = Time.unscaledTime + cooldown;
                _view.SimulateFishingQte();
                HideMenu();
            }
        }

        void Use(IOverlayInteractAction action)
        {
            if (action == null || _openFor == 0 || _view == null || Time.unscaledTime < _nextUse)
                return;

            if (!_view.TryGetChip(_openFor, out var target) || target == null || _view.LocalChip == null)
                return;

            var cooldown = _view.Config != null ? Mathf.Max(0f, _view.Config.interactCooldown) : 0.1f;
            _nextUse = Time.unscaledTime + cooldown;
            if (action.Id == "test_message")
            {
                _view.SimulateIncomingChat(_openFor);
                return;
            }

            action.Play(_fx, _view.LocalChip.FollowPosition, target.FollowPosition);
            if (action.Id == "tomato")
                _view.ApplyTomatoTapCost();

            if (_service != null)
                _service.Send(_openFor, action.Id);
        }

        void PlaceRing(Vector2 avatarPos, float scale)
        {
            var chipSize = _view.Config != null ? _view.Config.chipSize : 128f;
            var position = avatarPos + Vector2.left * (chipSize * 0.5f + AvatarGap + ButtonSize * 0.5f) * scale;
            var halfHeight = _ringRt.sizeDelta.y * scale * 0.5f;
            var bounds = FriendOverlayView.OverlayPixelSize;
            position.x = Mathf.Max(ButtonSize * scale * 0.5f, position.x);
            position.y = Mathf.Clamp(position.y, halfHeight, Mathf.Max(halfHeight, bounds.y - halfHeight));
            _ringRt.anchoredPosition = position;
            _ringRt.localScale = new Vector3(scale, scale, 1f);
        }

        void OnDestroy()
        {
            if (_ring != null) Destroy(_ring);
        }

        static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = color;
            return image;
        }

        static Text FillLabel(Transform parent, string text, int size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = OverlaySprites.UiFont;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            Stretch((RectTransform)go.transform);
            return label;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
