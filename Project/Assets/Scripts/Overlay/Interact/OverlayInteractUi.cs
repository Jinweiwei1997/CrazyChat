using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay.Interact
{
    public sealed class OverlayInteractUi : MonoBehaviour
    {
        const int SlotCount = 4;
        const float SlotSize = 36f;
        const float SlotGap = 8f;

        static readonly Vector2[] SlotDirections =
        {
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(0f, -1f),
            new Vector2(-1f, 0f)
        };

        FriendOverlayView _view;
        OverlayInteractService _service;
        OverlayInteractFx _fx;
        RectTransform _root;
        GameObject _ring;
        RectTransform _ringRt;
        readonly Image[] _slotBg = new Image[SlotCount];
        readonly Text[] _slotLabels = new Text[SlotCount];
        readonly IOverlayInteractAction[] _slotActions = new IOverlayInteractAction[SlotCount];
        ulong _openFor;
        float _nextUse;
        bool _hoverChip;
        bool _hoverRing;
        float _hideAt = -1f;
        float _showAt = -1f;
        ulong _pendingId;

        public static OverlayInteractUi Create(Transform chrome, Transform windows, FriendOverlayView view, OverlayInteractService service, OverlayInteractFx fx)
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

        void Build(Transform layer)
        {
            _root = (RectTransform)transform;
            Stretch(_root);

            _ring = new GameObject("ItemRing", typeof(RectTransform));
            _ring.transform.SetParent(layer, false);
            _ringRt = (RectTransform)_ring.transform;
            _ringRt.anchorMin = _ringRt.anchorMax = new Vector2(0f, 0f);
            _ringRt.pivot = new Vector2(0.5f, 0.5f);
            _ringRt.sizeDelta = Vector2.zero;

            for (var i = 0; i < SlotCount; i++)
            {
                BuildSlot(i, _ringRt);
            }

            RefreshSlots();
            _ring.SetActive(false);
        }

        void BuildSlot(int index, RectTransform parent)
        {
            var slot = CreateImage("Slot_" + index, parent, new Color(1f, 1f, 1f, 0.14f), OverlaySprites.RoundedRect);
            slot.raycastTarget = true;
            var rt = slot.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(SlotSize, SlotSize);
            _slotBg[index] = slot;
            _slotLabels[index] = FillLabel(rt, string.Empty, 11, OverlaySkin.Text);

            var capturedIndex = index;
            slot.gameObject.AddComponent<Button>().onClick.AddListener(() => UseSlot(capturedIndex));
            OverlayHoverRelay.Bind(slot.gameObject, HoverEnterFromRing, HoverLeaveFromRing);
        }

        void RefreshSlots()
        {
            var actions = OverlayInteractCatalog.All;
            for (var i = 0; i < SlotCount; i++)
            {
                _slotActions[i] = i < actions.Count ? actions[i] : null;
                var filled = _slotActions[i] != null;
                var bg = _slotBg[i];
                if (bg == null)
                {
                    continue;
                }

                bg.gameObject.SetActive(true);
                if (filled)
                {
                    OverlaySkin.ApplyButton(bg, accent: true);
                    bg.color = OverlaySprites.Accent;
                }
                else
                {
                    OverlaySkin.ApplyPanel(bg);
                    bg.color = new Color(1f, 1f, 1f, 0.14f);
                }

                bg.raycastTarget = true;
                var button = bg.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = filled;
                }

                if (_slotLabels[i] != null)
                {
                    _slotLabels[i].text = filled ? ShortSlotLabel(_slotActions[i].Label) : string.Empty;
                    _slotLabels[i].color = OverlaySkin.Text;
                }
            }
        }

        static string ShortSlotLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return string.Empty;
            }

            if (label.Length <= 3)
            {
                return label;
            }

            return label.StartsWith("扔") && label.Length > 1 ? label.Substring(1) : label;
        }

        public void ApplySkin()
        {
            RefreshSlots();
        }

        public void Sync()
        {
            if (_openFor != 0 && (_view == null || !_view.TryGetChip(_openFor, out var chip) || chip == null))
            {
                HideMenu();
            }
        }

        public void HideMenu()
        {
            _openFor = 0;
            _hoverChip = false;
            _hoverRing = false;
            _hideAt = -1f;
            _showAt = -1f;
            _pendingId = 0;
            if (_ring != null)
            {
                _ring.SetActive(false);
            }
        }

        public void NotifyChipHoverEnter(ulong friendId)
        {
            if (friendId == 0)
            {
                return;
            }

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

            if (_openFor != 0 && friendId != _openFor)
            {
                return;
            }

            _hoverChip = false;
            ScheduleHide();
        }

        void LateUpdate()
        {
            if (_view == null)
            {
                return;
            }

            if (_ring != null && _ring.activeSelf && _view.TryGetChip(_openFor, out var openChip) && openChip != null)
            {
                var scale = _view.Settings != null ? _view.Settings.Scale : 1f;
                var chipSize = _view.Config != null ? _view.Config.chipSize : 128f;
                PlaceRing(openChip.FollowPosition, chipSize, scale);
            }

            if (_showAt > 0f && Time.unscaledTime >= _showAt)
            {
                var id = _pendingId;
                _showAt = -1f;
                _pendingId = 0;
                if (_hoverChip && id != 0)
                {
                    Show(id);
                }
            }

            if (_ring != null && _ring.activeSelf && _hideAt > 0f && Time.unscaledTime >= _hideAt)
            {
                if (!_hoverChip && !_hoverRing)
                {
                    HideMenu();
                }
            }
        }

        void HoverEnterFromRing()
        {
            _hoverRing = true;
            _hideAt = -1f;
        }

        void HoverLeaveFromRing()
        {
            _hoverRing = false;
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
            if (_ring != null)
            {
                _ring.SetActive(true);
                _ring.transform.SetAsLastSibling();
            }
        }

        void ScheduleHide()
        {
            if (_hoverChip || _hoverRing)
            {
                return;
            }

            _hideAt = Time.unscaledTime + 0.22f;
        }

        void UseSlot(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                return;
            }

            Use(_slotActions[index]);
        }

        void Use(IOverlayInteractAction action)
        {
            if (action == null || _openFor == 0 || _view == null || Time.unscaledTime < _nextUse)
            {
                return;
            }

            if (!_view.TryGetChip(_openFor, out var target) || target == null || _view.LocalChip == null)
            {
                return;
            }

            var cooldown = _view.Config != null ? Mathf.Max(0f, _view.Config.interactCooldown) : 0.1f;
            _nextUse = Time.unscaledTime + cooldown;
            action.Play(_fx, _view.LocalChip.FollowPosition, target.FollowPosition);
            if (action.Id == "tomato")
            {
                _view.ApplyTomatoTapCost();
            }

            if (_service != null)
            {
                _service.Send(_openFor, action.Id);
            }
        }

        void PlaceRing(Vector2 avatarPos, float chipSize, float scale)
        {
            _ringRt.anchoredPosition = avatarPos;
            var radius = chipSize * 0.5f * scale + SlotSize * 0.5f + SlotGap;
            for (var i = 0; i < SlotCount; i++)
            {
                var bg = _slotBg[i];
                if (bg == null)
                {
                    continue;
                }

                bg.rectTransform.anchoredPosition = SlotDirections[i] * radius;
            }
        }

        static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
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
