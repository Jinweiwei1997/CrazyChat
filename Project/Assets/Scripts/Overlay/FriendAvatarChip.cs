using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class FriendAvatarChip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const float BubbleVisualScale = 0.75f;
        const float BubbleBaseWidth = 118f * BubbleVisualScale;
        const float BubbleHeight = 28f * BubbleVisualScale;
        const float BubbleMinWidth = 48f * BubbleVisualScale;
        const float BubbleMaxWidth = 220f * BubbleVisualScale;
        const float BubbleTextPad = 20f * BubbleVisualScale;
        const float BubbleOffsetY = 6f * BubbleVisualScale;
        const int BubbleFontSize = 9;

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
        bool _presenceActive;
        bool _presenceMode;
        Image _ring;
        Image _dash;
        GameObject _nameRoot;
        Text _nameText;
        Text _countText;
        Image _bubble;
        CanvasGroup _bubbleFade;
        Text _bubbleText;
        Image _badge;
        Text _badgeText;
        Image _nameBg;
        string _bubbleContent = "...";
        int _unread;
        bool _selected;
        Vector2 _layoutPos;
        bool _dragging;
        float _reactionUntil;
        float _tapFlip = 1f;
        OverlayClickEffect _playedEffect = OverlayClickEffect.Elastic;
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

            _dash = CreateImage("Dash", _body, new Color(1f, 1f, 1f, 0.95f), OverlaySprites.DashedRoundedSquare);
            Stretch(_dash.rectTransform);
            _dash.rectTransform.offsetMin = new Vector2(-6f, -6f);
            _dash.rectTransform.offsetMax = new Vector2(6f, 6f);
            _dash.raycastTarget = false;
            _dash.gameObject.SetActive(false);

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

            _nameRoot = new GameObject("NameTag", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _nameRoot.transform.SetParent(_rect, false);
            _nameBg = _nameRoot.GetComponent<Image>();
            OverlaySkin.ApplyButton(_nameBg);
            _nameBg.raycastTarget = false;
            var nameRt = (RectTransform)_nameRoot.transform;
            nameRt.anchorMin = new Vector2(0f, 0.5f);
            nameRt.anchorMax = new Vector2(0f, 0.5f);
            nameRt.pivot = new Vector2(1f, 0.5f);
            nameRt.anchoredPosition = new Vector2(-8f, 0f);
            nameRt.sizeDelta = new Vector2(140f, 28f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(_nameRoot.transform, false);
            _nameText = textGo.GetComponent<Text>();
            _nameText.font = OverlaySprites.UiFont;
            _nameText.fontSize = 14;
            _nameText.alignment = TextAnchor.MiddleCenter;
            _nameText.color = OverlaySkin.Text;
            _nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _nameText.verticalOverflow = VerticalWrapMode.Overflow;
            _nameText.raycastTarget = false;
            Stretch((RectTransform)textGo.transform);
            var textRt = (RectTransform)textGo.transform;
            textRt.offsetMin = new Vector2(8f, 0f);
            textRt.offsetMax = new Vector2(-8f, 0f);

            _nameRoot.SetActive(false);

            var countGo = new GameObject("TapCount", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            countGo.transform.SetParent(_rect, false);
            _countText = countGo.GetComponent<Text>();
            _countText.font = OverlaySprites.UiFont;
            _countText.fontSize = 26;
            _countText.alignment = TextAnchor.MiddleCenter;
            _countText.color = new Color(1f, 1f, 1f, 0.9f);
            _countText.raycastTarget = false;
            var countRt = (RectTransform)countGo.transform;
            countRt.anchorMin = new Vector2(0.5f, 0f);
            countRt.anchorMax = new Vector2(0.5f, 0f);
            countRt.pivot = new Vector2(0.5f, 1f);
            countRt.anchoredPosition = new Vector2(0f, -2f);
            countRt.sizeDelta = new Vector2(176f, 40f);
            countGo.SetActive(false);

            _bubble = CreateImage("Bubble", _rect, OverlaySprites.Panel, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(_bubble);
            _bubble.raycastTarget = true;
            var bubbleRt = _bubble.rectTransform;
            bubbleRt.anchorMin = new Vector2(0.5f, 1f);
            bubbleRt.anchorMax = new Vector2(0.5f, 1f);
            bubbleRt.pivot = new Vector2(0.5f, 0f);
            bubbleRt.anchoredPosition = new Vector2(0f, BubbleOffsetY);
            bubbleRt.sizeDelta = new Vector2(BubbleBaseWidth, BubbleHeight);
            _bubbleFade = _bubble.gameObject.AddComponent<CanvasGroup>();
            _bubbleFade.blocksRaycasts = true;
            _bubbleText = FillChipLabel(_bubble.rectTransform, "...", BubbleFontSize, OverlaySkin.Text);

            _badge = CreateImage("Badge", _rect, new Color(0.92f, 0.28f, 0.28f, 1f), OverlaySprites.Circle);
            _badge.raycastTarget = false;
            var badgeRt = _badge.rectTransform;
            badgeRt.anchorMin = new Vector2(1f, 1f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(-4f, -4f);
            badgeRt.sizeDelta = new Vector2(22f, 22f);
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
            var width = Mathf.Clamp(_nameText.preferredWidth + 20f, 72f, 220f);
            ((RectTransform)_nameRoot.transform).sizeDelta = new Vector2(width, 28f);
            RefreshCount();
            RefreshChatChrome();
            ApplySkin();
            RefreshPresenceVisual();
        }

        public bool PresenceMode => _presenceMode;

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
            OverlaySkin.ApplyButton(_nameBg);
            OverlaySkin.ApplyButton(_bubble);
            if (_nameText != null)
            {
                _nameText.color = OverlaySkin.Text;
            }

            if (_bubbleText != null)
            {
                _bubbleText.color = OverlaySkin.Text;
            }
        }

        public void SetChatPreview(string text, int unread)
        {
            var next = string.IsNullOrEmpty(text) ? "..." : Ellipsize(text, 8);
            if (next != _bubbleContent && _bubbleFade != null)
            {
                _bubbleFade.alpha = 0.15f;
            }

            _bubbleContent = next;
            _unread = unread;
            RefreshChatChrome();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            RefreshChatChrome();
        }

        void ApplyBubbleScale(float userScale)
        {
            if (_bubble == null)
            {
                return;
            }

            var inv = userScale > 0.0001f ? 1f / userScale : 1f;
            _bubble.transform.localScale = new Vector3(inv, inv, 1f);
            _bubble.rectTransform.anchoredPosition = new Vector2(0f, BubbleOffsetY / userScale);
        }

        void RefreshChatChrome()
        {
            var showChat = _friend != null && !_friend.IsLocal;
            if (_bubble != null)
            {
                _bubble.gameObject.SetActive(showChat);
                if (showChat && _bubbleText != null)
                {
                    _bubbleText.text = _unread > 0
                        ? _bubbleContent + "（未读 " + FormatUnread(_unread) + "）"
                        : _bubbleContent;
                    var width = Mathf.Clamp(_bubbleText.preferredWidth + BubbleTextPad, BubbleMinWidth, BubbleMaxWidth);
                    _bubble.rectTransform.sizeDelta = new Vector2(width, BubbleHeight);
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

            if (_dash != null)
            {
                _dash.gameObject.SetActive(_selected && _friend != null && !_friend.IsLocal);
            }

            if (_ring != null && _friend != null)
            {
                _ring.color = _friend.IsLocal
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

        public void PlayReaction()
        {
            if (_presenceMode)
            {
                return;
            }

            var effect = _view != null && _view.Settings != null
                ? _view.Settings.ClickEffect
                : OverlayClickEffect.Elastic;
            PlayReaction(effect);
        }

        public void PlayReaction(OverlayClickEffect effect)
        {
            if (_presenceMode)
            {
                return;
            }

            _playedEffect = effect;
            if (effect == OverlayClickEffect.Flip)
            {
                _tapFlip = -_tapFlip;
            }

            _reactionUntil = Time.unscaledTime + ReactionSeconds;
        }

        float ReactionSeconds
        {
            get
            {
                var seconds = _view != null && _view.Config != null ? _view.Config.reactionSeconds : 0.12f;
                return Mathf.Max(0.05f, seconds);
            }
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
            ApplyBubbleScale(userScale);

            if (_dragging || _body == null)
            {
                if (_body != null)
                {
                    _body.anchoredPosition = Vector2.zero;
                    _body.localScale = Vector3.one;
                }

                return;
            }

            var press = 0f;
            var effect = IsLocal && _view != null && _view.Settings != null
                ? _view.Settings.ClickEffect
                : _playedEffect;
            var reacting = Time.unscaledTime < _reactionUntil;
            var t = reacting ? 1f - (_reactionUntil - Time.unscaledTime) / ReactionSeconds : 1f;
            if (reacting && effect != OverlayClickEffect.Flip)
            {
                press = Mathf.Sin(t * Mathf.PI);
            }

            var hover = _hover ? 1.06f : 1f;
            var baseFlip = IsLocal && _view != null && _view.Settings != null && _view.Settings.FlipHorizontal ? -1f : 1f;
            var tapFlip = effect == OverlayClickEffect.Flip ? _tapFlip : 1f;
            var flipX = baseFlip * tapFlip;
            if (effect == OverlayClickEffect.Flip && reacting)
            {
                var dest = _tapFlip;
                var src = -_tapFlip;
                flipX = baseFlip * (t < 0.5f ? src * (1f - t * 2f) : dest * ((t - 0.5f) * 2f));
                if (Mathf.Abs(flipX) < 0.04f)
                {
                    flipX = 0.04f * Mathf.Sign(t < 0.5f ? src : dest) * baseFlip;
                }
            }

            _body.anchoredPosition = new Vector2(0f, -press * 7f);
            _body.localScale = new Vector3(hover * (1f + press * 0.04f) * flipX, hover * (1f - press * 0.1f), 1f);
            if (_bubbleFade != null && _bubbleFade.alpha < 1f)
            {
                _bubbleFade.alpha = Mathf.MoveTowards(_bubbleFade.alpha, 1f, Time.unscaledDeltaTime * 4f);
            }
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
