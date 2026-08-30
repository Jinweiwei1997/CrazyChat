using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class OverlayBagUi : MonoBehaviour
    {
        public const float Width = 200f;
        public const float ItemHeight = 52f;
        const float ToggleWidth = 56f;
        const float ToggleHeight = 26f;

        FriendOverlayView _view;
        RectTransform _root;
        RectTransform _listRt;
        Text _toggleText;
        Image _toggleBadge;
        Text _toggleBadgeText;
        bool _collapsed;
        ulong _selectedId;
        readonly Dictionary<ulong, OverlayBagItem> _items = new Dictionary<ulong, OverlayBagItem>();

        public static OverlayBagUi Create(Transform canvas, FriendOverlayView view)
        {
            var root = new GameObject("BagUi", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            var ui = root.AddComponent<OverlayBagUi>();
            ui._view = view;
            ui.Build();
            return ui;
        }

        void Build()
        {
            _root = (RectTransform)transform;
            _root.anchorMin = _root.anchorMax = new Vector2(1f, 0f);
            _root.pivot = new Vector2(1f, 0f);
            _root.anchoredPosition = new Vector2(-4f, 6f);
            _root.sizeDelta = new Vector2(Width, ToggleHeight);

            _listRt = new GameObject("List", typeof(RectTransform)).GetComponent<RectTransform>();
            _listRt.SetParent(_root, false);
            _listRt.anchorMin = new Vector2(0f, 0f);
            _listRt.anchorMax = new Vector2(1f, 0f);
            _listRt.pivot = new Vector2(1f, 0f);
            _listRt.anchoredPosition = new Vector2(0f, ToggleHeight);
            _listRt.sizeDelta = new Vector2(0f, 0f);

            var toggle = CreateImage("Toggle", _root, new Color(0.12f, 0.13f, 0.16f, 0.72f), OverlaySprites.RoundedRect);
            toggle.raycastTarget = true;
            var toggleRt = toggle.rectTransform;
            toggleRt.anchorMin = toggleRt.anchorMax = new Vector2(1f, 0f);
            toggleRt.pivot = new Vector2(1f, 0f);
            toggleRt.anchoredPosition = Vector2.zero;
            toggleRt.sizeDelta = new Vector2(ToggleWidth, ToggleHeight);
            _toggleText = FillLabel(toggleRt, "收起", 12, Color.white);
            toggle.gameObject.AddComponent<Button>().onClick.AddListener(Toggle);

            _toggleBadge = CreateImage("Unread", toggleRt, new Color(0.92f, 0.28f, 0.28f, 1f), OverlaySprites.Circle);
            _toggleBadge.raycastTarget = false;
            var badgeRt = _toggleBadge.rectTransform;
            badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(-2f, -2f);
            badgeRt.sizeDelta = new Vector2(16f, 16f);
            _toggleBadgeText = FillLabel(badgeRt, "1", 10, Color.white);
            _toggleBadge.gameObject.SetActive(false);
        }

        public void Refresh(IReadOnlyList<PlayingFriend> bagged, OverlayChatStore chat, bool showDropTarget)
        {
            var count = bagged != null ? bagged.Count : 0;
            var show = count > 0 || showDropTarget;
            gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            var seen = new HashSet<ulong>();
            for (var i = 0; i < count; i++)
            {
                var friend = bagged[i];
                seen.Add(friend.SteamId);
                if (!_items.TryGetValue(friend.SteamId, out var item))
                {
                    item = OverlayBagItem.Create(_listRt, _view);
                    _items[friend.SteamId] = item;
                }

                var latest = chat != null ? chat.GetLatest(friend.SteamId) : null;
                var unread = chat != null ? chat.GetUnread(friend.SteamId) : 0;
                item.Bind(friend, latest != null ? latest.text : "...", unread, i);
                item.SetSelected(friend.SteamId == _selectedId);
            }

            var stale = new List<ulong>();
            foreach (var pair in _items)
            {
                if (!seen.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                if (_items.TryGetValue(stale[i], out var item) && item != null)
                {
                    Destroy(item.gameObject);
                }

                _items.Remove(stale[i]);
            }

            var unreadTotal = 0;
            if (chat != null)
            {
                for (var i = 0; i < count; i++)
                {
                    unreadTotal += chat.GetUnread(bagged[i].SteamId);
                }
            }

            var showBadge = unreadTotal > 0;
            if (_toggleBadge != null)
            {
                _toggleBadge.gameObject.SetActive(showBadge);
                if (showBadge && _toggleBadgeText != null && _view != null && _view.Config != null)
                {
                    _toggleBadgeText.text = _view.Config.FormatUnread(unreadTotal);
                }
            }

            ApplyFold(count);
        }

        public void ExpandFor(ulong friendId)
        {
            if (friendId == 0 || !_items.ContainsKey(friendId))
            {
                return;
            }

            _collapsed = false;
            ApplyFold(_items.Count);
        }

        public void SetSelected(ulong friendId)
        {
            _selectedId = friendId;
            foreach (var pair in _items)
            {
                pair.Value.SetSelected(pair.Key == friendId);
            }
        }

        void Toggle()
        {
            _collapsed = !_collapsed;
            ApplyFold(_items.Count);
        }

        void ApplyFold(int count)
        {
            var expanded = !_collapsed && count > 0;
            if (_listRt != null)
            {
                _listRt.gameObject.SetActive(expanded);
                _listRt.sizeDelta = new Vector2(0f, count * ItemHeight);
            }

            if (_toggleText != null)
            {
                _toggleText.text = expanded ? "收起" : (count > 0 ? "麻袋 " + count : "麻袋");
            }

            var height = ToggleHeight + (expanded ? count * ItemHeight : 0f);
            _root.sizeDelta = new Vector2(expanded ? Width : ToggleWidth, height);
        }

        public bool ContainsScreenPoint(Vector2 screen)
        {
            if (!isActiveAndEnabled || _root == null)
            {
                return false;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(_root, screen, null);
        }

        public bool TryGetItemPosition(ulong friendId, out Vector2 pos)
        {
            if (!_collapsed && _items.TryGetValue(friendId, out var item) && item != null)
            {
                pos = item.FollowPosition;
                return true;
            }

            pos = default;
            return false;
        }

        public void SetDropHighlight(bool on)
        {
        }

        static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
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
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return label;
        }
    }

    public sealed class OverlayBagItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        FriendOverlayView _view;
        PlayingFriend _friend;
        RectTransform _rect;
        Image _avatar;
        Text _name;
        Text _preview;
        Image _badge;
        Text _badgeText;
        Sprite _runtimeSprite;
        GameObject _ghost;
        bool _dragging;
        float _ignoreClickUntil;

        public ulong SteamId => _friend != null ? _friend.SteamId : 0;

        public Vector2 FollowPosition
        {
            get { return _rect != null ? (Vector2)_rect.position : Vector2.zero; }
        }

        public static OverlayBagItem Create(Transform parent, FriendOverlayView view)
        {
            var root = new GameObject("BagItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            var item = root.AddComponent<OverlayBagItem>();
            item._view = view;
            item.Build();
            return item;
        }

        public void Bind(PlayingFriend friend, string preview, int unread, int index)
        {
            _friend = friend;
            _rect.anchoredPosition = new Vector2(0f, index * OverlayBagUi.ItemHeight);
            if (_name != null)
            {
                _name.text = friend != null ? friend.Name : string.Empty;
            }

            if (_preview != null)
            {
                _preview.text = string.IsNullOrEmpty(preview) ? "..." : preview;
            }

            var showBadge = unread > 0;
            if (_badge != null)
            {
                _badge.gameObject.SetActive(showBadge);
                if (showBadge && _badgeText != null)
                {
                    _badgeText.text = _view != null && _view.Config != null
                        ? _view.Config.FormatUnread(unread)
                        : unread.ToString();
                }
            }

            ApplyAvatar();
        }

        public void SetSelected(bool selected)
        {
            if (_name != null)
            {
                _name.color = selected ? new Color(1f, 0.92f, 0.55f) : new Color(1f, 1f, 1f, 0.92f);
            }
        }

        void Build()
        {
            _rect = (RectTransform)transform;
            _rect.anchorMin = new Vector2(0f, 0f);
            _rect.anchorMax = new Vector2(1f, 0f);
            _rect.pivot = new Vector2(1f, 0f);
            _rect.anchoredPosition = Vector2.zero;
            _rect.sizeDelta = new Vector2(0f, OverlayBagUi.ItemHeight);
            var hit = GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            _avatar = CreateImage("Avatar", _rect, Color.white, OverlaySprites.Circle);
            var avatarRt = _avatar.rectTransform;
            avatarRt.anchorMin = avatarRt.anchorMax = new Vector2(1f, 0.5f);
            avatarRt.pivot = new Vector2(1f, 0.5f);
            avatarRt.anchoredPosition = Vector2.zero;
            avatarRt.sizeDelta = new Vector2(32f, 32f);
            _avatar.raycastTarget = false;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            nameGo.transform.SetParent(_rect, false);
            _name = nameGo.GetComponent<Text>();
            _name.font = OverlaySprites.UiFont;
            _name.fontSize = 12;
            _name.alignment = TextAnchor.MiddleRight;
            _name.color = new Color(1f, 1f, 1f, 0.92f);
            _name.horizontalOverflow = HorizontalWrapMode.Wrap;
            _name.verticalOverflow = VerticalWrapMode.Truncate;
            _name.raycastTarget = false;
            var nameRt = (RectTransform)nameGo.transform;
            nameRt.anchorMin = new Vector2(0f, 0.5f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(8f, 0f);
            nameRt.offsetMax = new Vector2(-40f, -2f);

            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            previewGo.transform.SetParent(_rect, false);
            _preview = previewGo.GetComponent<Text>();
            _preview.font = OverlaySprites.UiFont;
            _preview.fontSize = 12;
            _preview.alignment = TextAnchor.MiddleRight;
            _preview.color = new Color(1f, 1f, 1f, 0.55f);
            _preview.horizontalOverflow = HorizontalWrapMode.Wrap;
            _preview.verticalOverflow = VerticalWrapMode.Truncate;
            _preview.raycastTarget = false;
            var previewRt = (RectTransform)previewGo.transform;
            previewRt.anchorMin = new Vector2(0f, 0f);
            previewRt.anchorMax = new Vector2(1f, 0.5f);
            previewRt.offsetMin = new Vector2(8f, 4f);
            previewRt.offsetMax = new Vector2(-40f, 0f);

            _badge = CreateImage("Badge", _rect, new Color(0.92f, 0.28f, 0.28f, 1f), OverlaySprites.Circle);
            _badge.raycastTarget = false;
            var itemBadgeRt = _badge.rectTransform;
            itemBadgeRt.anchorMin = itemBadgeRt.anchorMax = new Vector2(1f, 1f);
            itemBadgeRt.pivot = new Vector2(1f, 1f);
            itemBadgeRt.anchoredPosition = new Vector2(2f, 2f);
            itemBadgeRt.sizeDelta = new Vector2(16f, 16f);
            _badgeText = FillItemLabel(itemBadgeRt, "1", 10, Color.white);
            _badge.gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_dragging || _friend == null || eventData.dragging || Time.unscaledTime < _ignoreClickUntil)
            {
                return;
            }

            _view?.OpenChat(_friend.SteamId);
        }

        void ApplyAvatar()
        {
            if (_runtimeSprite != null)
            {
                Destroy(_runtimeSprite);
                _runtimeSprite = null;
            }

            if (_friend != null && _friend.Avatar != null)
            {
                _runtimeSprite = Sprite.Create(
                    _friend.Avatar,
                    new Rect(0, 0, _friend.Avatar.width, _friend.Avatar.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                _avatar.sprite = _runtimeSprite;
                _avatar.color = Color.white;
            }
            else
            {
                _avatar.sprite = OverlaySprites.Circle;
                _avatar.color = new Color(0.55f, 0.58f, 0.65f);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_view != null && _view.Settings != null && _view.Settings.DisableDrag)
            {
                return;
            }

            _dragging = true;
            _ghost = new GameObject("BagGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _ghost.transform.SetParent(_view != null ? _view.OverlayLayer : transform.root, false);
            var ghostImage = _ghost.GetComponent<Image>();
            ghostImage.sprite = _avatar != null ? _avatar.sprite : OverlaySprites.Circle;
            ghostImage.color = _avatar != null ? _avatar.color : Color.white;
            ghostImage.raycastTarget = false;
            var ghostRt = (RectTransform)_ghost.transform;
            ghostRt.anchorMin = ghostRt.anchorMax = new Vector2(0f, 0f);
            ghostRt.pivot = new Vector2(0.5f, 0.5f);
            ghostRt.sizeDelta = new Vector2(56f, 56f);
            ghostRt.anchoredPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _ghost == null)
            {
                return;
            }

            ((RectTransform)_ghost.transform).anchoredPosition = eventData.position;
            _view?.SetBagHover(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            _ignoreClickUntil = Time.unscaledTime + 0.2f;
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }

            _view?.SetBagHover(Vector2.zero);
            if (_friend != null && _view != null && !_view.IsOverBag(eventData.position))
            {
                _view.TakeOut(_friend.SteamId, eventData.position);
            }
        }

        void OnDestroy()
        {
            if (_runtimeSprite != null)
            {
                Destroy(_runtimeSprite);
                _runtimeSprite = null;
            }

            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
        }

        static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        static Text FillItemLabel(Transform parent, string text, int size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = OverlaySprites.UiFont;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return label;
        }
    }
}
