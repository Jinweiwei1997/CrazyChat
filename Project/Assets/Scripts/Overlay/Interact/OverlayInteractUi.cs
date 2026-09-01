using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay.Interact
{
    public sealed class OverlayInteractUi : MonoBehaviour
    {
        FriendOverlayView _view;
        OverlayInteractService _service;
        OverlayInteractFx _fx;
        RectTransform _root;
        GameObject _menu;
        RectTransform _menuRt;
        Image _menuImage;
        ulong _openFor;
        float _nextUse;
        bool _hoverButton;
        bool _hoverMenu;
        float _hideAt = -1f;
        float _showAt = -1f;
        ulong _pendingId;
        readonly Dictionary<ulong, RectTransform> _buttons = new Dictionary<ulong, RectTransform>();

        public static OverlayInteractUi Create(Transform chrome, Transform windows, FriendOverlayView view, OverlayInteractService service, OverlayInteractFx fx)
        {
            var root = new GameObject("InteractUi", typeof(RectTransform));
            root.transform.SetParent(chrome, false);
            var ui = root.AddComponent<OverlayInteractUi>();
            ui._view = view;
            ui._service = service;
            ui._fx = fx;
            ui.Build(windows);
            return ui;
        }

        void Build(Transform windows)
        {
            _root = (RectTransform)transform;
            Stretch(_root);

            _menu = new GameObject("Menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _menu.transform.SetParent(windows != null ? windows : _root, false);
            _menuImage = _menu.GetComponent<Image>();
            OverlaySkin.ApplyPanel(_menuImage);
            _menuImage.raycastTarget = true;
            _menuRt = (RectTransform)_menu.transform;
            _menuRt.anchorMin = _menuRt.anchorMax = new Vector2(0f, 0f);
            _menuRt.pivot = new Vector2(0.5f, 0.5f);
            var actions = OverlayInteractCatalog.All;
            _menuRt.sizeDelta = new Vector2(120f, 16f + actions.Count * 36f);
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var row = CreateImage(action.Id, _menuRt, OverlaySprites.Accent, OverlaySprites.RoundedRect);
                OverlaySkin.ApplyButton(row, accent: true);
                row.raycastTarget = true;
                var rowRt = row.rectTransform;
                rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -10f - i * 36f);
                rowRt.sizeDelta = new Vector2(100f, 30f);
                FillLabel(rowRt, action.Label, 13, OverlaySkin.Text);
                var captured = action;
                row.gameObject.AddComponent<Button>().onClick.AddListener(() => Use(captured));
            }

            _menu.SetActive(false);
            OverlayHoverRelay.Bind(_menu, HoverEnterFromMenu, HoverLeaveFromMenu);
        }

        public void ApplySkin()
        {
            OverlaySkin.ApplyPanel(_menuImage);
            if (_menuRt != null)
            {
                var rows = _menuRt.GetComponentsInChildren<Image>(true);
                for (var i = 0; i < rows.Length; i++)
                {
                    if (rows[i] == _menuImage)
                    {
                        continue;
                    }

                    OverlaySkin.ApplyButton(rows[i], accent: true);
                }

                var labels = _menuRt.GetComponentsInChildren<Text>(true);
                for (var i = 0; i < labels.Length; i++)
                {
                    labels[i].color = OverlaySkin.Text;
                }
            }

            foreach (var pair in _buttons)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                OverlaySkin.ApplyPanel(pair.Value.GetComponent<Image>());
                var label = pair.Value.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.color = OverlaySkin.Text;
                }
            }
        }

        public void Sync()
        {
            var seen = new HashSet<ulong>();
            if (_view != null)
            {
                _view.VisitDesktopFriends(chip =>
                {
                    seen.Add(chip.SteamId);
                    if (!_buttons.ContainsKey(chip.SteamId))
                    {
                        _buttons[chip.SteamId] = CreateButton(chip.SteamId);
                    }
                });
            }

            var stale = new List<ulong>();
            foreach (var pair in _buttons)
            {
                if (!seen.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                if (_buttons.TryGetValue(stale[i], out var rt) && rt != null)
                {
                    Destroy(rt.gameObject);
                }

                _buttons.Remove(stale[i]);
                if (_openFor == stale[i])
                {
                    HideMenu();
                }
            }

            ApplySkin();
        }

        public void HideMenu()
        {
            _openFor = 0;
            _hoverButton = false;
            _hoverMenu = false;
            _hideAt = -1f;
            _showAt = -1f;
            _pendingId = 0;
            if (_menu != null)
            {
                _menu.SetActive(false);
            }
        }

        void LateUpdate()
        {
            if (_view == null)
            {
                return;
            }

            var scale = _view.Settings != null ? _view.Settings.Scale : 1f;
            var chipSize = _view.Config != null ? _view.Config.chipSize : 128f;
            _view.VisitDesktopFriends(chip =>
            {
                if (!_buttons.TryGetValue(chip.SteamId, out var button) || button == null)
                {
                    return;
                }

                var pos = chip.FollowPosition;
                var offset = new Vector2(chipSize * 0.5f * scale + 28f, -10f * scale);
                if (pos.x + offset.x + 28f > Screen.width)
                {
                    offset.x = -offset.x;
                }

                button.anchoredPosition = pos + offset;
            });

            if (_menu != null && _menu.activeSelf && _view.TryGetChip(_openFor, out var openChip) && openChip != null)
            {
                PlaceMenu(openChip.FollowPosition, chipSize, scale);
            }

            if (_showAt > 0f && Time.unscaledTime >= _showAt)
            {
                var id = _pendingId;
                _showAt = -1f;
                _pendingId = 0;
                if (_hoverButton && id != 0)
                {
                    Show(id);
                }
            }

            if (_menu != null && _menu.activeSelf && _hideAt > 0f && Time.unscaledTime >= _hideAt)
            {
                if (!_hoverButton && !_hoverMenu)
                {
                    HideMenu();
                }
            }
        }

        void HoverEnterFromButton(ulong friendId)
        {
            _hoverButton = true;
            _hideAt = -1f;
            if (_menu != null && _menu.activeSelf && _openFor == friendId)
            {
                _showAt = -1f;
                _pendingId = 0;
                return;
            }

            ScheduleShow(friendId);
        }

        void HoverLeaveFromButton(ulong friendId)
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

            _hoverButton = false;
            ScheduleHide();
        }

        void HoverEnterFromMenu()
        {
            _hoverMenu = true;
            _hideAt = -1f;
        }

        void HoverLeaveFromMenu()
        {
            _hoverMenu = false;
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
            if (_menu != null)
            {
                _menu.SetActive(true);
                _menu.transform.SetAsLastSibling();
            }
        }

        void ScheduleHide()
        {
            if (_hoverButton || _hoverMenu)
            {
                return;
            }

            _hideAt = Time.unscaledTime + 0.22f;
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

        void PlaceMenu(Vector2 avatarPos, float chipSize, float scale)
        {
            var size = _menuRt.sizeDelta;
            var offsetX = chipSize * 0.5f * scale + 16f + size.x * 0.5f;
            if (avatarPos.x + offsetX + size.x * 0.5f > Screen.width - 12f)
            {
                offsetX = -offsetX;
            }

            var pos = avatarPos + new Vector2(offsetX, 8f);
            pos.x = Mathf.Clamp(pos.x, 12f + size.x * 0.5f, Screen.width - 12f - size.x * 0.5f);
            pos.y = Mathf.Clamp(pos.y, 12f + size.y * 0.5f, Screen.height - 12f - size.y * 0.5f);
            _menuRt.anchoredPosition = pos;
        }

        RectTransform CreateButton(ulong friendId)
        {
            var button = CreateImage("Interact_" + friendId, _root, OverlaySprites.Panel, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyPanel(button);
            button.raycastTarget = true;
            var rt = button.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(48f, 26f);
            FillLabel(rt, "互动", 13, OverlaySkin.Text);
            var id = friendId;
            OverlayHoverRelay.Bind(button.gameObject, () => HoverEnterFromButton(id), () => HoverLeaveFromButton(id));
            return rt;
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

        static void FillLabel(Transform parent, string text, int size, Color color)
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
