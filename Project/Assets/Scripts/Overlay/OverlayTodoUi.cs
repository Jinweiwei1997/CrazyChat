using System;
using System.Collections.Generic;
using UnityEngine;
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
        float _nextRotate;
        int _previewIndex;
        FriendAvatarChip _previewChip;
        RectTransform _checkBar;
        Image _checkTrack;
        Image _checkKnob;
        Text _checkHint;
        RectTransform _checkEditor;
        InputField _checkInput;
        float _checkSlide;
        bool _checkHover;
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
            _previewIndex = Mathf.Clamp(_previewIndex, 0, Mathf.Max(0, _visible.Count - 1));
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
            var previews = _visible.FindAll(value => !value.IsCheckin);
            var item = previews.Count > 0 ? previews[_previewIndex % previews.Count] : null;
            _previewChip?.SetTodoPreview(item == null ? "" : Status(item) + " " + item.text);
        }

        void Update()
        {
            var now = DateTime.Now;
            if (_date.Date != now.Date || _date.AddHours(-6).Date != now.AddHours(-6).Date) Refresh();
            if (_previewChip != _view.LocalChip) RefreshPreview();
            if (Time.unscaledTime >= _nextRotate)
            {
                _nextRotate = Time.unscaledTime + Mathf.Max(0.1f, _view.Config.bubbleRotateSeconds);
                var previewCount = _visible.FindAll(item => !item.IsCheckin).Count;
                if (previewCount > 0) _previewIndex = (_previewIndex + 1) % previewCount;
                RefreshPreview();
            }
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
            _checkBar = Rect("CheckinBar", chrome);
            _checkBar.anchorMin = _checkBar.anchorMax = Vector2.zero;
            _checkBar.sizeDelta = new Vector2(72f, 28f);
            var toggle = ButtonRect("CheckinSwitch", _checkBar, "");
            var rt = (RectTransform)toggle.transform;
            rt.anchoredPosition = new Vector2(-16f, 0f);
            rt.sizeDelta = new Vector2(40f, 28f);
            _checkTrack = ImageRect("Track", rt, Color.red, OverlaySprites.RoundedRect);
            _checkTrack.raycastTarget = false;
            _checkTrack.rectTransform.sizeDelta = new Vector2(36f, 20f);
            _checkKnob = ImageRect("Knob", _checkTrack.transform, Color.white, OverlaySprites.Circle);
            _checkKnob.raycastTarget = false;
            _checkKnob.rectTransform.sizeDelta = new Vector2(16f, 16f);
            toggle.onClick.AddListener(() => _view.Settings.CompleteTodo(_view.Settings.Checkin));
            OverlayHoverRelay.Bind(toggle.gameObject, () => _checkHover = true, () => _checkHover = false);
            var edit = ButtonRect("Edit", _checkBar, "");
            ((RectTransform)edit.transform).anchoredPosition = new Vector2(22f, 0f);
            ((RectTransform)edit.transform).sizeDelta = new Vector2(28f, 28f);
            // A 16px outline pencil, matching the existing Codicon tool buttons without font glyph fallback.
            var pencil = Rect("Pencil", edit.transform);
            pencil.sizeDelta = new Vector2(16f, 16f);
            pencil.localRotation = Quaternion.Euler(0f, 0f, -45f);
            for (var i = 0; i < 4; i++)
            {
                var stroke = ImageRect("Stroke", pencil, Color.white);
                stroke.raycastTarget = false;
                stroke.rectTransform.sizeDelta = i < 2 ? new Vector2(1f, 11f) : new Vector2(5f, 1f);
                stroke.rectTransform.anchoredPosition = i < 2 ? new Vector2(i == 0 ? -2f : 2f, 0f)
                    : new Vector2(0f, i == 2 ? 5f : -5f);
            }
            edit.onClick.AddListener(OpenCheckinEdit);
            StyleButton(edit);
            var hint = ImageRect("Hint", _checkBar, OverlaySkin.ThemeBackground(1), OverlaySprites.RoundedRect);
            hint.raycastTarget = false;
            hint.rectTransform.anchoredPosition = new Vector2(0f, 30f);
            hint.rectTransform.sizeDelta = new Vector2(180f, 26f);
            _checkHint = Label("Text", hint.transform, "", 13);
            _checkHint.alignment = TextAnchor.MiddleCenter;

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

        void OpenCheckinEdit()
        {
            _checkInput.SetTextWithoutNotify(_view.Settings.Checkin.DisplayText);
            _checkEditor.gameObject.SetActive(true);
            _checkEditor.SetAsLastSibling();
            _view.GetComponent<TransparentOverlayWindow>()?.FocusForTextInput(forceSteal: true);
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
            _checkBar.gameObject.SetActive(chip != null);
            if (chip == null) { CancelCheckinEdit(); return; }
            var scale = _view.Settings.Scale;
            var pos = chip.FollowPosition + Vector2.up * (_view.Config.chipSize * 0.5f + _checkBar.sizeDelta.y * 0.5f) * scale;
            var bounds = FriendOverlayView.OverlayPixelSize;
            pos.x = Mathf.Clamp(pos.x, 38f * scale, bounds.x - 38f * scale);
            pos.y = Mathf.Clamp(pos.y, 16f * scale, bounds.y - 16f * scale);
            _checkBar.anchoredPosition = pos;
            _checkBar.localScale = Vector3.one * scale;
            var done = _view.Settings.Checkin.IsComplete(DateTime.Now);
            _checkSlide = Mathf.MoveTowards(_checkSlide, done ? 1f : 0f, Time.unscaledDeltaTime / 0.18f);
            _checkTrack.color = Color.Lerp(OverlaySkin.ThemeDanger(_view.Settings.SettingsTheme), new Color32(46, 168, 83, 255), _checkSlide);
            _checkKnob.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-8f, 8f, _checkSlide), 0f);
            _checkHint.transform.parent.gameObject.SetActive(_checkHover && !IsEditingCheckin);
            ((RectTransform)_checkHint.transform.parent).anchoredPosition = new Vector2(
                (Mathf.Clamp(pos.x, 92f * scale, bounds.x - 92f * scale) - pos.x) / scale,
                pos.y + 44f * scale > bounds.y ? -30f : 30f);
            var text = _view.Settings.Checkin.DisplayText;
            _checkHint.text = text.Length > 12 ? text.Substring(0, 12) + "…" : text;
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
                foreach (var label in _checkBar.GetComponentsInChildren<Text>(true)) label.color = OverlaySkin.SettingsThemeText(theme);
                foreach (var image in _checkBar.Find("Edit/Pencil").GetComponentsInChildren<Image>())
                    image.color = OverlaySkin.SettingsThemeText(theme);
                _checkHint.transform.parent.GetComponent<Image>().color = OverlaySkin.ThemeBackground(theme);
                _checkEditor.GetComponent<Image>().color = OverlaySkin.ThemeBackground(theme);
                _checkInput.GetComponent<Image>().color = OverlaySkin.ThemeInputBackground(theme);
                _checkInput.GetComponent<Outline>().effectColor = OverlaySkin.ThemeDivider(theme);
                foreach (var label in _checkEditor.GetComponentsInChildren<Text>(true)) label.color = OverlaySkin.SettingsThemeText(theme);
                _checkInput.customCaretColor = true;
                _checkInput.caretColor = OverlaySkin.SettingsThemeText(theme);
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
}
