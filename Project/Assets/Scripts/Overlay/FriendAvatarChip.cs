using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class FriendAvatarChip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const string ControlSpriteResource = "Overlay/UI/control_rect";
        const float BubbleVisualScale = 1f;
        const float BubbleWidthRatio = 2f / 3f;
        const float BubbleHeight = 26f * BubbleVisualScale;
        const float BubbleOffsetY = 0f;
        const int BubbleFontSize = 16;
        const float BubbleSlideSeconds = 0.32f;

        float _size = 128f;

        PlayingFriendsService _service;
        FriendOverlayView _view;
        PlayingFriend _friend;
        RectTransform _rect;
        RectTransform _body;
        Image _avatar;
        Sprite _runtimeSprite;
        Sprite _presenceA;
        Sprite _presenceB;
        bool _presenceOwnedA;
        bool _presenceOwnedB;

        float BubbleRotateSeconds => _view != null && _view.Config != null
            ? Mathf.Max(0.1f, _view.Config.bubbleRotateSeconds)
            : 5f;
        bool _presenceActive;
        bool _presenceMode;
        Image _ring;
        GameObject _nameRoot;
        Text _nameText;
        Text _countText;
        Image _bubble;
        Text _bubbleText;
        Text _bubbleNextText;
        Image _badge;
        Text _badgeText;
        string _bubbleContent = "";
        readonly List<string> _bubbleUnread = new List<string>();
        int _bubbleUnreadIndex;
        float _nextBubbleRotateAt;
        float _bubbleSlideStartedAt = -1f;
        string _bubbleSlideTarget;
        int _unread;
        bool _selected;
        bool _chatExpanded;
        Vector2 _layoutPos;
        bool _dragging;
        long _tapCount;
        bool _hover;

        public ulong SteamId => _friend != null ? _friend.SteamId : 0;

        public bool IsLocal => _friend != null && _friend.IsLocal;

        public Vector2 LayoutPosition => _layoutPos;

        public Vector2 FollowPosition => _layoutPos;

        public void Bind(PlayingFriendsService service, FriendOverlayView view, PlayingFriend friend, int index)
        {
            _service = service;
            _view = view;
            _friend = friend;
            _ = index;
            ApplySize(_view != null && _view.Config != null ? _view.Config.chipSize : 128f);
            Apply();
        }

        void ApplySize(float size)
        {
            _size = size;
            if (_rect != null)
            {
                _rect.sizeDelta = new Vector2(size, size);
            }

            if (_body != null)
            {
                _body.sizeDelta = new Vector2(size, size);
            }

            if (_countText != null)
            {
                ((RectTransform)_countText.transform).sizeDelta = new Vector2(Mathf.Max(40f, size - 72f), 40f);
            }
        }

        public void SetLayoutPosition(Vector2 pixel)
        {
            _layoutPos = pixel;
            if (_rect != null && !_dragging)
            {
                _rect.anchoredPosition = pixel;
            }
        }

        public static FriendAvatarChip Create(Transform parent)
        {
            var root = new GameObject("FriendChip", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var chip = root.AddComponent<FriendAvatarChip>();
            chip.Build();
            return chip;
        }

        void Build()
        {
            _rect = (RectTransform)transform;
            _rect.anchorMin = new Vector2(0f, 0f);
            _rect.anchorMax = new Vector2(0f, 0f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(_size, _size);

            var bodyGo = new GameObject("AvatarBody", typeof(RectTransform));
            bodyGo.transform.SetParent(_rect, false);
            _body = (RectTransform)bodyGo.transform;
            _body.anchorMin = _body.anchorMax = new Vector2(0.5f, 0.5f);
            _body.pivot = new Vector2(0.5f, 0.5f);
            _body.sizeDelta = new Vector2(_size, _size);
            _body.anchoredPosition = Vector2.zero;

            var shadow = CreateImage("Shadow", _body, new Color(0f, 0f, 0f, 0.35f), OverlaySprites.RoundedSquare);
            var shadowRt = shadow.rectTransform;
            shadowRt.anchorMin = Vector2.zero;
            shadowRt.anchorMax = Vector2.one;
            shadowRt.offsetMin = new Vector2(4f, -6f);
            shadowRt.offsetMax = new Vector2(4f, -6f);
            shadow.raycastTarget = false;

            _ring = CreateImage("Ring", _body, new Color(0.35f, 0.9f, 0.45f, 1f), OverlaySprites.RoundedSquare);
            Stretch(_ring.rectTransform);
            _ring.raycastTarget = false;

            var maskGo = new GameObject("Mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            maskGo.transform.SetParent(_body, false);
            var maskRt = (RectTransform)maskGo.transform;
            Stretch(maskRt);
            maskRt.offsetMin = new Vector2(4f, 4f);
            maskRt.offsetMax = new Vector2(-4f, -4f);
            var maskImage = maskGo.GetComponent<Image>();
            maskImage.sprite = OverlaySprites.RoundedSquare;
            maskImage.raycastTarget = true;
            maskGo.GetComponent<Mask>().showMaskGraphic = false;

            _avatar = CreateImage("Avatar", maskRt, Color.white, OverlaySprites.RoundedSquare);
            Stretch(_avatar.rectTransform);
            _avatar.preserveAspect = true;
            _avatar.raycastTarget = false;

            _nameRoot = new GameObject("NameTag", typeof(RectTransform));
            _nameRoot.transform.SetParent(_rect, false);
            var nameRt = (RectTransform)_nameRoot.transform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0f, 4f);
            nameRt.sizeDelta = new Vector2(-8f, 24f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(_nameRoot.transform, false);
            _nameText = textGo.GetComponent<Text>();
            _nameText.font = OverlaySprites.UiFont;
            _nameText.fontSize = 15;
            _nameText.fontStyle = FontStyle.Bold;
            _nameText.resizeTextForBestFit = true;
            _nameText.resizeTextMinSize = 12;
            _nameText.resizeTextMaxSize = 15;
            _nameText.alignment = TextAnchor.MiddleCenter;
            _nameText.color = OverlaySkin.Text;
            _nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _nameText.verticalOverflow = VerticalWrapMode.Overflow;
            _nameText.raycastTarget = false;
            var nameOutline = textGo.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            nameOutline.effectDistance = new Vector2(1.5f, -1.5f);
            nameOutline.useGraphicAlpha = true;
            Stretch((RectTransform)textGo.transform);
            var textRt = (RectTransform)textGo.transform;
            textRt.offsetMin = new Vector2(3f, 0f);
            textRt.offsetMax = new Vector2(-3f, 0f);

            _nameRoot.SetActive(false);

            var countGo = new GameObject("TapCount", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            countGo.transform.SetParent(_rect, false);
            _countText = countGo.GetComponent<Text>();
            _countText.font = OverlaySprites.UiFont;
            _countText.fontSize = 26;
            _countText.resizeTextForBestFit = true;
            _countText.resizeTextMinSize = 6;
            _countText.resizeTextMaxSize = 26;
            _countText.alignment = TextAnchor.MiddleCenter;
            _countText.color = new Color(1f, 1f, 1f, 0.9f);
            _countText.raycastTarget = false;
            var countRt = (RectTransform)countGo.transform;
            countRt.anchorMin = new Vector2(0.5f, 0f);
            countRt.anchorMax = new Vector2(0.5f, 0f);
            countRt.pivot = new Vector2(0.5f, 1f);
            countRt.anchoredPosition = new Vector2(0f, -2f);
            countRt.sizeDelta = new Vector2(Mathf.Max(40f, _size - 72f), 40f);
            countGo.SetActive(false);

            _bubble = CreateImage("Bubble", _rect, OverlaySprites.Panel, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(_bubble);
            _bubble.raycastTarget = true;
            var bubbleRt = _bubble.rectTransform;
            bubbleRt.anchorMin = new Vector2(0.5f, 1f);
            bubbleRt.anchorMax = new Vector2(0.5f, 1f);
            bubbleRt.pivot = new Vector2(0.5f, 0f);
            bubbleRt.anchoredPosition = new Vector2(0f, BubbleOffsetY);
            bubbleRt.sizeDelta = new Vector2(_size * BubbleWidthRatio, BubbleHeight);
            var textViewport = new GameObject("TextViewport", typeof(RectTransform), typeof(RectMask2D));
            textViewport.transform.SetParent(_bubble.rectTransform, false);
            Stretch((RectTransform)textViewport.transform);
            _bubbleText = FillChipLabel(textViewport.transform, "", BubbleFontSize, OverlaySkin.Text);
            _bubbleNextText = FillChipLabel(textViewport.transform, "", BubbleFontSize, OverlaySkin.Text);
            _bubbleNextText.gameObject.SetActive(false);

            _badge = CreateImage("Badge", _bubble.rectTransform, new Color32(250, 81, 81, 255), OverlaySprites.Circle);
            _badge.raycastTarget = false;
            var badgeRt = _badge.rectTransform;
            badgeRt.anchorMin = new Vector2(1f, 0.5f);
            badgeRt.anchorMax = new Vector2(1f, 0.5f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(2f, 0f);
            badgeRt.sizeDelta = new Vector2(20f, 20f);
            _badgeText = FillChipLabel(_badge.rectTransform, "1", 11, Color.white);
            _badge.gameObject.SetActive(false);
        }

        static Text FillChipLabel(Transform parent, string text, int size, Color color)
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
            Stretch((RectTransform)go.transform);
            return label;
        }

        void Apply()
        {
            if (_friend == null)
            {
                return;
            }

            if (_runtimeSprite != null)
            {
                Destroy(_runtimeSprite);
                _runtimeSprite = null;
            }

            if (_friend.Avatar != null)
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
                _avatar.sprite = OverlaySprites.RoundedSquare;
                _avatar.color = _friend.IsLocal
                    ? new Color(0.35f, 0.62f, 0.95f)
                    : new Color(0.55f, 0.58f, 0.65f);
            }

            _ring.color = _friend.IsLocal
                ? new Color(0.95f, 0.78f, 0.28f, 1f)
                : new Color(0.35f, 0.9f, 0.45f, 1f);

            var label = _friend.IsLocal ? _friend.Name + "（你）" : _friend.Name;
            _nameText.text = label;
            RefreshCount();
            RefreshChatChrome();
            ApplySkin();
            RefreshPresenceVisual();
        }

        public void SetPresenceSprites(Sprite idle, Sprite active, bool takeOwnership = false)
        {
            ReleaseOwnedPresence();
            _presenceA = idle;
            _presenceB = active;
            _presenceOwnedA = takeOwnership && idle != null;
            _presenceOwnedB = takeOwnership && active != null;
            _presenceMode = _presenceA != null && _presenceB != null;
            RefreshPresenceVisual();
        }

        public void ClearPresenceSprites()
        {
            ReleaseOwnedPresence();
            _presenceA = null;
            _presenceB = null;
            _presenceMode = false;
            _presenceActive = false;
            ApplySteamAvatarOnly();
        }

        public void SetPresenceActive(bool active)
        {
            _presenceActive = active;
            if (_presenceMode)
            {
                RefreshPresenceVisual();
            }
        }

        void RefreshPresenceVisual()
        {
            if (!_presenceMode || _avatar == null)
            {
                return;
            }

            var sprite = _presenceActive ? _presenceB : _presenceA;
            if (sprite == null)
            {
                return;
            }

            _avatar.sprite = sprite;
            _avatar.color = Color.white;
        }

        void ApplySteamAvatarOnly()
        {
            if (_friend == null || _avatar == null)
            {
                return;
            }

            if (_runtimeSprite != null)
            {
                Destroy(_runtimeSprite);
                _runtimeSprite = null;
            }

            if (_friend.Avatar != null)
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
                _avatar.sprite = OverlaySprites.RoundedSquare;
                _avatar.color = _friend.IsLocal
                    ? new Color(0.35f, 0.62f, 0.95f)
                    : new Color(0.55f, 0.58f, 0.65f);
            }
        }

        void ReleaseOwnedPresence()
        {
            if (_presenceOwnedA && _presenceA != null)
            {
                if (_presenceA.texture != null)
                {
                    Destroy(_presenceA.texture);
                }

                Destroy(_presenceA);
            }

            if (_presenceOwnedB && _presenceB != null)
            {
                if (_presenceB.texture != null)
                {
                    Destroy(_presenceB.texture);
                }

                Destroy(_presenceB);
            }

            _presenceOwnedA = false;
            _presenceOwnedB = false;
            _presenceA = null;
            _presenceB = null;
        }

        public void ApplySkin()
        {
            var theme = _view != null && _view.Settings != null ? _view.Settings.SettingsTheme : 1;
            var control = Resources.Load<Sprite>(ControlSpriteResource);
            if (_bubble != null)
            {
                var bubbleColor = OverlaySkin.ThemeControl(theme);
                bubbleColor.a = OverlaySkin.ReduceTransparency ? 1f : 0.58f;
                ApplyFlatStyle(_bubble, control, bubbleColor);
            }
            if (_badge != null)
            {
                _badge.color = new Color32(250, 81, 81, 255);
            }
            if (_nameText != null)
            {
                _nameText.color = Color.white;
            }

            if (_bubbleText != null)
            {
                _bubbleText.color = OverlaySkin.SettingsThemeText(theme);
            }
            if (_bubbleNextText != null)
            {
                _bubbleNextText.color = OverlaySkin.SettingsThemeText(theme);
            }
            if (_badgeText != null)
            {
                _badgeText.color = Color.white;
            }
        }

        static void ApplyFlatStyle(Image image, Sprite sprite, Color color)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite != null ? sprite : OverlaySprites.RoundedRect;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.color = color;
        }

        public void SetChatPreview(IReadOnlyList<OverlayChatMessage> unreadMessages, int unread)
        {
            _bubbleUnread.Clear();
            if (unreadMessages != null)
            {
                for (var i = 0; i < unreadMessages.Count; i++)
                {
                    var text = unreadMessages[i] != null ? unreadMessages[i].text : null;
                    if (!string.IsNullOrEmpty(text))
                    {
                        _bubbleUnread.Add(Ellipsize(text, 8));
                    }
                }
            }

            _bubbleUnreadIndex = 0;
            _nextBubbleRotateAt = Time.unscaledTime + BubbleRotateSeconds;
            var next = _bubbleUnread.Count > 0 ? _bubbleUnread[0] : "";
            _bubbleSlideStartedAt = -1f;
            _bubbleSlideTarget = null;
            _bubbleContent = next;
            if (_bubbleText != null)
            {
                _bubbleText.text = next;
                _bubbleText.rectTransform.anchoredPosition = Vector2.zero;
            }
            if (_bubbleNextText != null)
            {
                _bubbleNextText.gameObject.SetActive(false);
                _bubbleNextText.rectTransform.anchoredPosition = new Vector2(0f, -BubbleHeight);
            }

            _unread = unread;
            RefreshChatChrome();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            RefreshChatChrome();
        }

        public void SetChatExpanded(bool expanded)
        {
            if (_chatExpanded == expanded)
            {
                return;
            }

            _chatExpanded = expanded;
            RefreshChatChrome();
        }

        void RefreshChatChrome()
        {
            var showChat = _friend != null && !_friend.IsLocal;
            if (_bubble != null)
            {
                var showBubble = showChat && !_chatExpanded && _unread > 0 && !string.IsNullOrEmpty(_bubbleContent);
                _bubble.gameObject.SetActive(showBubble);
                if (showBubble && _bubbleText != null)
                {
                    if (_bubbleSlideStartedAt < 0f)
                    {
                        _bubbleText.text = _bubbleContent;
                    }
                    _bubble.rectTransform.sizeDelta = new Vector2(_size * BubbleWidthRatio, BubbleHeight);
                }
            }

            if (_badge != null)
            {
                var showBadge = showChat && _unread > 0;
                _badge.gameObject.SetActive(showBadge);
                if (showBadge && _badgeText != null)
                {
                    _badgeText.text = _view != null && _view.Config != null
                        ? _view.Config.FormatUnread(_unread)
                        : (_unread > 99 ? "99+" : _unread.ToString());
                }
            }

            if (_ring != null && _friend != null)
            {
                _ring.color = _selected && !_friend.IsLocal
                    ? OverlaySkin.ThemeAccent(_view != null && _view.Settings != null
                        ? _view.Settings.SettingsTheme
                        : 1)
                    : _friend.IsLocal
                        ? new Color(0.95f, 0.78f, 0.28f, 1f)
                        : new Color(0.35f, 0.9f, 0.45f, 1f);
            }
        }

        string FormatUnread(int count)
        {
            return _view != null && _view.Config != null
                ? _view.Config.FormatUnread(count)
                : (count > 99 ? "99+" : count.ToString());
        }

        static string Ellipsize(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }

        public void SetTapCount(long count)
        {
            _tapCount = count;
            RefreshCount();
        }

        void RefreshCount()
        {
            if (_countText == null)
            {
                return;
            }

            var show = IsLocal;
            _countText.gameObject.SetActive(show);
            if (show)
            {
                _countText.text = _tapCount.ToString();
            }
        }

        void Update()
        {
            if (_rect == null)
            {
                return;
            }

            var userScale = _view != null && _view.Settings != null ? _view.Settings.Scale : 1f;
            _rect.anchoredPosition = _layoutPos;
            _rect.localScale = new Vector3(userScale, userScale, 1f);

            if (_dragging || _body == null)
            {
                if (_body != null)
                {
                    _body.anchoredPosition = Vector2.zero;
                    _body.localScale = Vector3.one;
                }

                return;
            }

            var hover = _hover ? 1.06f : 1f;
            var baseFlip = IsLocal && _view != null && _view.Settings != null && _view.Settings.FlipHorizontal ? -1f : 1f;
            _body.anchoredPosition = Vector2.zero;
            _body.localScale = new Vector3(hover * baseFlip, hover, 1f);
            UpdateBubbleSlide();

            if (!_chatExpanded && _bubbleSlideStartedAt < 0f &&
                _bubbleUnread.Count > 1 && Time.unscaledTime >= _nextBubbleRotateAt)
            {
                _bubbleUnreadIndex = (_bubbleUnreadIndex + 1) % _bubbleUnread.Count;
                _nextBubbleRotateAt = Time.unscaledTime + BubbleRotateSeconds;
                BeginBubbleSlide(_bubbleUnread[_bubbleUnreadIndex]);
            }
        }

        void BeginBubbleSlide(string next)
        {
            if (_bubbleText == null || _bubbleNextText == null || string.IsNullOrEmpty(next))
            {
                return;
            }

            _bubbleSlideTarget = next;
            _bubbleSlideStartedAt = Time.unscaledTime;
            _bubbleText.rectTransform.anchoredPosition = Vector2.zero;
            _bubbleNextText.text = next;
            _bubbleNextText.rectTransform.anchoredPosition = new Vector2(0f, -BubbleHeight);
            _bubbleNextText.gameObject.SetActive(true);
        }

        void UpdateBubbleSlide()
        {
            if (_bubbleSlideStartedAt < 0f || _bubbleText == null || _bubbleNextText == null)
            {
                return;
            }

            var t = Mathf.Clamp01((Time.unscaledTime - _bubbleSlideStartedAt) / BubbleSlideSeconds);
            var eased = t * t * (3f - 2f * t);
            _bubbleText.rectTransform.anchoredPosition = new Vector2(0f, BubbleHeight * eased);
            _bubbleNextText.rectTransform.anchoredPosition = new Vector2(0f, -BubbleHeight * (1f - eased));
            if (t < 1f)
            {
                return;
            }

            _bubbleText.gameObject.SetActive(false);
            var oldText = _bubbleText;
            _bubbleText = _bubbleNextText;
            _bubbleNextText = oldText;
            _bubbleText.rectTransform.anchoredPosition = Vector2.zero;
            _bubbleNextText.rectTransform.anchoredPosition = new Vector2(0f, -BubbleHeight);
            _bubbleNextText.gameObject.SetActive(false);
            _bubbleContent = _bubbleSlideTarget ?? "";
            _bubbleSlideTarget = null;
            _bubbleSlideStartedAt = -1f;
        }

        void OnDestroy()
        {
            if (_runtimeSprite != null)
            {
                Destroy(_runtimeSprite);
                _runtimeSprite = null;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            _nameRoot.SetActive(true);
            if (!IsLocal)
            {
                _view?.OnChipHoverEnter(this);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            _nameRoot.SetActive(false);
            if (!IsLocal)
            {
                _view?.OnChipHoverExit(this);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_dragging || eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _view?.OnChipClicked(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_view != null && _view.Settings != null && _view.Settings.DisableDrag)
            {
                return;
            }

            _view?.HideInteractMenu();
            _dragging = true;
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            var scale = _view != null && _view.Settings != null ? _view.Settings.Scale : 1f;
            _layoutPos = FriendOverlayView.Clamp(_layoutPos + eventData.delta, _size * 0.5f * scale);
            _rect.anchoredPosition = _layoutPos;
            _view?.SetBagHover(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            if (_view != null && _view.TryPutInBag(this))
            {
                return;
            }

            _view?.NotifyMoved(this);
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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
