using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class OverlayChatUi : MonoBehaviour
    {
        const string PrefabResource = "Prefab/UI/ChatPanel";
        const string ThemeSpriteResource = "Overlay/UI/square_rect";
        const string ControlSpriteResource = "Overlay/UI/control_rect";
        const string SendIconResource = "Overlay/UI/codicon_send";
        const string HistoryIconResource = "Overlay/UI/codicon_history";
        const string CloseIconResource = "Overlay/UI/codicon_close";
        const float CardVisualScale = 1f;
        const float ChatWidth = 300f * OverlaySkin.SettingsChatWidthScale;
        const float ChatHeight = 360f;
        const float HistoryWidth = 420f * OverlaySkin.SettingsChatWidthScale;
        const float HistoryHeight = 520f;
        const float HeaderHeight = 36f;
        const float StatusHeight = 0f;
        const float ComposerHeight = 44f;
        const float CompactMinBodyHeight = 48f;
        const float BubbleMaxWidth = 214f;
        const float ToolbarButtonSize = 28f;
        const float SendButtonWidth = ToolbarButtonSize;
        const float HistoryButtonWidth = ToolbarButtonSize;
        const float ComposerGap = 6f;
        const float WindowInset = 8f;

        FriendOverlayView _view;
        OverlayChatService _chat;
        [SerializeField] GameObject _backdrop;
        [SerializeField] RectTransform _cardRt;
        [SerializeField] GameObject _card;
        [SerializeField] Text _title;
        [SerializeField] Text _status;
        [SerializeField] Text _empty;
        [SerializeField] InputField _input;
        [SerializeField] GameObject _historyButton;
        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _content;
        ulong _friendId;
        ChatMode _mode;
        int _compactStartIndex;
        Coroutine _refocusRoutine;
        readonly List<ChatRow> _rows = new List<ChatRow>();
        readonly List<OverlayChatMessage> _visibleMessages = new List<OverlayChatMessage>();
        Sprite _themeSprite;
        Sprite _controlSprite;

        enum ChatMode
        {
            Closed,
            Compact,
            History
        }

        public static OverlayChatUi Create(Transform canvas, FriendOverlayView view, OverlayChatService chat)
        {
            var prefab = Resources.Load<GameObject>(PrefabResource);
            if (prefab == null)
            {
                Debug.LogError("[Overlay] 缺少聊天界面 Prefab: Resources/" + PrefabResource);
                return null;
            }

            var root = Instantiate(prefab, canvas, false);
            root.name = "ChatUi";
            Stretch((RectTransform)root.transform);
            var ui = root.GetComponent<OverlayChatUi>();
            if (ui == null)
            {
                Debug.LogError("[Overlay] 聊天界面 Prefab 缺少 OverlayChatUi。");
                Destroy(root);
                return null;
            }

            ui._view = view;
            ui._chat = chat;
            ui._cardRt.localScale = new Vector3(CardVisualScale, CardVisualScale, 1f);
            ui.ApplyIcons();
            ui.Bind();
            ui.ApplyTypography();
            ui.ApplyTheme();
            ui.Hide();
            if (chat != null && chat.Store != null)
            {
                chat.Store.Changed -= ui.OnStoreChanged;
                chat.Store.Changed += ui.OnStoreChanged;
            }

            return ui;
        }

#if UNITY_EDITOR
        public void EditorPopulate()
        {
            Build();
        }
#endif

        static void InsetStretch(RectTransform rt, float padX)
        {
            if (rt == null)
            {
                return;
            }

            Stretch(rt);
            rt.offsetMin = new Vector2(padX, 0f);
            rt.offsetMax = new Vector2(-padX, 0f);
        }

        void Bind()
        {
            OverlayHoverRelay.Bind(_card, OnCardPointerEnter, null);
            BindClick(_backdrop != null ? _backdrop.transform : null, Hide);
            BindClick(FindNode(_cardRt, "Header/Close"), Hide);
            BindClick(FindNode(_cardRt, "Send"), Send);
            BindClick(FindNode(_cardRt, "History"), ToggleHistory);
            BindToolbarHover(FindNode(_cardRt, "Header/Close")?.GetComponent<Image>());
            BindToolbarHover(FindNode(_cardRt, "Send")?.GetComponent<Image>());
            BindToolbarHover(FindNode(_cardRt, "History")?.GetComponent<Image>());
            if (_input != null)
            {
                _input.transition = Selectable.Transition.None;
                _input.lineType = InputField.LineType.SingleLine;
                _input.characterLimit = 200;
                _input.caretColor = OverlaySkin.Text;
                _input.selectionColor = new Color(0.28f, 0.48f, 0.86f, 0.45f);
                _input.onEndEdit.RemoveAllListeners();
                _input.onEndEdit.AddListener(OnEndEdit);
            }

            ApplyFonts();
            ApplyLayout();
        }

        static void BindClick(Component graphic, UnityEngine.Events.UnityAction action)
        {
            if (graphic == null)
            {
                return;
            }

            var button = graphic.GetComponent<Button>();
            if (button == null)
            {
                button = graphic.gameObject.AddComponent<Button>();
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            button.transition = Selectable.Transition.None;
        }

        static Transform FindNode(Transform root, string path)
        {
            return root != null ? root.Find(path) : null;
        }

        void ApplyFonts()
        {
            if (_cardRt == null)
            {
                return;
            }

            var labels = _cardRt.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].font = OverlaySprites.UiFont;
            }
        }

        void ApplyTypography()
        {
            if (_cardRt == null)
            {
                return;
            }

            var labels = _cardRt.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var name = labels[i].gameObject.name;
                labels[i].fontSize = name == "Title"
                    ? 14
                    : name == "Status" || name == "Empty" || name == "Placeholder"
                        ? 12
                        : 13;
            }
        }

        void ApplyIcons()
        {
            ApplyIcon(FindNode(_cardRt, "Header/Close/Icon"), Resources.Load<Sprite>(CloseIconResource));
            ApplyIcon(FindNode(_cardRt, "Send/Icon"), Resources.Load<Sprite>(SendIconResource));
            ApplyIcon(FindNode(_cardRt, "History/Icon"), Resources.Load<Sprite>(HistoryIconResource));
        }

        static void ApplyIcon(Transform target, Sprite sprite)
        {
            var image = target != null ? target.GetComponent<Image>() : null;
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(16f, 16f);
        }

        void BindToolbarHover(Image image)
        {
            if (image == null)
            {
                return;
            }

            OverlayHoverRelay.Bind(
                image.gameObject,
                () => image.color = OverlaySkin.ThemeHover(CurrentTheme),
                () => image.color = Color.clear);
        }

        int CurrentTheme => _view != null && _view.Settings != null ? _view.Settings.SettingsTheme : 1;

        public void ApplyTheme()
        {
            if (_cardRt == null)
            {
                return;
            }

            var theme = CurrentTheme;
            _themeSprite = _themeSprite != null ? _themeSprite : Resources.Load<Sprite>(ThemeSpriteResource);
            _controlSprite = _controlSprite != null ? _controlSprite : Resources.Load<Sprite>(ControlSpriteResource);

            var images = _cardRt.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                var image = images[i];
                var name = image.gameObject.name;
                if (name == "Icon")
                {
                    image.color = OverlaySkin.SettingsThemeText(theme);
                    continue;
                }

                if (name == "Divider" || name == "ComposerDivider")
                {
                    image.sprite = _themeSprite;
                    image.type = Image.Type.Sliced;
                    image.color = OverlaySkin.ThemeDivider(theme);
                    continue;
                }

                var toolbarButton = name == "Send" || name == "History" || name == "Close";
                var roundedSurface =
                    name == "ChatCard" || name == "Header" || name == "Body" ||
                    name == "Input" || name == "Bubble";
                image.sprite = toolbarButton
                    ? OverlaySprites.Circle
                    : roundedSurface
                        ? OverlaySprites.RoundedRect
                        : _controlSprite;
                image.type = toolbarButton ? Image.Type.Simple : Image.Type.Sliced;
                image.preserveAspect = toolbarButton;
                image.color = name == "ChatCard"
                    ? OverlaySkin.ThemeBackground(theme)
                    : name == "Header"
                        ? OverlaySkin.ThemeBackground(theme)
                        : name == "Body"
                            ? OverlaySkin.ThemeBackground(theme)
                            : name == "Input"
                                ? OverlaySkin.ThemeInputBackground(theme)
                                : name == "Send" || name == "History" || name == "Close"
                                    ? Color.clear
                                    : OverlaySkin.ThemeControl(theme);
            }

            var labels = _cardRt.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var name = labels[i].gameObject.name;
                labels[i].color = name == "Status" || name == "Empty" || name == "Placeholder"
                    ? OverlaySkin.ThemeMuted(theme)
                    : OverlaySkin.SettingsThemeText(theme);
            }

            if (_input != null)
            {
                _input.caretColor = OverlaySkin.SettingsThemeText(theme);
                var selection = OverlaySkin.ThemeAccent(theme);
                selection.a = 0.45f;
                _input.selectionColor = selection;

                var outline = _input.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = _input.gameObject.AddComponent<Outline>();
                }
                outline.enabled = true;
                outline.effectColor = OverlaySkin.ThemeDivider(theme);
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = false;
            }

            if (_cardRt != null)
            {
                var effects = _cardRt.GetComponents<Shadow>();
                for (var i = 0; i < effects.Length; i++)
                {
                    effects[i].enabled = false;
                }
            }

            if (IsOpen && _chat != null && _chat.Store != null)
            {
                RebuildMessages(_chat.Store.GetMessages(_friendId));
            }
        }

        public bool IsOpen => _mode != ChatMode.Closed;

        public ulong OpenFriendId => _friendId;

        public void Toggle(ulong friendId)
        {
            if (IsOpen && _friendId == friendId)
            {
                Hide();
                return;
            }

            Open(friendId);
        }

        public void Open(ulong friendId)
        {
            Open(friendId, true);
        }

        void Open(ulong friendId, bool focusInput)
        {
            if (friendId == 0 || _chat == null || _chat.Store == null || _view == null || !_view.IsPresent(friendId))
            {
                return;
            }

            _friendId = friendId;
            _mode = ChatMode.Compact;
            var messages = _chat.Store.GetMessages(friendId);
            _compactStartIndex = ResolveCompactStartIndex(messages, friendId);
            ApplyLayout();
            _backdrop.SetActive(true);
            _card.SetActive(true);
            transform.SetAsLastSibling();
            _chat.Store.MarkRead(friendId);
            Refresh();
            if (_input != null)
            {
                _input.text = string.Empty;
                if (focusInput)
                {
                    KeepInputFocused();
                }
            }

            _view?.RefreshChatSelection();
        }

        public void Hide()
        {
            if (_refocusRoutine != null)
            {
                StopCoroutine(_refocusRoutine);
                _refocusRoutine = null;
            }

            if (_input != null)
            {
                _input.DeactivateInputField();
                if (EventSystem.current != null &&
                    EventSystem.current.currentSelectedGameObject == _input.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }

            _mode = ChatMode.Closed;
            _friendId = 0;
            _compactStartIndex = 0;
            if (_card != null)
            {
                _card.SetActive(false);
            }
            if (_backdrop != null)
            {
                _backdrop.SetActive(false);
            }

            _view?.RefreshChatSelection();
        }

        int ResolveCompactStartIndex(IReadOnlyList<OverlayChatMessage> messages, ulong friendId)
        {
            if (messages == null || messages.Count == 0)
            {
                return 0;
            }

            var unreadMessages = _chat.Store.GetUnreadPeerMessages(friendId);
            var anchor = unreadMessages.Count > 0
                ? unreadMessages[0]
                : _chat.Store.GetLatestPeer(friendId);
            if (anchor == null)
            {
                return messages.Count;
            }

            for (var i = 0; i < messages.Count; i++)
            {
                if (ReferenceEquals(messages[i], anchor))
                {
                    return i;
                }
            }

            return messages.Count;
        }

        void OnEnable()
        {
            if (_chat != null && _chat.Store != null)
            {
                _chat.Store.Changed += OnStoreChanged;
            }
        }

        void OnDisable()
        {
            if (_chat != null && _chat.Store != null)
            {
                _chat.Store.Changed -= OnStoreChanged;
            }
        }

        void OnStoreChanged()
        {
            if (!IsOpen || _chat == null || _chat.Store == null)
            {
                return;
            }

            _chat.Store.MarkRead(_friendId);
            Refresh();
        }

        void Build()
        {
            _themeSprite = Resources.Load<Sprite>(ThemeSpriteResource);
            _controlSprite = Resources.Load<Sprite>(ControlSpriteResource);
            var backdrop = CreateImage("Backdrop", transform, Color.clear, null);
            backdrop.raycastTarget = true;
            Stretch(backdrop.rectTransform);
            _backdrop = backdrop.gameObject;

            var card = CreateImage("ChatCard", transform, OverlaySkin.ThemeBackground(1), _themeSprite);
            card.raycastTarget = true;
            _card = card.gameObject;
            _cardRt = card.rectTransform;
            _cardRt.anchorMin = _cardRt.anchorMax = new Vector2(0f, 0f);
            _cardRt.pivot = new Vector2(0.5f, 0.5f);
            _cardRt.localScale = new Vector3(CardVisualScale, CardVisualScale, 1f);
            ApplyCardSize();
            _backdrop.SetActive(false);
            _card.SetActive(false);

            var header = CreateImage("Header", _cardRt, OverlaySkin.ThemeHeader(1), _themeSprite);
            header.raycastTarget = false;
            PinTop(header.rectTransform, HeaderHeight);

            _title = PlaceAnchoredLabel(header.rectTransform, "聊天", 14, OverlaySkin.Text, TextAnchor.MiddleLeft);
            _title.gameObject.name = "Title";
            var titleRt = _title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(12f, 0f);
            titleRt.offsetMax = new Vector2(-42f, 0f);

            var close = CreateImage("Close", header.rectTransform, Color.clear, _controlSprite);
            close.raycastTarget = true;
            close.rectTransform.anchorMin = close.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            close.rectTransform.pivot = new Vector2(1f, 0.5f);
            close.rectTransform.anchoredPosition = new Vector2(-8f, 0f);
            close.rectTransform.sizeDelta = new Vector2(ToolbarButtonSize, ToolbarButtonSize);
            CreateIconPlaceholder(close.rectTransform, 16f);
            CreateDivider(header.rectTransform, "Divider", 0f);

            var body = CreateImage("Body", _cardRt, OverlaySkin.ThemeSection(1), _themeSprite);
            body.raycastTarget = true;
            var bodyRt = body.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(WindowInset, ComposerHeight + StatusHeight);
            bodyRt.offsetMax = new Vector2(-WindowInset, -HeaderHeight - 1f);
            body.gameObject.AddComponent<RectMask2D>();

            _content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            _content.SetParent(bodyRt, false);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 8f);

            _scroll = body.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content;
            _scroll.viewport = bodyRt;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 24f;

            _empty = PlaceAnchoredLabel(bodyRt, "还没有消息", 13, OverlaySkin.TextMuted, TextAnchor.MiddleCenter);
            _empty.gameObject.name = "Empty";
            Stretch(_empty.rectTransform);
            _empty.raycastTarget = false;

            _status = PlaceAnchoredLabel(_cardRt, "", 12, OverlaySkin.TextMuted, TextAnchor.MiddleCenter);
            _status.gameObject.name = "Status";
            _status.gameObject.SetActive(false);
            var statusRt = _status.rectTransform;
            statusRt.anchorMin = new Vector2(0f, 0f);
            statusRt.anchorMax = new Vector2(1f, 0f);
            statusRt.pivot = new Vector2(0.5f, 0f);
            statusRt.anchoredPosition = new Vector2(0f, ComposerHeight);
            statusRt.sizeDelta = new Vector2(-20f, StatusHeight);

            var inputBg = CreateImage("Input", _cardRt, OverlaySkin.ThemeInputBackground(1), _controlSprite);
            inputBg.raycastTarget = true;
            var inputRt = inputBg.rectTransform;
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 0f);
            inputRt.pivot = new Vector2(0f, 0f);
            inputRt.anchoredPosition = new Vector2(WindowInset, WindowInset);
            inputRt.sizeDelta = new Vector2(-84f, 28f);

            var placeholder = PlaceAnchoredLabel(inputRt, "输入消息", 13, OverlaySkin.TextMuted, TextAnchor.MiddleLeft);
            placeholder.gameObject.name = "Placeholder";
            var placeholderRt = placeholder.rectTransform;
            Stretch(placeholderRt);
            placeholderRt.offsetMin = new Vector2(8f, 0f);
            placeholderRt.offsetMax = new Vector2(-8f, 0f);

            var inputText = PlaceAnchoredLabel(inputRt, "", 13, OverlaySkin.Text, TextAnchor.MiddleLeft);
            inputText.gameObject.name = "Text";
            inputText.supportRichText = false;
            inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
            inputText.verticalOverflow = VerticalWrapMode.Overflow;
            var inputTextRt = inputText.rectTransform;
            Stretch(inputTextRt);
            inputTextRt.offsetMin = new Vector2(8f, 0f);
            inputTextRt.offsetMax = new Vector2(-8f, 0f);

            _input = inputBg.gameObject.AddComponent<InputField>();
            _input.textComponent = inputText;
            _input.placeholder = placeholder;
            var inputOutline = inputBg.gameObject.AddComponent<Outline>();
            inputOutline.effectColor = OverlaySkin.ThemeDivider(1);
            inputOutline.effectDistance = new Vector2(1f, -1f);
            inputOutline.useGraphicAlpha = false;

            var send = CreateImage("Send", _cardRt, Color.clear, _controlSprite);
            send.raycastTarget = true;
            PinBottomRight(send.rectTransform,
                new Vector2(-(WindowInset + HistoryButtonWidth + ComposerGap), WindowInset),
                new Vector2(SendButtonWidth, ToolbarButtonSize));
            CreateIconPlaceholder(send.rectTransform, 16f);

            var history = CreateImage("History", _cardRt, Color.clear, _controlSprite);
            history.raycastTarget = true;
            PinBottomRight(history.rectTransform, new Vector2(-WindowInset, WindowInset),
                new Vector2(HistoryButtonWidth, ToolbarButtonSize));
            CreateIconPlaceholder(history.rectTransform, 16f);
            _historyButton = history.gameObject;
        }

        void ApplyLayout()
        {
            ApplyCardSize();
            ApplyComposerLayout();
            if (IsOpen && _view != null && _view.TryGetFollowPosition(_friendId, out var position))
            {
                PlaceCardAbove(position);
            }
        }

        void ApplyCardSize()
        {
            if (_cardRt == null)
            {
                return;
            }

            _cardRt.pivot = new Vector2(0.5f, 0.5f);
            _cardRt.sizeDelta = _mode == ChatMode.History
                ? new Vector2(HistoryWidth, HistoryHeight)
                : new Vector2(ChatWidth, ChatHeight);
        }

        void ApplyComposerLayout()
        {
            var composerDivider = FindNode(_cardRt, "ComposerDivider");
            if (composerDivider != null)
            {
                composerDivider.gameObject.SetActive(false);
            }

            var body = FindNode(_cardRt, "Body") as RectTransform;
            if (body != null)
            {
                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.one;
                body.pivot = new Vector2(0.5f, 0.5f);
                body.offsetMin = new Vector2(WindowInset, ComposerHeight + StatusHeight);
                body.offsetMax = new Vector2(-WindowInset, -HeaderHeight - 1f);
            }

            if (_status != null)
            {
                _status.text = string.Empty;
                _status.gameObject.SetActive(false);
            }

            var inputRt = FindNode(_cardRt, "Input") as RectTransform;
            if (inputRt != null)
            {
                inputRt.anchorMin = new Vector2(0f, 0f);
                inputRt.anchorMax = new Vector2(1f, 0f);
                inputRt.pivot = new Vector2(0f, 0f);
                inputRt.anchoredPosition = new Vector2(WindowInset, WindowInset);
                var reserved = 50f + HistoryButtonWidth + ComposerGap;
                inputRt.sizeDelta = new Vector2(-reserved, 28f);
                InsetStretch(inputRt.Find("Placeholder") as RectTransform, 8f);
                InsetStretch(inputRt.Find("Text") as RectTransform, 8f);
            }

            var send = FindNode(_cardRt, "Send") as RectTransform;
            if (send != null)
            {
                var sendX = -(WindowInset + HistoryButtonWidth + ComposerGap);
                PinBottomRight(send, new Vector2(sendX, WindowInset),
                    new Vector2(SendButtonWidth, ToolbarButtonSize));
            }

            var history = FindNode(_cardRt, "History") as RectTransform;
            if (history != null)
            {
                PinBottomRight(history, new Vector2(-WindowInset, WindowInset),
                    new Vector2(HistoryButtonWidth, ToolbarButtonSize));
                history.gameObject.SetActive(_mode != ChatMode.Closed);
                _historyButton = history.gameObject;
            }
        }

        void ToggleHistory()
        {
            if (_mode == ChatMode.Closed)
            {
                return;
            }

            _mode = _mode == ChatMode.History
                ? ChatMode.Compact
                : ChatMode.History;
            ApplyLayout();
            Refresh();
            KeepInputFocused();
        }

        void OnEndEdit(string _)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Send();
            }
        }

        void Send()
        {
            if (!IsOpen || _chat == null || _input == null)
            {
                return;
            }

            if (_view == null || !_view.IsPresent(_friendId))
            {
                Hide();
                return;
            }

            var text = _input.text;
            _input.text = string.Empty;
            _chat.Send(_friendId, text);
            KeepInputFocused();
        }

        void KeepInputFocused()
        {
            if (!isActiveAndEnabled)
            {
                FocusInputNow();
                return;
            }

            if (_refocusRoutine != null)
            {
                StopCoroutine(_refocusRoutine);
            }

            _refocusRoutine = StartCoroutine(RefocusInputNextFrame());
        }

        IEnumerator RefocusInputNextFrame()
        {
            yield return null;
            _refocusRoutine = null;
            FocusInputNow();
        }

        void FocusInputNow()
        {
            if (!IsOpen || _input == null || !_input.gameObject.activeInHierarchy)
            {
                return;
            }

            _view?.GetComponent<TransparentOverlayWindow>()?.FocusForTextInput();
            EventSystem.current?.SetSelectedGameObject(_input.gameObject);
            _input.ActivateInputField();
            _input.Select();
        }

        void OnCardPointerEnter()
        {
            KeepInputFocused();
        }

        void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }

        void LateUpdate()
        {
            if (!IsOpen || _cardRt == null || _view == null)
            {
                return;
            }

            if (!_view.TryGetFollowPosition(_friendId, out var pos))
            {
                return;
            }

            PlaceCardAbove(pos);
        }

        void Refresh()
        {
            if (_title == null || _chat == null || _chat.Store == null)
            {
                return;
            }

            var name = IsOpen ? _view.GetFriendName(_friendId) : "聊天";
            _title.text = Ellipsize(name, 12) + (_mode == ChatMode.History ? " · 历史" : "");
            if (!IsOpen)
            {
                return;
            }

            RebuildMessages(_chat.Store.GetMessages(_friendId));
        }

        void RebuildMessages(IReadOnlyList<OverlayChatMessage> messages)
        {
            var count = messages != null ? messages.Count : 0;
            if (_mode == ChatMode.History)
            {
                _visibleMessages.Clear();
                for (var i = 0; i < count; i++)
                {
                    if (messages[i] != null)
                    {
                        _visibleMessages.Add(messages[i]);
                    }
                }
            }
            else
            {
                CollectVisibleMessages(messages, count);
            }

            var visible = _visibleMessages.Count;
            if (_empty != null)
            {
                _empty.gameObject.SetActive(visible == 0);
            }

            while (_rows.Count < visible)
            {
                _rows.Add(CreateRow());
            }

            var y = 10f;
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (i >= visible)
                {
                    row.Root.SetActive(false);
                    continue;
                }

                var msg = _visibleMessages[i];
                row.Root.SetActive(true);
                var height = BindRow(row, msg);
                row.Rt.anchoredPosition = new Vector2(0f, -y);
                row.Rt.sizeDelta = new Vector2(0f, height);
                y += height + 6f;
            }

            var contentHeight = Mathf.Max(8f, y + 2f);
            _content.sizeDelta = new Vector2(0f, contentHeight);
            FitCompactHeight(contentHeight);
            Canvas.ForceUpdateCanvases();
            if (_scroll != null)
            {
                _scroll.verticalNormalizedPosition = 0f;
            }
        }

        void FitCompactHeight(float contentHeight)
        {
            if (_mode != ChatMode.Compact || _cardRt == null)
            {
                return;
            }

            var fixedHeight = HeaderHeight + StatusHeight + ComposerHeight + 1f;
            var maxBodyHeight = Mathf.Max(CompactMinBodyHeight, ChatHeight - fixedHeight);
            var bodyHeight = Mathf.Clamp(contentHeight, CompactMinBodyHeight, maxBodyHeight);
            _cardRt.sizeDelta = new Vector2(ChatWidth, fixedHeight + bodyHeight);
            ApplyComposerLayout();

            if (_view != null && _view.TryGetFollowPosition(_friendId, out var position))
            {
                PlaceCardAbove(position);
            }
        }

        void CollectVisibleMessages(IReadOnlyList<OverlayChatMessage> messages, int count)
        {
            _visibleMessages.Clear();
            var max = _view != null && _view.Config != null
                ? Mathf.Max(1, _view.Config.maxCompactChatMessages)
                : 5;
            var start = Mathf.Max(Mathf.Clamp(_compactStartIndex, 0, count), count - max);
            for (var i = start; i < count; i++)
            {
                if (messages[i] != null)
                {
                    _visibleMessages.Add(messages[i]);
                }
            }
        }

        ChatRow CreateRow()
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);

            var bubble = CreateImage("Bubble", rt, OverlaySprites.Button, OverlaySprites.RoundedRect);
            bubble.type = Image.Type.Sliced;
            bubble.raycastTarget = false;
            var bubbleRt = bubble.rectTransform;
            bubbleRt.anchorMin = bubbleRt.anchorMax = new Vector2(0f, 1f);
            bubbleRt.pivot = new Vector2(0f, 1f);

            var text = PlaceAnchoredLabel(bubbleRt, "", 13, Color.white, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(8f, 6f);
            text.rectTransform.offsetMax = new Vector2(-8f, -6f);

            return new ChatRow
            {
                Root = go,
                Rt = rt,
                Bubble = bubble,
                BubbleRt = bubbleRt,
                Text = text
            };
        }

        float BindRow(ChatRow row, OverlayChatMessage msg)
        {
            var mine = msg != null && msg.mine;
            var text = msg != null ? msg.text : "";
            var theme = _view != null && _view.Settings != null ? _view.Settings.SettingsTheme : 1;
            row.Text.text = text;
            row.Text.alignment = TextAnchor.UpperLeft;
            row.Bubble.sprite = OverlaySprites.RoundedRect;
            row.Bubble.type = Image.Type.Sliced;
            row.Bubble.color = mine
                ? Color.Lerp(
                    OverlaySkin.ThemeControl(theme),
                    OverlaySkin.ThemeAccent(theme),
                    theme == 2 ? 0.22f : 0.42f)
                : OverlaySkin.ThemeControl(theme);
            row.Text.color = OverlaySkin.SettingsThemeText(theme);

            var windowWidth = _mode == ChatMode.History ? HistoryWidth : ChatWidth;
            var maxWidth = Mathf.Min(BubbleMaxWidth, windowWidth - 24f);
            var bubbleW = Mathf.Clamp(row.Text.preferredWidth + 16f, 36f, maxWidth);
            row.BubbleRt.anchorMin = row.BubbleRt.anchorMax = mine ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            row.BubbleRt.pivot = mine ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            row.BubbleRt.anchoredPosition = new Vector2(mine ? -10f : 10f, 0f);
            row.BubbleRt.sizeDelta = new Vector2(bubbleW, 40f);
            var textH = Mathf.Max(16f, row.Text.preferredHeight);
            var bubbleH = textH + 12f;
            row.BubbleRt.sizeDelta = new Vector2(bubbleW, bubbleH);
            return bubbleH;
        }

        void PlaceCardAbove(Vector2 avatarPos)
        {
            var size = Vector2.Scale(_cardRt.sizeDelta, new Vector2(
                Mathf.Abs(_cardRt.localScale.x),
                Mathf.Abs(_cardRt.localScale.y)));
            var chipSize = _view != null && _view.Config != null ? _view.Config.chipSize : 128f;
            var scale = _view != null && _view.Settings != null ? _view.Settings.Scale : 1f;
            var gap = 8f * scale;
            var bounds = FriendOverlayView.OverlayPixelSize;
            const float margin = 12f;
            var avatarHalf = chipSize * 0.5f * scale;
            var above = avatarPos.y + avatarHalf + gap + size.y * 0.5f;
            var below = avatarPos.y - avatarHalf - gap - size.y * 0.5f;
            var y = above;
            if (above + size.y * 0.5f > bounds.y - margin &&
                below - size.y * 0.5f >= margin)
            {
                y = below;
            }

            var cardPos = new Vector2(avatarPos.x, y);
            cardPos.x = Mathf.Clamp(cardPos.x, margin + size.x * 0.5f, bounds.x - margin - size.x * 0.5f);
            cardPos.y = Mathf.Clamp(cardPos.y, margin + size.y * 0.5f, bounds.y - margin - size.y * 0.5f);
            _cardRt.anchoredPosition = cardPos;
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

        static Image CreateIconPlaceholder(Transform parent, float size)
        {
            var icon = CreateImage("Icon", parent, Color.white, null);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var rt = icon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);
            return icon;
        }

        void CreateDivider(Transform parent, string name, float y)
        {
            var divider = CreateImage(name, parent, OverlaySkin.ThemeDivider(1), _themeSprite);
            divider.raycastTarget = false;
            var rt = divider.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(0f, 1f);
            divider.transform.SetAsLastSibling();
        }

        static Text FillLabel(Transform parent, string text, int size, Color color)
        {
            var label = PlaceAnchoredLabel(parent, text, size, color, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return label;
        }

        static Text PlaceAnchoredLabel(Transform parent, string text, int size, Color color, TextAnchor align)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = OverlaySprites.UiFont;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        static void PinTop(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
        }

        static void PinTopRight(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void PinBottomRight(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static string Ellipsize(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text ?? "";
            }

            return text.Substring(0, max) + "…";
        }

        sealed class ChatRow
        {
            public GameObject Root;
            public RectTransform Rt;
            public Image Bubble;
            public RectTransform BubbleRt;
            public Text Text;
        }
    }
}
