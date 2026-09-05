using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class OverlayChatUi : MonoBehaviour
    {
        const string PrefabResource = "Prefab/UI/ChatPanel";
        const float CardWidth = 300f;
        const float CompactHeight = 360f;
        const float HistoryHeight = 520f;
        const float HeaderHeight = 44f;
        const float StatusHeight = 18f;
        const float ComposerHeight = 52f;
        const float BubbleMaxWidth = 214f;
        const int CompactTake = 12;

        FriendOverlayView _view;
        OverlayChatService _chat;
        [SerializeField] RectTransform _cardRt;
        [SerializeField] GameObject _card;
        [SerializeField] Text _title;
        [SerializeField] Text _historyLabel;
        [SerializeField] Text _status;
        [SerializeField] Text _empty;
        [SerializeField] InputField _input;
        [SerializeField] ScrollRect _scroll;
        [SerializeField] RectTransform _content;
        ulong _friendId;
        bool _open;
        bool _showAll;
        readonly List<ChatRow> _rows = new List<ChatRow>();

        public static OverlayChatUi Create(Transform canvas, FriendOverlayView view, OverlayChatService chat)
        {
            var prefab = Resources.Load<GameObject>(PrefabResource);
            OverlayChatUi ui = null;
            if (prefab != null)
            {
                var root = Instantiate(prefab, canvas, false);
                root.name = "ChatUi";
                Stretch((RectTransform)root.transform);
                ui = root.GetComponent<OverlayChatUi>();
                if (ui == null)
                {
                    Destroy(root);
                }
            }

            if (ui == null)
            {
                var root = new GameObject("ChatUi", typeof(RectTransform));
                root.transform.SetParent(canvas, false);
                Stretch((RectTransform)root.transform);
                ui = root.AddComponent<OverlayChatUi>();
                ui.Build();
            }

            ui._view = view;
            ui._chat = chat;
            ui.Resolve();
            ui.Bind();
            ui.ApplySkin();
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

        void Resolve()
        {
            if (_card == null)
            {
                var card = transform.Find("ChatCard");
                if (card != null)
                {
                    _card = card.gameObject;
                }
            }

            if (_cardRt == null && _card != null)
            {
                _cardRt = (RectTransform)_card.transform;
            }

            _title = First(_title, FindLabel(_cardRt, "Header/Title"));
            _historyLabel = First(_historyLabel, FindLabel(_cardRt, "Header/History"));
            _status = First(_status, FindLabel(_cardRt, "Status"));
            _empty = First(_empty, FindLabel(_cardRt, "Body/Empty"));
            if (_content == null)
            {
                var content = FindNode(_cardRt, "Body/Content");
                if (content != null)
                {
                    _content = (RectTransform)content;
                }
            }

            var body = FindNode(_cardRt, "Body");
            if (body != null)
            {
                _scroll = body.GetComponent<ScrollRect>();
            }

            var input = FindNode(_cardRt, "Input");
            if (input != null)
            {
                _input = input.GetComponent<InputField>();
            }

            ApplyPrefabLayout();
        }

        void ApplyPrefabLayout()
        {
            if (_cardRt != null)
            {
                _cardRt.anchorMin = _cardRt.anchorMax = Vector2.zero;
                _cardRt.pivot = new Vector2(0.5f, 0.5f);
                ApplyCardSize();
            }

            var header = FindNode(_cardRt, "Header") as RectTransform;
            if (header != null)
            {
                PinTop(header, HeaderHeight);
            }

            var title = FindNode(_cardRt, "Header/Title") as RectTransform;
            if (title != null)
            {
                title.anchorMin = Vector2.zero;
                title.anchorMax = Vector2.one;
                title.pivot = new Vector2(0.5f, 0.5f);
                title.offsetMin = new Vector2(14f, 0f);
                title.offsetMax = new Vector2(-108f, 0f);
            }

            var close = FindNode(_cardRt, "Header/Close") as RectTransform;
            if (close != null)
            {
                PinTopRight(close, new Vector2(-8f, -8f), new Vector2(44f, 26f));
            }

            var history = FindNode(_cardRt, "Header/History") as RectTransform;
            if (history != null)
            {
                PinTopRight(history, new Vector2(-56f, -8f), new Vector2(44f, 26f));
            }

            var body = FindNode(_cardRt, "Body") as RectTransform;
            if (body != null)
            {
                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.one;
                body.pivot = new Vector2(0.5f, 0.5f);
                body.offsetMin = new Vector2(8f, ComposerHeight + StatusHeight);
                body.offsetMax = new Vector2(-8f, -HeaderHeight - 2f);
            }

            if (_content != null)
            {
                _content.anchorMin = new Vector2(0f, 1f);
                _content.anchorMax = new Vector2(1f, 1f);
                _content.pivot = new Vector2(0.5f, 1f);
                _content.anchoredPosition = Vector2.zero;
                _content.sizeDelta = new Vector2(0f, Mathf.Max(8f, _content.sizeDelta.y));
            }

            var empty = FindNode(_cardRt, "Body/Empty") as RectTransform;
            if (empty != null)
            {
                Stretch(empty);
            }

            if (_status != null)
            {
                var statusRt = _status.rectTransform;
                statusRt.anchorMin = new Vector2(0f, 0f);
                statusRt.anchorMax = new Vector2(1f, 0f);
                statusRt.pivot = new Vector2(0.5f, 0f);
                statusRt.anchoredPosition = new Vector2(0f, ComposerHeight);
                statusRt.sizeDelta = new Vector2(-20f, StatusHeight);
            }

            var inputRt = FindNode(_cardRt, "Input") as RectTransform;
            if (inputRt != null)
            {
                inputRt.anchorMin = new Vector2(0f, 0f);
                inputRt.anchorMax = new Vector2(1f, 0f);
                inputRt.pivot = new Vector2(0f, 0f);
                inputRt.anchoredPosition = new Vector2(10f, 10f);
                inputRt.sizeDelta = new Vector2(-82f, 32f);
                InsetStretch(inputRt.Find("Placeholder") as RectTransform, 10f);
                InsetStretch(inputRt.Find("Text") as RectTransform, 10f);
            }

            var send = FindNode(_cardRt, "Send") as RectTransform;
            if (send != null)
            {
                PinBottomRight(send, new Vector2(-10f, 10f), new Vector2(52f, 32f));
            }

            StretchNamedLabel(FindNode(_cardRt, "Header/Close"));
            StretchNamedLabel(FindNode(_cardRt, "Header/History"));
            StretchNamedLabel(FindNode(_cardRt, "Send"));
        }

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

        static void StretchNamedLabel(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var label = root.Find("Label") as RectTransform;
            if (label != null)
            {
                Stretch(label);
            }
        }

        void Bind()
        {
            OverlayHoverRelay.Bind(_card, OnCardPointerEnter, null);
            BindClick(FindNode(_cardRt, "Header/Close"), Hide);
            BindClick(FindNode(_cardRt, "Header/History"), ToggleHistory);
            BindClick(FindNode(_cardRt, "Send"), Send);
            EnsureScroll();
            EnsureInput();
            ApplyFonts();
        }

        void EnsureScroll()
        {
            var body = FindNode(_cardRt, "Body");
            if (body == null)
            {
                return;
            }

            if (body.GetComponent<RectMask2D>() == null)
            {
                body.gameObject.AddComponent<RectMask2D>();
            }

            _scroll = body.GetComponent<ScrollRect>();
            if (_scroll == null)
            {
                _scroll = body.gameObject.AddComponent<ScrollRect>();
            }

            if (_content == null)
            {
                var content = body.Find("Content");
                if (content != null)
                {
                    _content = (RectTransform)content;
                }
            }

            _scroll.content = _content;
            _scroll.viewport = (RectTransform)body;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 24f;
        }

        void EnsureInput()
        {
            var inputGo = FindNode(_cardRt, "Input");
            if (inputGo == null)
            {
                return;
            }

            _input = inputGo.GetComponent<InputField>();
            if (_input == null)
            {
                _input = inputGo.gameObject.AddComponent<InputField>();
            }

            var text = FindLabel(inputGo, "Text");
            var placeholder = FindLabel(inputGo, "Placeholder");
            if (text != null)
            {
                text.supportRichText = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                _input.textComponent = text;
            }

            if (placeholder != null)
            {
                _input.placeholder = placeholder;
            }

            _input.lineType = InputField.LineType.SingleLine;
            _input.characterLimit = 200;
            _input.caretColor = OverlaySkin.Text;
            _input.selectionColor = new Color(0.28f, 0.48f, 0.86f, 0.45f);
            _input.onEndEdit.RemoveAllListeners();
            _input.onEndEdit.AddListener(OnEndEdit);
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
        }

        static Text First(Text current, Text found)
        {
            return current != null ? current : found;
        }

        static Transform FindNode(Transform root, string path)
        {
            return root != null ? root.Find(path) : null;
        }

        static Text FindLabel(Transform root, string path)
        {
            var t = FindNode(root, path);
            if (t == null)
            {
                return null;
            }

            var label = t.GetComponent<Text>();
            return label != null ? label : t.GetComponentInChildren<Text>(true);
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

        public bool IsOpen => _open;

        public ulong OpenFriendId => _friendId;

        public void Toggle(ulong friendId)
        {
            if (_open && _friendId == friendId)
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
            _open = true;
            _showAll = false;
            ApplyCardSize();
            _card.SetActive(true);
            transform.SetAsLastSibling();
            _chat.Store.MarkRead(friendId);
            Refresh();
            if (_input != null)
            {
                _input.text = string.Empty;
                if (focusInput)
                {
                    EventSystem.current?.SetSelectedGameObject(_input.gameObject);
                }
            }

            _view?.RefreshChatSelection();
        }

        public void Hide()
        {
            _open = false;
            _friendId = 0;
            if (_card != null)
            {
                _card.SetActive(false);
            }

            _view?.RefreshChatSelection();
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
            if (_open && _chat != null && _chat.Store != null)
            {
                _chat.Store.MarkRead(_friendId);
            }

            Refresh();
        }

        void Build()
        {
            var card = CreateImage("ChatCard", transform, OverlaySprites.Panel, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyPanel(card);
            card.raycastTarget = true;
            _card = card.gameObject;
            _cardRt = card.rectTransform;
            _cardRt.anchorMin = _cardRt.anchorMax = new Vector2(0f, 0f);
            _cardRt.pivot = new Vector2(0.5f, 0.5f);
            ApplyCardSize();
            _card.SetActive(false);

            var header = CreateImage("Header", _cardRt, new Color(1f, 1f, 1f, 0.06f), OverlaySprites.RoundedRect);
            header.raycastTarget = false;
            PinTop(header.rectTransform, HeaderHeight);

            OverlaySkin.ApplyButton(header, well: true);
            _title = PlaceAnchoredLabel(header.rectTransform, "聊天", 16, OverlaySkin.Text, TextAnchor.MiddleLeft);
            _title.gameObject.name = "Title";
            var titleRt = _title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(14f, 0f);
            titleRt.offsetMax = new Vector2(-108f, 0f);

            var close = CreateImage("Close", header.rectTransform, OverlaySprites.Button, OverlaySprites.RoundedRect);
            close.raycastTarget = true;
            PinTopRight(close.rectTransform, new Vector2(-8f, -8f), new Vector2(44f, 26f));
            OverlaySkin.ApplyButton(close);
            FillLabel(close.rectTransform, "关闭", 12, OverlaySkin.Text);

            var historyBtn = CreateImage("History", header.rectTransform, OverlaySprites.Button, OverlaySprites.RoundedRect);
            historyBtn.raycastTarget = true;
            PinTopRight(historyBtn.rectTransform, new Vector2(-56f, -8f), new Vector2(44f, 26f));
            OverlaySkin.ApplyButton(historyBtn);
            _historyLabel = FillLabel(historyBtn.rectTransform, "历史", 12, OverlaySkin.Text);

            var body = CreateImage("Body", _cardRt, OverlaySprites.Well, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(body, well: true);
            body.raycastTarget = true;
            var bodyRt = body.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(8f, ComposerHeight + StatusHeight);
            bodyRt.offsetMax = new Vector2(-8f, -HeaderHeight - 2f);
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

            _status = PlaceAnchoredLabel(_cardRt, "", 11, OverlaySkin.TextMuted, TextAnchor.MiddleCenter);
            _status.gameObject.name = "Status";
            var statusRt = _status.rectTransform;
            statusRt.anchorMin = new Vector2(0f, 0f);
            statusRt.anchorMax = new Vector2(1f, 0f);
            statusRt.pivot = new Vector2(0.5f, 0f);
            statusRt.anchoredPosition = new Vector2(0f, ComposerHeight);
            statusRt.sizeDelta = new Vector2(-20f, StatusHeight);

            var inputBg = CreateImage("Input", _cardRt, OverlaySprites.Button, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(inputBg);
            inputBg.raycastTarget = true;
            var inputRt = inputBg.rectTransform;
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 0f);
            inputRt.pivot = new Vector2(0f, 0f);
            inputRt.anchoredPosition = new Vector2(10f, 10f);
            inputRt.sizeDelta = new Vector2(-82f, 32f);

            var placeholder = PlaceAnchoredLabel(inputRt, "输入消息", 13, OverlaySkin.TextMuted, TextAnchor.MiddleLeft);
            placeholder.gameObject.name = "Placeholder";
            var placeholderRt = placeholder.rectTransform;
            Stretch(placeholderRt);
            placeholderRt.offsetMin = new Vector2(10f, 0f);
            placeholderRt.offsetMax = new Vector2(-10f, 0f);

            var inputText = PlaceAnchoredLabel(inputRt, "", 13, OverlaySkin.Text, TextAnchor.MiddleLeft);
            inputText.gameObject.name = "Text";
            inputText.supportRichText = false;
            inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
            inputText.verticalOverflow = VerticalWrapMode.Overflow;
            var inputTextRt = inputText.rectTransform;
            Stretch(inputTextRt);
            inputTextRt.offsetMin = new Vector2(10f, 0f);
            inputTextRt.offsetMax = new Vector2(-10f, 0f);

            _input = inputBg.gameObject.AddComponent<InputField>();
            _input.textComponent = inputText;
            _input.placeholder = placeholder;

            var send = CreateImage("Send", _cardRt, OverlaySprites.Accent, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(send, accent: true);
            send.raycastTarget = true;
            PinBottomRight(send.rectTransform, new Vector2(-10f, 10f), new Vector2(52f, 32f));
            FillLabel(send.rectTransform, "发送", 13, OverlaySkin.Text);
        }

        public void ApplySkin()
        {
            if (_cardRt == null)
            {
                return;
            }

            OverlaySkin.ApplyPanel(_cardRt.GetComponent<Image>());
            ApplyFonts();
            OverlaySkin.ApplyButton(FindChildImage(_cardRt, "Header"), well: true);
            OverlaySkin.ApplyButton(FindChildImage(_cardRt, "Header/Close"));
            OverlaySkin.ApplyButton(FindChildImage(_cardRt, "Header/History"));
            OverlaySkin.ApplyButton(FindChildImage(_cardRt, "Body"), well: true);
            OverlaySkin.ApplyButton(FindChildImage(_cardRt, "Input"));
            OverlaySkin.ApplyButton(FindChildImage(_cardRt, "Send"), accent: true);
            if (_title != null)
            {
                _title.color = OverlaySkin.Text;
            }

            if (_historyLabel != null)
            {
                _historyLabel.color = OverlaySkin.Text;
            }

            if (_status != null)
            {
                _status.color = OverlaySkin.TextMuted;
            }

            if (_empty != null)
            {
                _empty.color = OverlaySkin.TextMuted;
            }

            if (_input != null)
            {
                _input.caretColor = OverlaySkin.Text;
                if (_input.textComponent != null)
                {
                    _input.textComponent.color = OverlaySkin.Text;
                }

                if (_input.placeholder is Text placeholder)
                {
                    placeholder.color = OverlaySkin.TextMuted;
                }
            }

            PaintLabel(FindChild(_cardRt, "Send"), OverlaySkin.Text);
            PaintLabel(FindChild(_cardRt, "Header/Close"), OverlaySkin.Text);
            PaintLabel(FindChild(_cardRt, "Header/History"), OverlaySkin.Text);

            if (_open)
            {
                Refresh();
            }
        }

        void ToggleHistory()
        {
            _showAll = !_showAll;
            ApplyCardSize();
            Refresh();
        }

        void ApplyCardSize()
        {
            if (_cardRt != null)
            {
                _cardRt.sizeDelta = new Vector2(CardWidth, _showAll ? HistoryHeight : CompactHeight);
            }

            if (_historyLabel != null)
            {
                _historyLabel.text = _showAll ? "收起" : "历史";
            }
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
            if (!_open || _chat == null || _input == null)
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
            EventSystem.current?.SetSelectedGameObject(_input.gameObject);
        }

        void OnCardPointerEnter()
        {
            if (_input != null)
            {
                EventSystem.current?.SetSelectedGameObject(_input.gameObject);
            }
        }

        void Update()
        {
            if (_open && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }

        void LateUpdate()
        {
            if (!_open || _cardRt == null || _view == null)
            {
                return;
            }

            if (!_view.TryGetFollowPosition(_friendId, out var pos))
            {
                return;
            }

            PlaceCardBeside(pos);
        }

        void Refresh()
        {
            if (_title == null || _chat == null || _chat.Store == null)
            {
                return;
            }

            var name = _open ? _view.GetFriendName(_friendId) : "聊天";
            _title.text = Ellipsize(name, 12);
            if (_status != null)
            {
                _status.text = SteamManager.Initialized
                    ? "双方开着本游戏才能送到"
                    : "Steam 未连接，消息只会留在本机";
            }

            if (_historyLabel != null)
            {
                _historyLabel.text = _showAll ? "收起" : "历史";
            }

            if (!_open)
            {
                return;
            }

            RebuildMessages(_chat.Store.GetMessages(_friendId));
        }

        void RebuildMessages(IReadOnlyList<OverlayChatMessage> messages)
        {
            var count = messages != null ? messages.Count : 0;
            var start = 0;
            if (!_showAll && count > CompactTake)
            {
                start = count - CompactTake;
            }

            var visible = count - start;
            if (_empty != null)
            {
                _empty.gameObject.SetActive(visible == 0);
                _empty.text = "还没有消息";
            }

            while (_rows.Count < visible)
            {
                _rows.Add(CreateRow());
            }

            var y = 8f;
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (i >= visible)
                {
                    row.Root.SetActive(false);
                    continue;
                }

                var msg = messages[start + i];
                row.Root.SetActive(true);
                var height = BindRow(row, msg);
                row.Rt.anchoredPosition = new Vector2(0f, -y);
                row.Rt.sizeDelta = new Vector2(0f, height);
                y += height + 6f;
            }

            _content.sizeDelta = new Vector2(0f, Mathf.Max(8f, y + 2f));
            Canvas.ForceUpdateCanvases();
            if (_scroll != null)
            {
                _scroll.verticalNormalizedPosition = 0f;
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

        static float BindRow(ChatRow row, OverlayChatMessage msg)
        {
            var mine = msg != null && msg.mine;
            var text = msg != null ? msg.text : "";
            row.Text.text = text;
            row.Text.alignment = TextAnchor.UpperLeft;
            OverlaySkin.ApplyBubble(row.Bubble, mine);
            row.Text.color = OverlaySkin.Text;

            var bubbleW = Mathf.Clamp(row.Text.preferredWidth + 16f, 36f, BubbleMaxWidth);
            row.BubbleRt.anchorMin = row.BubbleRt.anchorMax = mine ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            row.BubbleRt.pivot = mine ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            row.BubbleRt.anchoredPosition = new Vector2(mine ? -8f : 8f, 0f);
            row.BubbleRt.sizeDelta = new Vector2(bubbleW, 40f);
            var textH = Mathf.Max(16f, row.Text.preferredHeight);
            var bubbleH = textH + 12f;
            row.BubbleRt.sizeDelta = new Vector2(bubbleW, bubbleH);
            return bubbleH;
        }

        void PlaceCardBeside(Vector2 avatarPos)
        {
            var size = _cardRt.sizeDelta;
            var chipSize = _view != null && _view.Config != null ? _view.Config.chipSize : 128f;
            var offsetX = chipSize * 0.5f + 10f + size.x * 0.5f;
            if (avatarPos.x + offsetX + size.x * 0.5f > Screen.width - 12f)
            {
                offsetX = -offsetX;
            }

            var cardPos = avatarPos + new Vector2(offsetX, 18f);
            cardPos.x = Mathf.Clamp(cardPos.x, 12f + size.x * 0.5f, Screen.width - 12f - size.x * 0.5f);
            cardPos.y = Mathf.Clamp(cardPos.y, 12f + size.y * 0.5f, Screen.height - 12f - size.y * 0.5f);
            _cardRt.anchoredPosition = cardPos;
        }

        static Transform FindChild(Transform root, string path)
        {
            return root != null ? root.Find(path) : null;
        }

        static Image FindChildImage(Transform root, string path)
        {
            var child = FindChild(root, path);
            return child != null ? child.GetComponent<Image>() : null;
        }

        static void PaintLabel(Transform root, Color color)
        {
            if (root == null)
            {
                return;
            }

            var label = root.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = color;
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
