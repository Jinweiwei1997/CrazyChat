using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class FriendAvatarChip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const float BubbleVisualScale = 1f;
        const float AvatarInset = 4f;
        const float BubbleHeight = 26f * BubbleVisualScale;
        const float BubbleOffsetY = 0f;
        const int BubbleFontSize = 16;
        const float BubbleSlideSeconds = 0.32f;
        const string IdleBubbleText = "…";

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
        GameObject _nameRoot;
        Text _nameText;
        Text _countText;
        Image _bubble;
        Text _bubbleText;
        Text _bubbleNextText;
        Image _selectionMarker;
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
        bool _todoExpanded;
        string _todoPreview = "";
        Vector2 _layoutPos;
        bool _dragging;
        bool _settlingDrag;
        Vector2 _dragPointer;
        Vector2 _pointerOffset;
        Vector2 _dragTarget;
        Vector2 _releaseOffset;
        float _releaseStartedAt = -1f;
        Vector2 _snapMotionOffset;
        float _snapStartedAt = -1f;
        FriendAvatarChip _snapAnchor;
        Vector2 _snapDirection;
        long _tapCount;
        bool _hover;

        public ulong SteamId => _friend != null ? _friend.SteamId : 0;

        public bool IsLocal => _friend != null && _friend.IsLocal;

        public Vector2 LayoutPosition => _layoutPos;
        internal bool IsPresenceActive => _presenceMode && _presenceActive;

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
            if (!_dragging)
            {
                _settlingDrag = false;
                _snapAnchor = null;
            }
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

            var maskGo = new GameObject("Mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            maskGo.transform.SetParent(_body, false);
            var maskRt = (RectTransform)maskGo.transform;
            Stretch(maskRt);
            maskRt.offsetMin = Vector2.one * AvatarInset;
            maskRt.offsetMax = -Vector2.one * AvatarInset;
            var maskImage = maskGo.GetComponent<Image>();
            maskImage.sprite = OverlaySprites.RoundedSquare;
            maskImage.color = Color.clear;
            maskImage.raycastTarget = true;

            _avatar = CreateImage("Avatar", maskRt, Color.white, OverlaySprites.RoundedSquare);
            Stretch(_avatar.rectTransform);
            _avatar.preserveAspect = true;
            _avatar.material = OverlaySprites.RoundedAvatarMaterial;
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
            bubbleRt.sizeDelta = new Vector2(_size - 2f * AvatarInset, BubbleHeight);
            var textViewport = new GameObject("TextViewport", typeof(RectTransform), typeof(RectMask2D));
            textViewport.transform.SetParent(_bubble.rectTransform, false);
            Stretch((RectTransform)textViewport.transform);
            _bubbleText = FillChipLabel(textViewport.transform, "", BubbleFontSize, OverlaySkin.Text);
            _bubbleNextText = FillChipLabel(textViewport.transform, "", BubbleFontSize, OverlaySkin.Text);
            _bubbleNextText.gameObject.SetActive(false);

            _selectionMarker = CreateImage("SelectionMarker", bubbleRt,
                Color.white, OverlaySprites.Sparkles);
            _selectionMarker.raycastTarget = false;
            _selectionMarker.type = Image.Type.Simple;
            var markerRt = _selectionMarker.rectTransform;
            markerRt.anchorMin = markerRt.anchorMax = new Vector2(0f, 1f);
            markerRt.pivot = new Vector2(0.5f, 0.5f);
            markerRt.sizeDelta = new Vector2(21f, 25f);
            markerRt.anchoredPosition = new Vector2(2f, -2f);
            _selectionMarker.gameObject.SetActive(false);

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

        static void AlignBubbleText(Text label, bool left)
        {
            if (label == null) return;
            label.alignment = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            var rt = label.rectTransform;
            rt.offsetMin = new Vector2(left ? 8f : 0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(left ? -8f : 0f, rt.offsetMax.y);
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

            var label = _friend.Name;
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
            if (_selectionMarker != null && _view != null)
            {
                var config = _view.Config;
                _selectionMarker.rectTransform.sizeDelta = new Vector2(21f, 25f) * config.SelectionStarScale;
                _selectionMarker.rectTransform.anchoredPosition = new Vector2(
                    config.SelectionStarOffsetX, config.SelectionStarOffsetY);
            }
            var theme = _view != null && _view.Settings != null ? _view.Settings.SettingsTheme : 1;
            if (_bubble != null)
            {
                var bubbleColor = OverlaySkin.ThemeAccent(theme);
                bubbleColor.a = OverlaySkin.ReduceTransparency ? 1f : 0.58f;
                ApplyFlatStyle(_bubble, OverlaySprites.RoundedRect, bubbleColor);
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
                _bubbleText.color = OverlaySkin.ThemeAccentText(theme);
            }
            if (_bubbleNextText != null)
            {
                _bubbleNextText.color = OverlaySkin.ThemeAccentText(theme);
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
            image.pixelsPerUnitMultiplier = 1f;
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
                        _bubbleUnread.Add(Ellipsize(text, 5));
                    }
                }
            }

            _bubbleUnreadIndex = 0;
            _nextBubbleRotateAt = Time.unscaledTime + BubbleRotateSeconds;
            var next = _bubbleUnread.Count > 0 ? _bubbleUnread[0] : IdleBubbleText;
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

        public void SetSelected(bool selected, bool keyboardTarget = false)
        {
            _selected = selected;
            if (_selectionMarker != null)
            {
                _selectionMarker.gameObject.SetActive(selected && keyboardTarget && !IsLocal);
            }
            RefreshNameVisibility();
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
                if (showChat && _unread <= 0)
                {
                    _bubbleContent = IdleBubbleText;
                    _bubbleSlideStartedAt = -1f;
                    _bubbleSlideTarget = null;
                    if (_bubbleNextText != null)
                    {
                        _bubbleNextText.gameObject.SetActive(false);
                    }
                }

                var showBubble = IsLocal ? !_todoExpanded && !string.IsNullOrEmpty(_todoPreview)
                    : showChat && !_chatExpanded;
                _bubble.gameObject.SetActive(showBubble);
                AlignBubbleText(_bubbleText, IsLocal);
                AlignBubbleText(_bubbleNextText, IsLocal);
                if (showBubble && _bubbleText != null)
                {
                    if (_bubbleSlideStartedAt < 0f)
                    {
                        _bubbleText.text = IsLocal ? _todoPreview :
                            string.IsNullOrEmpty(_bubbleContent) ? IdleBubbleText : _bubbleContent;
                    }

                    _bubble.rectTransform.sizeDelta = new Vector2(_size - 2f * AvatarInset, BubbleHeight);
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
            UpdateDragMotion(userScale);
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
            RefreshNameVisibility();
            _view?.OnChipHoverEnter(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            RefreshNameVisibility();
            _view?.OnChipHoverExit(this);
        }

        void RefreshNameVisibility()
        {
            if (_nameRoot != null)
            {
                _nameRoot.SetActive(_hover || (_selected && !IsLocal));
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _view?.ClaimInteractionFocus();
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

            _view?.ClaimInteractionFocus();
            _view?.HideInteractMenu();
            _dragging = true;
            _releaseStartedAt = -1f;
            _snapStartedAt = -1f;
            _settlingDrag = false;
            _snapAnchor = null;
            _dragPointer = eventData.pressPosition;
            _pointerOffset = _layoutPos - eventData.pressPosition;
            var scale = _view != null && _view.Settings != null ? _view.Settings.Scale : 1f;
            _dragTarget = ResolveDragTarget(scale);
            _dragPointer = eventData.position;
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            _dragPointer = eventData.position;
            _view?.SetBagHover(eventData.position);
            var scale = _view != null && _view.Settings != null ? _view.Settings.Scale : 1f;
            UpdateDragMotion(scale);
            _rect.anchoredPosition = _layoutPos;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            _dragPointer = eventData.position;
            var scale = _view != null && _view.Settings != null ? _view.Settings.Scale : 1f;
            UpdateDragMotion(scale);
            _dragging = false;
            if (_view != null && _view.TryPutInBag(this))
            {
                return;
            }

            _snapAnchor = null;
            _settlingDrag = true;
        }

        public void SetTodoPreview(string text)
        {
            _todoPreview = Ellipsize(text ?? "", 7);
            if (IsLocal) RefreshChatChrome();
        }

        public void SetTodoExpanded(bool expanded)
        {
            _todoExpanded = expanded;
            if (IsLocal) RefreshChatChrome();
        }

        void UpdateDragMotion(float scale)
        {
            if (!_dragging && !_settlingDrag) return;
            if (_dragging)
            {
                var previousAnchor = _snapAnchor;
                var previousDirection = _snapDirection;
                var wasSnapped = _snapAnchor != null;
                _dragTarget = ResolveDragTarget(scale);
                if (_snapAnchor != null)
                {
                    if (_snapStartedAt < 0f || previousAnchor != _snapAnchor || previousDirection != _snapDirection)
                    {
                        _snapMotionOffset = _layoutPos - _dragTarget;
                        _snapStartedAt = Time.unscaledTime;
                    }
                    var snapT = Mathf.Clamp01((Time.unscaledTime - _snapStartedAt) / OverlayConfig.AvatarSnapBounceSeconds);
                    _layoutPos = FriendOverlayView.Clamp(_dragTarget + _snapMotionOffset * (1f - DragBounce(snapT)), _size * 0.5f * scale);
                    _releaseStartedAt = -1f;
                    return;
                }
                if (wasSnapped)
                {
                    _releaseOffset = _layoutPos - _dragTarget;
                    _releaseStartedAt = Time.unscaledTime;
                }
                _snapStartedAt = -1f;
                var t = _releaseStartedAt < 0f ? 1f
                    : Mathf.Clamp01((Time.unscaledTime - _releaseStartedAt) / OverlayConfig.AvatarReleaseBounceSeconds);
                var eased = DragBounce(t);
                _layoutPos = FriendOverlayView.Clamp(_dragTarget + _releaseOffset * (1f - eased), _size * 0.5f * scale);
                if (t >= 1f) _releaseStartedAt = -1f;
                return;
            }
            _dragTarget = FriendOverlayView.Clamp(_dragTarget, _size * 0.5f * scale);
            if (_snapStartedAt >= 0f || _releaseStartedAt >= 0f)
            {
                var snapping = _snapStartedAt >= 0f;
                var start = snapping ? _snapStartedAt : _releaseStartedAt;
                var duration = snapping ? OverlayConfig.AvatarSnapBounceSeconds : OverlayConfig.AvatarReleaseBounceSeconds;
                var t = Mathf.Clamp01((Time.unscaledTime - start) / duration);
                var offset = snapping ? _snapMotionOffset : _releaseOffset;
                _layoutPos = _dragTarget + offset * (1f - DragBounce(t));
                if (t >= 1f) _snapStartedAt = _releaseStartedAt = -1f;
            }
            else
            {
                _layoutPos = _dragTarget;
            }
            _layoutPos = FriendOverlayView.Clamp(_layoutPos, _size * 0.5f * scale);
            if (!_dragging && _snapStartedAt < 0f && _releaseStartedAt < 0f && (_layoutPos - _dragTarget).sqrMagnitude < 0.01f)
            {
                _layoutPos = _dragTarget;
                _settlingDrag = false;
                _view?.NotifyMoved(this);
            }
        }

        static float DragBounce(float t)
        {
            // Fast capture, a small overshoot, then a firm finish (about 6% rebound).
            var u = Mathf.Clamp01(t) - 1f;
            return 1f + 2.3f * u * u * u + 1.3f * u * u;
        }

        Vector2 ResolveDragTarget(float scale)
        {
            var desired = FriendOverlayView.Clamp(_dragPointer + _pointerOffset, _size * 0.5f * scale);
            var distance = _view != null ? _view.Config.AvatarSnapDistance : 0f;
            var startDistance = _view != null ? _view.Config.AvatarSnapStartDistance : 0f;
            if (distance <= 0f || startDistance <= 0f || _view.IsOverBag(_dragPointer))
            {
                _snapAnchor = null;
                return desired;
            }
            if (_snapAnchor != null && _snapAnchor.gameObject.activeInHierarchy)
            {
                var held = SnapPosition(_snapAnchor, _snapDirection, scale);
                var releaseDistance = Mathf.Max(distance, startDistance + 1f);
                if ((desired - held).sqrMagnitude <= releaseDistance * releaseDistance && CanSnapAt(held, scale))
                    return held;
            }
            _snapAnchor = null;
            // Acquire closer than the release threshold to avoid flickering at the boundary.
            var bestDistance = startDistance * startDistance;
            var best = desired;
            foreach (var other in _view.DesktopChips)
            {
                if (other == null || other == this || !other.gameObject.activeInHierarchy) continue;
                for (var side = 0; side < 4; side++)
                {
                    var direction = side == 0 ? Vector2.left : side == 1 ? Vector2.right
                        : side == 2 ? Vector2.up : Vector2.down;
                    var candidate = SnapPosition(other, direction, scale);
                    var squared = (candidate - desired).sqrMagnitude;
                    if (squared > bestDistance || !CanSnapAt(candidate, scale)) continue;
                    bestDistance = squared;
                    best = candidate;
                    _snapAnchor = other;
                    _snapDirection = direction;
                }
            }
            return best;
        }

        Vector2 SnapPosition(FriendAvatarChip other, Vector2 direction, float scale)
        {
            return other.LayoutPosition + direction * (((_size + other._size) * 0.5f - 2f * AvatarInset) * scale + _view.Config.AvatarSnapGap);
        }

        bool CanSnapAt(Vector2 position, float scale)
        {
            if ((FriendOverlayView.Clamp(position, _size * 0.5f * scale) - position).sqrMagnitude > 0.01f)
                return false;
            foreach (var other in _view.DesktopChips)
            {
                if (other == null || other == this || !other.gameObject.activeInHierarchy) continue;
                var separation = ((_size + other._size) * 0.5f - 2f * AvatarInset) * scale - 0.5f;
                var offset = position - other.LayoutPosition;
                if (Mathf.Abs(offset.x) < separation && Mathf.Abs(offset.y) < separation) return false;
            }
            return true;
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
