using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    // Owns the local checklist window and the data-driven rows in the settings prefab.
    public sealed class OverlayTodoUi : MonoBehaviour
    {
        const float Width = 300f * OverlaySkin.SettingsChatWidthScale;
        const float RowHeight = 42f;
        const string ThemeSpriteResource = "Overlay/UI/square_rect";
        const string ControlSpriteResource = "Overlay/UI/control_rect";
        const string CloseIconResource = "Overlay/UI/codicon_close";
        FriendOverlayView _view;
        RectTransform _card;
        Image _cardImage;
        Image _headerImage;
        Image _dividerImage;
        Image _closeIcon;
        RectTransform _content;
        ScrollRect _scroll;
        Transform _settingsPage;
        Transform _settingsContent;
        GameObject _settingsTemplate;
        ScrollRect _settingsScroll;
        readonly Dictionary<string, GameObject> _editRows = new Dictionary<string, GameObject>();
        readonly Dictionary<string, Button> _rows = new Dictionary<string, Button>();
        readonly List<OverlayUserSettings.TodoItem> _visible = new List<OverlayUserSettings.TodoItem>();
        Sprite _themeSprite;
        Sprite _controlSprite;
        DateTime _date;
        FriendAvatarChip _previewChip;
        RectTransform _checkBar;
        Image _checkTrack;
        Image _checkKnob;
        Text _checkMark;
        RectTransform _checkHintRoot;
        Image _checkHintBg;
        Text _checkHint;
        RectTransform _checkEditor;
        InputField _checkInput;
        float _checkSlide;
        bool _checkHover;
        bool _checkDragging;
        float _checkPunch;
        RectTransform _checkFxRoot;
        public bool IsEditingCheckin => _checkEditor != null && _checkEditor.gameObject.activeSelf;
        public bool IsOpen => _card != null && _card.gameObject.activeSelf;

        public static OverlayTodoUi Create(Transform parent, Transform chrome, FriendOverlayView view)
        {
            var root = new GameObject("TodoUi", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch((RectTransform)root.transform);
            var ui = root.AddComponent<OverlayTodoUi>();
            ui._view = view;
            ui.Build();
            ui.BuildCheckin(chrome);
            view.Settings.TodosChanged += ui.Refresh;
            ui.Refresh();
            ui.Hide();
            return ui;
        }

        void OnDestroy()
        {
            if (_checkBar != null) Destroy(_checkBar.gameObject);
            if (_checkHintRoot != null) Destroy(_checkHintRoot.gameObject);
            if (_checkFxRoot != null) Destroy(_checkFxRoot.gameObject);
            if (_view != null && _view.Settings != null)
                _view.Settings.TodosChanged -= Refresh;
        }

        public void BindSettings(Transform page)
        {
            if (page == null)
            {
                Debug.LogError("[Overlay] 设置 Prefab 缺少 TodoPage。");
                return;
            }
            _settingsScroll = page.Find("List").GetComponent<ScrollRect>();
            _settingsPage = page;
            _settingsContent = _settingsScroll.content;
            _settingsTemplate = page.Find("RowTemplate").gameObject;
            page.Find("Add").GetComponent<Button>().onClick.AddListener(() =>
            {
                var item = _view.Settings.AddTodo();
                Canvas.ForceUpdateCanvases();
                _settingsScroll.verticalNormalizedPosition = 0f;
                var input = _editRows[item.id].GetComponentInChildren<InputField>();
                input.Select();
                input.ActivateInputField();
            });
            Refresh();
        }

        public void Toggle()
        {
            if (IsOpen) { Hide(); return; }
            Refresh();
            if (_visible.Count == 0) return;
            _card.gameObject.SetActive(true);
            _scroll.verticalNormalizedPosition = 1f;
            _view.LocalChip?.SetTodoExpanded(true);
            PlaceCard();
        }

        public void Hide()
        {
            CancelCheckinEdit();
            _card.gameObject.SetActive(false);
            _view.LocalChip?.SetTodoExpanded(false);
        }

        void Build()
        {
            _themeSprite = Resources.Load<Sprite>(ThemeSpriteResource);
            _controlSprite = Resources.Load<Sprite>(ControlSpriteResource);
            _cardImage = ImageRect("TodoCard", transform, OverlaySkin.ThemeBackground(1), OverlaySprites.RoundedRect);
            _card = _cardImage.rectTransform;
            _card.anchorMin = _card.anchorMax = Vector2.zero;
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(Width, 100f);

            _headerImage = ImageRect("Header", _card, OverlaySkin.ThemeHeader(1), OverlaySprites.RoundedRect);
            _headerImage.raycastTarget = false;
            PinTop(_headerImage.rectTransform, 0f, 36f);
            var title = Label("Title", _headerImage.rectTransform, "我的事项", 14);
            PinTop(title.rectTransform, 0f, 36f);
            title.rectTransform.offsetMin = new Vector2(12f, title.rectTransform.offsetMin.y);
            title.rectTransform.offsetMax = new Vector2(-42f, title.rectTransform.offsetMax.y);

            var close = IconButton("Close", _headerImage.rectTransform, Resources.Load<Sprite>(CloseIconResource), out _closeIcon);
            var closeRt = (RectTransform)close.transform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = Vector2.one;
            closeRt.anchoredPosition = new Vector2(-4f, -4f);
            closeRt.sizeDelta = new Vector2(28f, 28f);
            close.onClick.AddListener(Hide);

            _dividerImage = ImageRect("Divider", _headerImage.rectTransform, OverlaySkin.ThemeDivider(1), _themeSprite);
            _dividerImage.raycastTarget = false;
            var dividerRt = _dividerImage.rectTransform;
            dividerRt.anchorMin = new Vector2(0f, 0f);
            dividerRt.anchorMax = new Vector2(1f, 0f);
            dividerRt.pivot = new Vector2(0.5f, 0f);
            dividerRt.sizeDelta = new Vector2(0f, 1f);

            _scroll = BuildScroll(_card);
            _scroll.name = "List";
            var listRt = (RectTransform)_scroll.transform;
            listRt.offsetMin = new Vector2(6f, 6f);
            listRt.offsetMax = new Vector2(-6f, -36f);
            _content = _scroll.content;
        }

        void Refresh()
        {
            _date = DateTime.Now;
            _visible.Clear();
            foreach (var item in _view.Settings.Todos)
                if (item.IsCheckin || !string.IsNullOrWhiteSpace(item.text)) _visible.Add(item);
            RemoveStale(_rows, id => _visible.Exists(item => item.id == id));
            var y = 0f;
            foreach (var item in _visible)
            {
                if (!_rows.TryGetValue(item.id, out var button))
                {
                    button = ButtonRect("Todo", _content, "");
                    button.onClick.AddListener(() => _view.Settings.CompleteTodo(item));
                    var check = ButtonRect("Check", button.transform, "");
                    var checkRt = (RectTransform)check.transform;
                    checkRt.anchorMin = checkRt.anchorMax = new Vector2(0f, 0.5f);
                    checkRt.pivot = new Vector2(0f, 0.5f);
                    checkRt.anchoredPosition = Vector2.zero;
                    checkRt.sizeDelta = new Vector2(28f, 28f);
                    check.GetComponentInChildren<Text>().fontSize = 18;
                    check.onClick.AddListener(() => _view.Settings.ToggleTodo(item));
                    _rows.Add(item.id, button);
                }
                var text = button.GetComponentInChildren<Text>();
                text.text = item.DisplayText;
                button.transform.Find("Check").GetComponentInChildren<Text>().text = Status(item);
                text.supportRichText = false;
                text.alignment = TextAnchor.MiddleLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.rectTransform.offsetMin = new Vector2(30f, 2f);
                text.rectTransform.offsetMax = new Vector2(-6f, -2f);
                var textHeight = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,
                    text.GetGenerationSettings(new Vector2(Width - 48f, 0f))) / text.pixelsPerUnit;
                var height = Mathf.Max(RowHeight, Mathf.Ceil(textHeight) + 12f);
                PinTop((RectTransform)button.transform, y, height);
                y += height;
            }
            _content.sizeDelta = new Vector2(0f, y);
            _card.sizeDelta = new Vector2(Width, 42f + Mathf.Clamp(y, 48f, 294f));
            if (_visible.Count == 0) Hide();
            RefreshSettingsRows();
            RefreshPreview();
            ApplyTheme();
        }

        void RefreshSettingsRows()
        {
            if (_settingsContent == null) return;
            var stale = new List<string>();
            foreach (var pair in _editRows)
                if (!_view.Settings.Todos.Exists(item => item.id == pair.Key)) stale.Add(pair.Key);
            foreach (var id in stale)
            {
                _editRows[id].SetActive(false);
                Destroy(_editRows[id]);
                _editRows.Remove(id);
            }
            var y = 0f;
            foreach (var item in _view.Settings.Todos)
            {
                if (!_editRows.TryGetValue(item.id, out var row))
                {
                    row = Instantiate(_settingsTemplate, _settingsContent, false);
                    row.name = "TodoRow";
                    row.SetActive(true);
                    _editRows.Add(item.id, row);
                    var input = row.GetComponentInChildren<InputField>(true);
                    var frequency = row.GetComponentInChildren<Dropdown>(true);
                    input.SetTextWithoutNotify(item.text ?? "");
                    frequency.SetValueWithoutNotify((int)item.frequency);
                    input.onEndEdit.AddListener(value =>
                        _view.Settings.EditTodo(item, value, item.frequency));
                    frequency.onValueChanged.AddListener(value =>
                        _view.Settings.EditTodo(item, input.text, (OverlayUserSettings.TodoFrequency)value));
                    row.transform.Find("Delete").GetComponent<Button>().onClick.AddListener(() =>
                        _view.Settings.DeleteTodo(item));
                }
                PinTop((RectTransform)row.transform, y, 32f);
                var rowInput = row.GetComponentInChildren<InputField>(true);
                if (!rowInput.isFocused) rowInput.SetTextWithoutNotify(item.DisplayText ?? "");
                row.GetComponentInChildren<Dropdown>(true).interactable = !item.IsCheckin;
                row.transform.Find("Delete").gameObject.SetActive(!item.IsCheckin);
                y += 36f;
            }
            ((RectTransform)_settingsContent).sizeDelta = new Vector2(0f, y);
        }

        static void RemoveStale(Dictionary<string, Button> rows, Predicate<string> keep)
        {
            var stale = new List<string>();
            foreach (var pair in rows) if (!keep(pair.Key)) stale.Add(pair.Key);
            foreach (var id in stale)
            {
                rows[id].gameObject.SetActive(false);
                Destroy(rows[id].gameObject);
                rows.Remove(id);
            }
        }

        string Status(OverlayUserSettings.TodoItem item) => item.IsComplete(_date) ? "☑" : "☐";

        void RefreshPreview()
        {
            _previewChip = _view.LocalChip;
            // Self avatar no longer shows the daily-todo preview bubble.
            _previewChip?.SetTodoPreview("");
        }

        void Update()
        {
            var now = DateTime.Now;
            if (_date.Date != now.Date || _date.AddHours(-6).Date != now.AddHours(-6).Date) Refresh();
            if (_previewChip != _view.LocalChip) RefreshPreview();
            if (IsOpen && Input.GetMouseButtonDown(0) &&
                !RectTransformUtility.RectangleContainsScreenPoint(_card, Input.mousePosition) &&
                (_view.LocalChip == null || !RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)_view.LocalChip.transform, Input.mousePosition))) Hide();
            if (IsEditingCheckin)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) CancelCheckinEdit();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) SaveCheckinEdit();
                else if (Input.GetMouseButtonDown(0) &&
                    !RectTransformUtility.RectangleContainsScreenPoint(_checkEditor, Input.mousePosition) &&
                    !RectTransformUtility.RectangleContainsScreenPoint(_checkBar, Input.mousePosition)) CancelCheckinEdit();
            }
        }

        void LateUpdate()
        {
            if (IsOpen) PlaceCard();
            UpdateCheckin();
        }

        void BuildCheckin(Transform chrome)
        {
            var chipW = _view.Config != null ? _view.Config.chipSize : 128f;
            const float trackH = 22f;
            const float knob = 20f;
            const float editHit = 24f;
            const float gap = 2f;
            var trackW = Mathf.Max(48f, chipW - editHit - gap);

            _checkBar = Rect("CheckinBar", chrome);
            _checkBar.anchorMin = _checkBar.anchorMax = Vector2.zero;
            // Whole strip ≤ avatar width (track + edit).
            _checkBar.sizeDelta = new Vector2(chipW, Mathf.Max(trackH, editHit));

            var toggle = ButtonRect("CheckinSwitch", _checkBar, "");
            toggle.onClick.RemoveAllListeners();
            var rt = (RectTransform)toggle.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(trackW, trackH);

            _checkTrack = ImageRect("Track", rt, Color.red, OverlaySprites.RoundedRect);
            _checkTrack.raycastTarget = false;
            _checkTrack.rectTransform.anchorMin = Vector2.zero;
            _checkTrack.rectTransform.anchorMax = Vector2.one;
            _checkTrack.rectTransform.offsetMin = Vector2.zero;
            _checkTrack.rectTransform.offsetMax = Vector2.zero;
            var rim = ImageRect("Rim", _checkTrack.transform, new Color(1f, 1f, 1f, 0.28f), OverlaySprites.RoundedRect);
            rim.raycastTarget = false;
            rim.rectTransform.anchorMin = Vector2.zero;
            rim.rectTransform.anchorMax = Vector2.one;
            rim.rectTransform.offsetMin = new Vector2(2f, 2f);
            rim.rectTransform.offsetMax = new Vector2(-2f, -2f);

            _checkKnob = ImageRect("Knob", _checkTrack.transform, Color.white, OverlaySprites.Circle);
            _checkKnob.raycastTarget = true;
            _checkKnob.rectTransform.sizeDelta = new Vector2(knob, knob);
            var knobEdge = ImageRect("KnobEdge", _checkKnob.transform, new Color(0f, 0f, 0f, 0.18f), OverlaySprites.Circle);
            knobEdge.raycastTarget = false;
            knobEdge.rectTransform.anchorMin = Vector2.zero;
            knobEdge.rectTransform.anchorMax = Vector2.one;
            knobEdge.rectTransform.offsetMin = new Vector2(-2f, -2f);
            knobEdge.rectTransform.offsetMax = new Vector2(2f, 2f);
            knobEdge.transform.SetAsFirstSibling();
            _checkMark = Label("Mark", _checkKnob.transform, "✓", 14);
            _checkMark.alignment = TextAnchor.MiddleCenter;
            _checkMark.raycastTarget = false;
            _checkMark.gameObject.SetActive(false);

            var drag = toggle.gameObject.AddComponent<CheckinSlideDrag>();
            drag.Bind(this, rt);
            OverlayHoverRelay.Bind(toggle.gameObject, () => _checkHover = true, () => _checkHover = false);
            OverlayHoverRelay.Bind(_checkKnob.gameObject, () => _checkHover = true, () => _checkHover = false);

            var edit = ButtonRect("Edit", _checkBar, "");
            var editRt = (RectTransform)edit.transform;
            editRt.anchorMin = editRt.anchorMax = new Vector2(1f, 0.5f);
            editRt.pivot = new Vector2(1f, 0.5f);
            editRt.anchoredPosition = Vector2.zero;
            editRt.sizeDelta = new Vector2(editHit, editHit);
            var pencil = Rect("Pencil", edit.transform);
            pencil.sizeDelta = new Vector2(14f, 14f);
            pencil.localRotation = Quaternion.Euler(0f, 0f, -45f);
            for (var i = 0; i < 4; i++)
            {
                var stroke = ImageRect("Stroke", pencil, Color.white);
                stroke.raycastTarget = false;
                stroke.rectTransform.sizeDelta = i < 2 ? new Vector2(1f, 10f) : new Vector2(4f, 1f);
                stroke.rectTransform.anchoredPosition = i < 2 ? new Vector2(i == 0 ? -2f : 2f, 0f)
                    : new Vector2(0f, i == 2 ? 4f : -4f);
            }
            edit.onClick.AddListener(OpenCheckinEdit);
            StyleButton(edit);

            var square = Resources.Load<Sprite>(ThemeSpriteResource);
            _checkHintBg = ImageRect("CheckinHint", chrome, new Color(0f, 0f, 0f, 0.55f), square);
            _checkHintBg.raycastTarget = false;
            _checkHintRoot = _checkHintBg.rectTransform;
            _checkHintRoot.anchorMin = _checkHintRoot.anchorMax = Vector2.zero;
            _checkHintRoot.pivot = new Vector2(0f, 1f);
            _checkHintRoot.sizeDelta = new Vector2(160f, 22f);
            _checkHint = Label("Text", _checkHintRoot, "", 12);
            _checkHint.alignment = TextAnchor.MiddleLeft;
            _checkHint.rectTransform.offsetMin = new Vector2(6f, 0f);
            _checkHint.rectTransform.offsetMax = new Vector2(-6f, 0f);
            _checkHintRoot.gameObject.SetActive(false);

            _checkFxRoot = Rect("CheckinFx", chrome);
            _checkFxRoot.anchorMin = _checkFxRoot.anchorMax = Vector2.zero;
            _checkFxRoot.sizeDelta = Vector2.zero;

            _checkEditor = ImageRect("CheckinEditor", transform, OverlaySkin.ThemeBackground(1)).rectTransform;
            _checkEditor.anchorMin = _checkEditor.anchorMax = Vector2.zero;
            _checkEditor.sizeDelta = new Vector2(240f, 40f);
            var inputImage = ImageRect("Input", _checkEditor, OverlaySkin.ThemeInputBackground(1));
            inputImage.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
            inputImage.rectTransform.sizeDelta = new Vector2(192f, 28f);
            var border = inputImage.gameObject.AddComponent<Outline>();
            border.effectDistance = Vector2.one;
            _checkInput = inputImage.gameObject.AddComponent<InputField>();
            _checkInput.targetGraphic = inputImage;
            _checkInput.textComponent = Label("Text", inputImage.transform, "", 13);
            _checkInput.textComponent.rectTransform.offsetMin = new Vector2(6f, 0f);
            _checkInput.textComponent.rectTransform.offsetMax = new Vector2(-6f, 0f);
            _checkInput.lineType = InputField.LineType.SingleLine;
            _checkInput.characterLimit = 100;
            var confirm = ButtonRect("Confirm", _checkEditor, "✓");
            ((RectTransform)confirm.transform).anchoredPosition = new Vector2(100f, 0f);
            ((RectTransform)confirm.transform).sizeDelta = new Vector2(28f, 28f);
            confirm.onClick.AddListener(SaveCheckinEdit);
            StyleButton(confirm);
            _checkEditor.gameObject.SetActive(false);
            _checkSlide = _view.Settings.Checkin.IsComplete(DateTime.Now) ? 1f : 0f;
        }

        internal bool CanDragCheckin()
        {
            if (_view == null || _view.Settings == null || !_view.Settings.ShowCheckin || IsEditingCheckin)
                return false;
            if (_view.Settings.TestMode) return true;
            return !_view.Settings.Checkin.IsComplete(DateTime.Now);
        }

        internal void BeginCheckinDrag()
        {
            _checkDragging = true;
            _view?.ClaimInteractionFocus();
        }

        internal void DragCheckinTo(float normalized)
        {
            if (!_checkDragging) return;
            _checkSlide = Mathf.Clamp01(normalized);
        }

        internal void EndCheckinDrag()
        {
            if (!_checkDragging) return;
            _checkDragging = false;
            if (_checkSlide >= 0.9f)
            {
                _checkSlide = 1f;
                if (!_view.Settings.TestMode)
                    _view.Settings.CompleteTodo(_view.Settings.Checkin);
                StartCoroutine(PlayCheckinFx());
            }
        }

        IEnumerator PlayCheckinFx()
        {
            _checkPunch = 1f;
            if (_checkFxRoot == null || _checkKnob == null) yield break;
            var origin = (Vector2)_checkKnob.rectTransform.position;
            for (var i = 0; i < 6; i++)
            {
                var spark = ImageRect("Spark", _checkFxRoot, new Color32(80, 220, 120, 255), OverlaySprites.Circle);
                spark.raycastTarget = false;
                spark.rectTransform.sizeDelta = new Vector2(6f, 6f);
                spark.rectTransform.position = origin;
                var dir = new Vector2(Mathf.Cos(i * 1.047f), Mathf.Sin(i * 1.047f) + 0.35f);
                StartCoroutine(AnimateSpark(spark, dir * 36f));
            }
            var t = 0f;
            while (t < 0.28f)
            {
                t += Time.unscaledDeltaTime;
                _checkPunch = Mathf.Lerp(1.35f, 1f, t / 0.28f);
                yield return null;
            }
            _checkPunch = 0f;
        }

        IEnumerator AnimateSpark(Image spark, Vector2 delta)
        {
            var start = (Vector2)spark.rectTransform.anchoredPosition;
            // Convert world kick into local delta roughly.
            var from = spark.rectTransform.position;
            var t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                var u = t / 0.35f;
                spark.rectTransform.position = from + (Vector3)(delta * u);
                var c = spark.color;
                c.a = 1f - u;
                spark.color = c;
                spark.rectTransform.localScale = Vector3.one * (1.2f - 0.6f * u);
                yield return null;
            }
            if (spark != null) Destroy(spark.gameObject);
        }

        void OpenCheckinEdit()
        {
            _checkInput.SetTextWithoutNotify(_view.Settings.Checkin.DisplayText);
            _checkEditor.gameObject.SetActive(true);
            _checkEditor.SetAsLastSibling();
            _view.ClaimInteractionFocus();
            _checkInput.Select();
            _checkInput.ActivateInputField();
        }

        public void CancelCheckinEdit()
        {
            if (_checkEditor != null) _checkEditor.gameObject.SetActive(false);
        }

        void SaveCheckinEdit()
        {
            if (!IsEditingCheckin) return;
            var value = _checkInput.text;
            CancelCheckinEdit();
            _view.Settings.EditTodo(_view.Settings.Checkin, value, OverlayUserSettings.TodoFrequency.Daily);
        }

        void UpdateCheckin()
        {
            if (_checkBar == null) return;
            var chip = _view.LocalChip;
            var show = chip != null && _view.Settings != null && _view.Settings.ShowCheckin;
            _checkBar.gameObject.SetActive(show);
            if (!show)
            {
                CancelCheckinEdit();
                if (_checkHintRoot != null) _checkHintRoot.gameObject.SetActive(false);
                return;
            }

            var chipW = _view.Config != null ? _view.Config.chipSize : 128f;
            const float trackH = 22f;
            const float knob = 20f;
            const float editHit = 24f;
            const float gap = 2f;
            var trackW = Mathf.Max(48f, chipW - editHit - gap);
            _checkBar.sizeDelta = new Vector2(chipW, Mathf.Max(trackH, editHit));
            var switchRt = (RectTransform)_checkBar.Find("CheckinSwitch");
            if (switchRt != null) switchRt.sizeDelta = new Vector2(trackW, trackH);

            var scale = _view.Settings.Scale;
            var pos = chip.FollowPosition + Vector2.up * (_view.Config.chipSize * 0.5f + _checkBar.sizeDelta.y * 0.5f + 6f) * scale;
            var bounds = FriendOverlayView.OverlayPixelSize;
            var halfW = _checkBar.sizeDelta.x * 0.5f * scale;
            pos.x = Mathf.Clamp(pos.x, halfW + 8f, bounds.x - halfW - 8f);
            pos.y = Mathf.Clamp(pos.y, 16f * scale, bounds.y - 16f * scale);
            _checkBar.anchoredPosition = pos;
            _checkBar.localScale = Vector3.one * scale;

            var done = !_view.Settings.TestMode && _view.Settings.Checkin.IsComplete(DateTime.Now);
            if (!_checkDragging)
            {
                var target = done ? 1f : 0f;
                // Test mode: after a successful slide, ease back so it can be tried again.
                _checkSlide = Mathf.MoveTowards(_checkSlide, target, Time.unscaledDeltaTime / 0.16f);
            }

            var green = new Color32(46, 168, 83, 255);
            _checkTrack.color = Color.Lerp(OverlaySkin.ThemeDanger(_view.Settings.SettingsTheme), green, _checkSlide);
            var travel = (trackW - knob) * 0.5f - 2f;
            _checkKnob.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, _checkSlide), 0f);
            var showMark = done || _checkSlide >= 0.9f;
            if (_checkMark != null)
            {
                _checkMark.gameObject.SetActive(showMark);
                _checkMark.color = Color.white;
                _checkMark.font = OverlaySprites.UiFont;
            }
            _checkKnob.color = showMark ? green : Color.white;
            var punch = _checkDragging ? 1.12f : (_checkPunch > 0f ? _checkPunch : 1f);
            _checkKnob.rectTransform.localScale = Vector3.one * punch;

            var showHint = _checkHover && !IsEditingCheckin && !_checkDragging;
            if (_checkHintRoot != null)
            {
                _checkHintRoot.gameObject.SetActive(showHint);
                if (showHint)
                {
                    var text = _view.Settings.Checkin.DisplayText;
                    if (text.Length > 16) text = text.Substring(0, 16) + "…";
                    _checkHint.text = text;
                    _checkHint.font = OverlaySprites.UiFont;
                    _checkHint.color = Color.white;
                    var preferred = Mathf.Clamp(_checkHint.preferredWidth + 12f, 48f, 220f);
                    _checkHintRoot.sizeDelta = new Vector2(preferred, 22f);
                    Vector2 mouse;
                    var window = _view.GetComponent<TransparentOverlayWindow>();
                    if (window == null || !window.TryGetPointerPosition(out mouse))
                        mouse = Input.mousePosition;
                    var tip = mouse + new Vector2(14f, -10f);
                    tip.x = Mathf.Clamp(tip.x, 8f, bounds.x - preferred - 8f);
                    tip.y = Mathf.Clamp(tip.y, 24f, bounds.y - 8f);
                    _checkHintRoot.anchoredPosition = tip;
                    _checkHintRoot.SetAsLastSibling();
                }
            }

            if (IsEditingCheckin)
                _checkEditor.anchoredPosition = new Vector2(Mathf.Clamp(pos.x, 124f, bounds.x - 124f),
                    Mathf.Clamp(pos.y + 38f * scale, 24f, bounds.y - 24f));
        }

        void PlaceCard()
        {
            if (_view.LocalChip == null) { Hide(); return; }
            var pos = _view.LocalChip.FollowPosition;
            var half = _card.sizeDelta * 0.5f;
            var gap = _view.Config.chipSize * _view.Settings.Scale * 0.5f + 8f;
            var y = pos.y + gap + half.y;
            var bounds = FriendOverlayView.OverlayPixelSize;
            if (y + half.y > bounds.y - 8f) y = pos.y - gap - half.y;
            _card.anchoredPosition = new Vector2(
                Mathf.Clamp(pos.x, half.x + 8f, Mathf.Max(half.x + 8f, bounds.x - half.x - 8f)),
                Mathf.Clamp(y, half.y + 8f, Mathf.Max(half.y + 8f, bounds.y - half.y - 8f)));
        }

        public void ApplyTheme()
        {
            var theme = _view.Settings.SettingsTheme;
            if (_checkBar != null)
            {
                var themeText = OverlaySkin.SettingsThemeText(theme);
                foreach (var label in _checkBar.GetComponentsInChildren<Text>(true)) label.color = themeText;
                var pencil = _checkBar.Find("Edit/Pencil");
                if (pencil != null)
                    foreach (var image in pencil.GetComponentsInChildren<Image>())
                        image.color = themeText;
                if (_checkHintBg != null) _checkHintBg.color = new Color(0f, 0f, 0f, 0.55f);
                if (_checkHint != null) _checkHint.color = Color.white;
                if (_checkMark != null)
                {
                    _checkMark.font = OverlaySprites.UiFont;
                    _checkMark.color = Color.white;
                }
                _checkEditor.GetComponent<Image>().color = OverlaySkin.ThemeBackground(theme);
                _checkInput.GetComponent<Image>().color = OverlaySkin.ThemeInputBackground(theme);
                _checkInput.GetComponent<Outline>().effectColor = OverlaySkin.ThemeDivider(theme);
                foreach (var label in _checkEditor.GetComponentsInChildren<Text>(true)) label.color = themeText;
                _checkInput.customCaretColor = true;
                _checkInput.caretColor = themeText;
            }
            _themeSprite = _themeSprite != null ? _themeSprite : Resources.Load<Sprite>(ThemeSpriteResource);
            _controlSprite = _controlSprite != null ? _controlSprite : Resources.Load<Sprite>(ControlSpriteResource);
            ApplySurface(_cardImage, OverlaySprites.RoundedRect, OverlaySkin.ThemeBackground(theme));
            ApplySurface(_headerImage, OverlaySprites.RoundedRect, OverlaySkin.ThemeHeader(theme));
            ApplySurface(_dividerImage, _themeSprite, OverlaySkin.ThemeDivider(theme));
            if (_closeIcon != null) _closeIcon.color = OverlaySkin.SettingsThemeText(theme);
            foreach (var text in _card.GetComponentsInChildren<Text>(true))
                text.color = OverlaySkin.SettingsThemeText(theme);
            foreach (var pair in _rows)
            {
                var item = _visible.Find(value => value.id == pair.Key);
                if (item != null && item.IsComplete(_date))
                {
                    pair.Value.GetComponentInChildren<Text>().color = OverlaySkin.ThemeMuted(theme);
                    pair.Value.transform.Find("Check").GetComponentInChildren<Text>().color = OverlaySkin.ThemeAccent(theme);
                }
            }
            foreach (var button in _card.GetComponentsInChildren<Button>(true)) StyleButton(button);
            if (_settingsPage == null) return;
            // Include the static page and inactive template, not just instantiated rows.
            // SettingsUi's generic image fallback must not turn the scroll area into a solid block.
            var square = Resources.Load<Sprite>("Overlay/UI/square_rect");
            foreach (var text in _settingsPage.GetComponentsInChildren<Text>(true))
            {
                text.font = OverlaySprites.UiFont;
                text.fontSize = text.transform.parent.name == "Add" ? 20 : 13;
                text.color = text.name == "Placeholder" ? OverlaySkin.ThemeMuted(theme)
                    : OverlaySkin.SettingsThemeText(theme);
            }
            foreach (var image in _settingsPage.GetComponentsInChildren<Image>(true))
            {
                var circular = image.name == "Add" || image.name == "Delete";
                var rounded = image.name == "Input" || image.name == "Frequency" ||
                              image.name == "Template" || image.name == "Dropdown List";
                image.sprite = circular
                    ? OverlaySprites.Circle
                    : rounded
                        ? OverlaySprites.RoundedRect
                        : square;
                image.type = circular ? Image.Type.Simple : Image.Type.Sliced;
                image.preserveAspect = circular;
                image.color = image.name == "Input" || image.name == "Frequency"
                    ? OverlaySkin.ThemeInputBackground(theme)
                    : image.name == "Template" || image.name == "Dropdown List"
                    ? OverlaySkin.ThemeBackground(theme) : Color.clear;
            }
            var addLabel = _settingsPage.Find("Add")?.GetComponentInChildren<Text>(true);
            if (addLabel != null) addLabel.color = OverlaySkin.ThemeAccent(theme);
            var deleteLabels = _settingsPage.GetComponentsInChildren<Text>(true);
            foreach (var text in deleteLabels)
                if (text.transform.parent.name == "Delete") text.color = OverlaySkin.ThemeDanger(theme);
            foreach (var input in _settingsPage.GetComponentsInChildren<InputField>(true))
            {
                input.transition = Selectable.Transition.None;
                input.customCaretColor = true;
                input.caretColor = OverlaySkin.SettingsThemeText(theme);
                var selection = OverlaySkin.ThemeAccent(theme);
                selection.a = 0.35f;
                input.selectionColor = selection;
            }
            foreach (var button in _settingsPage.GetComponentsInChildren<Button>(true)) StyleButton(button);
            foreach (var dropdown in _settingsPage.GetComponentsInChildren<Dropdown>(true))
            {
                dropdown.transition = Selectable.Transition.None;
                var image = dropdown.GetComponent<Image>();
                OverlayHoverRelay.Bind(dropdown.gameObject,
                    () => image.color = OverlaySkin.ThemeControl(_view.Settings.SettingsTheme),
                    () => image.color = OverlaySkin.ThemeInputBackground(_view.Settings.SettingsTheme));
                foreach (var toggle in dropdown.GetComponentsInChildren<Toggle>(true))
                {
                    toggle.transition = Selectable.Transition.ColorTint;
                    var colors = toggle.colors;
                    colors.normalColor = Color.clear;
                    colors.highlightedColor = OverlaySkin.ThemeHover(theme);
                    colors.selectedColor = OverlaySkin.ThemeHover(theme);
                    colors.pressedColor = OverlaySkin.ThemeHover(theme);
                    toggle.colors = colors;
                    // ColorTint multiplies this graphic, so keep its base color opaque.
                    if (toggle.targetGraphic != null) toggle.targetGraphic.color = Color.white;
                }
            }
        }

        void StyleButton(Button button)
        {
            button.transition = Selectable.Transition.None;
            var image = button.GetComponent<Image>();
            if (image == null) return;
            var circular = image.name == "Close" || image.name == "Add" || image.name == "Delete";
            image.sprite = circular ? OverlaySprites.Circle : _controlSprite;
            image.type = circular ? Image.Type.Simple : Image.Type.Sliced;
            image.preserveAspect = circular;
            image.color = Color.clear;
            OverlayHoverRelay.Bind(button.gameObject,
                () => image.color = OverlaySkin.ThemeHover(_view.Settings.SettingsTheme),
                () => image.color = Color.clear);
        }

        static void ApplySurface(Image image, Sprite sprite, Color color)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.color = color;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image ImageRect(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        static Text Label(string name, Transform parent, string value, int size)
        {
            var rt = Rect(name, parent);
            Stretch(rt);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = OverlaySprites.UiFont;
            text.fontSize = size;
            text.text = value;
            text.supportRichText = false;
            text.color = OverlaySkin.Text;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        static Button ButtonRect(string name, Transform parent, string value)
        {
            var image = ImageRect(name, parent, Color.clear);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var text = Label("Text", image.transform, value, 13);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        static Button IconButton(string name, Transform parent, Sprite sprite, out Image icon)
        {
            var image = ImageRect(name, parent, Color.clear, OverlaySprites.Circle);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            icon = ImageRect("Icon", image.transform, Color.white, sprite);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(16f, 16f);
            return button;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void PinTop(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta = new Vector2(0f, height);
        }

        static ScrollRect BuildScroll(Transform parent)
        {
            var image = ImageRect("List", parent, Color.clear);
            Stretch(image.rectTransform);
            var scroll = image.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            var viewport = Rect("Viewport", image.transform);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport);
            PinTop(content, 0f, 0f);
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

#if UNITY_EDITOR
        // Only used to author the static settings page; runtime clones the saved row template.
        public static void EditorBuildSettingsPage(Transform pages, Dropdown dropdownSource)
        {
            if (pages.Find("TodoPage") != null) return;
            var page = Rect("TodoPage", pages);
            Stretch(page);
            var add = ButtonRect("Add", page, "+");
            var addRt = (RectTransform)add.transform;
            addRt.anchorMin = addRt.anchorMax = new Vector2(1f, 1f);
            addRt.pivot = Vector2.one;
            addRt.anchoredPosition = new Vector2(-12f, -4f);
            addRt.sizeDelta = new Vector2(28f, 28f);
            var scroll = BuildScroll(page);
            ((RectTransform)scroll.transform).offsetMin = new Vector2(12f, 8f);
            ((RectTransform)scroll.transform).offsetMax = new Vector2(-12f, -36f);
            var row = Rect("RowTemplate", page);
            PinTop(row, 0f, 32f);
            var inputImage = ImageRect("Input", row, OverlaySkin.ThemeInputBackground(1));
            Stretch(inputImage.rectTransform);
            inputImage.rectTransform.offsetMax = new Vector2(-98f, 0f);
            var input = inputImage.gameObject.AddComponent<InputField>();
            input.targetGraphic = inputImage;
            input.textComponent = Label("Text", input.transform, "", 13);
            input.textComponent.rectTransform.offsetMin = new Vector2(6f, 0f);
            input.textComponent.rectTransform.offsetMax = new Vector2(-6f, 0f);
            var placeholder = Label("Placeholder", input.transform, "输入事项", 13);
            placeholder.rectTransform.offsetMin = new Vector2(6f, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-6f, 0f);
            input.placeholder = placeholder;
            input.characterLimit = 100;
            input.lineType = InputField.LineType.SingleLine;
            var frequency = Instantiate(dropdownSource, row, false);
            frequency.name = "Frequency";
            frequency.onValueChanged = new Dropdown.DropdownEvent();
            frequency.ClearOptions();
            frequency.AddOptions(new List<string> { "一次", "每日", "每周" });
            frequency.value = 0;
            var frequencyRt = (RectTransform)frequency.transform;
            frequencyRt.anchorMin = frequencyRt.anchorMax = new Vector2(1f, 0.5f);
            frequencyRt.pivot = new Vector2(1f, 0.5f);
            frequencyRt.anchoredPosition = new Vector2(-28f, 0f);
            frequencyRt.sizeDelta = new Vector2(66f, 28f);
            var delete = ButtonRect("Delete", row, "×");
            var deleteRt = (RectTransform)delete.transform;
            deleteRt.anchorMin = deleteRt.anchorMax = new Vector2(1f, 0.5f);
            deleteRt.pivot = new Vector2(1f, 0.5f);
            deleteRt.sizeDelta = new Vector2(24f, 28f);
            row.gameObject.SetActive(false);
            page.gameObject.SetActive(false);
        }
#endif
    }

    /// <summary>Drag the check-in knob across the track; click alone does not complete.</summary>
    sealed class CheckinSlideDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        OverlayTodoUi _owner;
        RectTransform _track;

        public void Bind(OverlayTodoUi owner, RectTransform track)
        {
            _owner = owner;
            _track = track;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_owner == null || !_owner.CanDragCheckin() || eventData == null) return;
            _owner.BeginCheckinDrag();
            Apply(eventData);
        }

        public void OnPointerUp(PointerEventData eventData) => _owner?.EndCheckinDrag();

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_owner == null || !_owner.CanDragCheckin()) return;
            _owner.BeginCheckinDrag();
        }

        public void OnDrag(PointerEventData eventData) => Apply(eventData);

        public void OnEndDrag(PointerEventData eventData) => _owner?.EndCheckinDrag();

        void Apply(PointerEventData eventData)
        {
            if (_owner == null || _track == null || eventData == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _track, eventData.position, eventData.pressEventCamera, out var local))
                return;
            var half = _track.rect.width * 0.5f;
            if (half <= 0.01f) return;
            _owner.DragCheckinTo(Mathf.InverseLerp(-half, half, local.x));
        }
    }
}
