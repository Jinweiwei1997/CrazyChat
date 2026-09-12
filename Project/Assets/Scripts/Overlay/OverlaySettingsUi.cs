using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class OverlaySettingsUi : MonoBehaviour
    {
        const string PrefabResource = "Prefab/UI/SettingsMenu";
        const string DefaultPage = "GamePage";
        const float CardWidth = 300f * OverlaySkin.SettingsChatWidthScale * 1.2f;
        const float CardHeight = 422f;
        const float HeaderHeight = 36f;
        const float TabBarHeight = 32f;
        const float RowHeight = 26f;
        const float ActionRowHeight = 28f;
        const float GearSize = 32f;
        const float SettingsIconSize = 24f;
        const float CloseSize = 28f;
        const float CardVisualScale = 1f;
        const string ThemeSpriteResource = "Overlay/UI/square_rect";
        const string ControlSpriteResource = "Overlay/UI/control_rect";
        const string SettingsIconResource = "Overlay/UI/codicon_settings";
        const string CloseIconResource = "Overlay/UI/codicon_close";

        FriendOverlayView _view;
        [SerializeField] GameObject _panel;
        [SerializeField] RectTransform _buttonRt;
        [SerializeField] RectTransform _cardRt;
        [SerializeField] RectTransform _tabBar;
        [SerializeField] RectTransform _pagesRoot;
        [SerializeField] Text _scaleText;
        [SerializeField] Text _topmostText;
        [SerializeField] Text _testModeText;
        [SerializeField] Text _flipText;
        [SerializeField] Text _autoStartText;
        [SerializeField] Text _inputIconsText;
        [SerializeField] Text _avatarStatusText;
        [SerializeField] Dropdown _displayDropdown;
        Dropdown _themeDropdown;
        Slider _hueSlider;
        Slider _intensitySlider;
        Image _hueSwatch;
        Image _hueHandle;
        Image _intensityHandle;
        Text _intensityValue;
        readonly List<Image> _themeImages = new List<Image>();
        readonly List<Text> _themeLabels = new List<Text>();
        Sprite _themeSprite;
        Sprite _controlSprite;
        Sprite _settingsIcon;
        Sprite _closeIcon;
        Text _avatarSetupStatus;
        Image _slotAImage;
        Image _slotBImage;
        GameObject _avatarSetup;
        OverlayAvatarCropUi _cropUi;
        Coroutine _pickRoutine;
        [SerializeField] Image _buttonImage;
        [SerializeField] Image _cardImage;
        [SerializeField] Image _closeImage;
        string _page = DefaultPage;

        public static OverlaySettingsUi Create(Transform chrome, Transform modal, FriendOverlayView view)
        {
            var prefab = Resources.Load<GameObject>(PrefabResource);
            if (prefab == null)
            {
                Debug.LogError("[Overlay] 缺少设置界面 Prefab: Resources/" + PrefabResource);
                return null;
            }

            var root = Instantiate(prefab, chrome, false);
            root.name = "SettingsUi";
            Stretch((RectTransform)root.transform);
            var ui = root.GetComponent<OverlaySettingsUi>();
            if (ui == null)
            {
                Debug.LogError("[Overlay] 设置界面 Prefab 缺少 OverlaySettingsUi。");
                Destroy(root);
                return null;
            }

            if (ui._panel != null)
            {
                ui._panel.transform.SetParent(modal != null ? modal : ui.transform, false);
                Stretch((RectTransform)ui._panel.transform);
            }

            ui._view = view;
            ApplyFonts(ui._buttonRt);
            ApplyFonts(ui._cardRt);
            ui.Bind();
            ui._cardRt.localScale = new Vector3(CardVisualScale, CardVisualScale, 1f);
            ui.RestoreTextTabs();
            ui.ApplyTypography();
            ui.ApplyIcons();
            ui.ApplyTheme();
            ui.ShowPage(DefaultPage);
            ui.Hide();
            return ui;
        }

#if UNITY_EDITOR
        public void EditorPopulate()
        {
            Build(null);
        }

        public void EditorFitContent()
        {
            ApplyFixedCardSize();
        }

        public Transform EditorEnsureThemeRow()
        {
            return FindNode(_cardRt, "Pages/DisplayPage/ThemeRow");
        }

        public void EditorEnsureColorRows()
        {
        }

        public Transform EditorEnsureBackdrop()
        {
            var existing = FindNode(_panel != null ? _panel.transform : null, "Backdrop");
            if (existing != null)
            {
                existing.SetAsFirstSibling();
            }

            return existing;
        }
#endif

        static void ApplyFonts(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var labels = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].font = OverlaySprites.UiFont;
            }
        }

        int ThemeId => _view != null && _view.Settings != null ? _view.Settings.SettingsTheme : 1;
        Color ThemeBackground => OverlaySkin.ThemeBackground(ThemeId);
        Color ThemeHeader => OverlaySkin.ThemeHeader(ThemeId);
        Color ThemeSection => OverlaySkin.ThemeSection(ThemeId);
        Color ThemeControl => OverlaySkin.ThemeControl(ThemeId);
        Color ThemeAccent => OverlaySkin.ThemeAccent(ThemeId);
        Color ThemeText => OverlaySkin.SettingsThemeText(ThemeId);
        Color ThemeMuted => OverlaySkin.ThemeMuted(ThemeId);

        void Bind()
        {
            if (_themeSprite == null)
            {
                _themeSprite = Resources.Load<Sprite>(ThemeSpriteResource);
                _controlSprite = Resources.Load<Sprite>(ControlSpriteResource);
            }

            BindClick(_buttonImage, TogglePanel);
            BindClick(FindNode(_panel != null ? _panel.transform : null, "Backdrop"), Hide);
            BindClick(_closeImage, Hide);
            BindTabs();
            BindAvatarSetup();
            BindClick(FindNode(_cardRt, "Pages/GamePage/ScaleRow/Minus"), () =>
            {
                _view.Settings.AddScale(-0.1f);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            BindClick(FindNode(_cardRt, "Pages/GamePage/ScaleRow/Plus"), () =>
            {
                _view.Settings.AddScale(0.1f);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            BindClick(FindNode(_cardRt, "Pages/GamePage/TestModeRow/Toggle"), () =>
            {
                _view.Settings.SetTestMode(!_view.Settings.TestMode);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            BindClick(FindNode(_cardRt, "Pages/GamePage/FlipHorizontalRow/Toggle"), () =>
            {
                _view.Settings.SetFlipHorizontal(!_view.Settings.FlipHorizontal);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            BindClick(FindNode(_cardRt, "Pages/GamePage/InputIconsRow/Toggle"), () =>
            {
                _view.Settings.SetShowInputIcons(!_view.Settings.ShowInputIcons);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            BindAppearanceControls();
            BindClick(FindNode(_cardRt, "Pages/GamePage/ResetLayoutRow"), () => _view.ResetVisibleToDefault());
            BindClick(FindNode(_cardRt, "Pages/GamePage/ResetScaleRow"), () =>
            {
                _view.ResetScale();
                RefreshLabels();
            });
            BindClick(FindNode(_cardRt, "Pages/SystemPage/AlwaysOnTopRow/Toggle"), () =>
            {
                _view.Settings.SetAlwaysOnTop(!_view.Settings.AlwaysOnTop);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            BindClick(FindNode(_cardRt, "Pages/SystemPage/AutoStartRow/Toggle"), () =>
            {
                _view.Settings.SetAutoStart(!_view.Settings.AutoStart);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            BindDisplayDropdown();
            BindClick(FindNode(_cardRt, "Pages/SystemPage/QuitGameRow"), QuitGame);
            BindHoverStyles();
        }

        void BindDisplayDropdown()
        {
            if (_displayDropdown == null)
            {
                _displayDropdown = FindNode(_cardRt, "Pages/SystemPage/DisplayRow/DisplayDropdown")
                    ?.GetComponent<Dropdown>();
            }
            if (_displayDropdown == null)
            {
                return;
            }

            _displayDropdown.transition = Selectable.Transition.None;
            _displayDropdown.onValueChanged.RemoveAllListeners();
            _displayDropdown.onValueChanged.AddListener(index =>
            {
                if (_view == null || _view.Settings == null)
                {
                    return;
                }

                var count = TransparentOverlayWindow.GetAvailableDisplayCount();
                _view.Settings.SetTargetDisplayIndex(Mathf.Clamp(index, 0, Mathf.Max(0, count - 1)));
                _view.ApplyUserSettings();
                RefreshDisplayOptions();
            });
            RefreshDisplayOptions();
        }

        void RefreshDisplayOptions()
        {
            if (_displayDropdown == null)
            {
                return;
            }

            var count = TransparentOverlayWindow.GetAvailableDisplayCount();
            var options = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                options.Add((i + 1).ToString());
            }

            _displayDropdown.ClearOptions();
            _displayDropdown.AddOptions(options);
            var saved = _view != null && _view.Settings != null ? _view.Settings.TargetDisplayIndex : 0;
            var selected = Mathf.Clamp(saved, 0, Mathf.Max(0, count - 1));
            if (_view != null && _view.Settings != null && selected != saved)
            {
                _view.Settings.SetTargetDisplayIndex(selected);
            }
            _displayDropdown.SetValueWithoutNotify(selected);
            _displayDropdown.RefreshShownValue();
        }

        void BindHoverStyles()
        {
            BindHover(_buttonImage, () => Color.clear);
            BindHover(_closeImage, () => Color.clear);
            if (_cardRt == null)
            {
                return;
            }

            var images = _cardRt.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                var image = images[i];
                var name = image.gameObject.name;
                if (name.EndsWith("Tab"))
                {
                    BindHover(image, () => TabRestColor(image.transform));
                }
                else if (name == "Toggle")
                {
                    BindHover(image, () => ToggleRestColor(image));
                }
                else if (name == "DisplayDropdown" || name == "ThemeDropdown")
                {
                    BindHover(image, () => OverlaySkin.ThemeInputBackground(ThemeId));
                }
                else if (name == "Minus" || name == "Plus" || name == "Reset" || name == "Confirm" ||
                         name == "Cancel" || name == "Shrink" || name == "Grow" || name == "Clear" ||
                         name.EndsWith("Row") && image.GetComponent<Button>() != null)
                {
                    BindHover(image, () => Color.clear);
                }
            }
        }

        void BindHover(Image image, System.Func<Color> restColor)
        {
            if (image == null)
            {
                return;
            }

            OverlayHoverRelay.Bind(
                image.gameObject,
                () => image.color = OverlaySkin.ThemeHover(ThemeId),
                () => image.color = restColor != null ? restColor() : Color.clear);
        }

        Color TabRestColor(Transform tab)
        {
            var selected = tab != null && TabToPage(tab.name) == _page;
            return selected ? WithAlpha(ThemeAccent, 0.3f) : Color.clear;
        }

        Color ToggleRestColor(Image image)
        {
            var label = image != null ? image.GetComponentInChildren<Text>(true) : null;
            return label != null && label.text == "开" ? WithAlpha(ThemeAccent, 0.3f) : Color.clear;
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        void BindTabs()
        {
            if (_tabBar == null)
            {
                return;
            }

            for (var i = 0; i < _tabBar.childCount; i++)
            {
                BindTab(_tabBar.GetChild(i));
            }
        }

        void BindTab(Transform tab)
        {
            if (tab == null || tab.name == "Divider")
            {
                return;
            }

            var pageName = TabToPage(tab.name);
            BindClick(tab, () => ShowPage(pageName));
        }

        static string TabToPage(string tabName)
        {
            return tabName.EndsWith("Tab")
                ? tabName.Substring(0, tabName.Length - 3) + "Page"
                : tabName + "Page";
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

        void Build(Transform modal)
        {
            _themeSprite = Resources.Load<Sprite>(ThemeSpriteResource);
            _controlSprite = Resources.Load<Sprite>(ControlSpriteResource);
            _buttonImage = CreateImage("SettingsButton", transform, Color.clear, _controlSprite);
            _buttonImage.raycastTarget = true;
            _buttonRt = _buttonImage.rectTransform;
            _buttonRt.anchorMin = new Vector2(0f, 0f);
            _buttonRt.anchorMax = new Vector2(0f, 0f);
            _buttonRt.pivot = new Vector2(0.5f, 0.5f);
            _buttonRt.sizeDelta = new Vector2(GearSize, GearSize);
            CreateIconPlaceholder(_buttonRt, SettingsIconSize);

            _panel = new GameObject("SettingsPanel", typeof(RectTransform));
            _panel.transform.SetParent(modal != null ? modal : transform, false);
            Stretch((RectTransform)_panel.transform);
            _panel.SetActive(false);

            var backdrop = CreateImage("Backdrop", _panel.transform, Color.clear, OverlaySprites.RoundedRect);
            backdrop.raycastTarget = true;
            Stretch(backdrop.rectTransform);

            _cardImage = CreateImage("Background", _panel.transform, OverlaySkin.ThemeBackground(1), OverlaySprites.RoundedRect);
            _cardImage.raycastTarget = true;
            _cardRt = _cardImage.rectTransform;
            _cardRt.anchorMin = new Vector2(0f, 0f);
            _cardRt.anchorMax = new Vector2(0f, 0f);
            _cardRt.pivot = new Vector2(0.5f, 0.5f);
            _cardRt.sizeDelta = new Vector2(CardWidth, CardHeight);
            _cardRt.localScale = new Vector3(CardVisualScale, CardVisualScale, 1f);

            BuildHeader(_cardRt);
            BuildTabBar(_cardRt);
            BuildPages(_cardRt);
            ShowPage(DefaultPage);
        }

        void BuildHeader(RectTransform parent)
        {
            var header = CreateImage("Header", parent, OverlaySkin.ThemeHeader(1), _themeSprite);
            header.raycastTarget = false;
            var headerRt = header.rectTransform;
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0f, HeaderHeight);

            var title = PlaceLabel(headerRt, "设置", 14, OverlaySkin.SettingsText, Vector2.zero, new Vector2(160f, 24f));
            title.gameObject.name = "Title";
            title.alignment = TextAnchor.MiddleLeft;
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(12f, 0f);

            _closeImage = CreateImage("Close", headerRt, Color.clear, _controlSprite);
            _closeImage.raycastTarget = true;
            var closeRt = _closeImage.rectTransform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-8f, 0f);
            closeRt.sizeDelta = new Vector2(CloseSize, CloseSize);
            CreateIconPlaceholder(closeRt, 16f);
            _closeImage.gameObject.AddComponent<Button>();
            CreateDivider(headerRt, "Divider", true);
        }

        void BuildTabBar(RectTransform parent)
        {
            var tabBar = CreateImage("TabBar", parent, OverlaySkin.ThemeSection(1), _themeSprite);
            tabBar.raycastTarget = false;
            _tabBar = tabBar.rectTransform;
            _tabBar.anchorMin = new Vector2(0f, 1f);
            _tabBar.anchorMax = new Vector2(1f, 1f);
            _tabBar.pivot = new Vector2(0.5f, 1f);
            _tabBar.anchoredPosition = new Vector2(0f, -HeaderHeight);
            _tabBar.sizeDelta = new Vector2(0f, TabBarHeight);
            var layout = _tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 2, 2);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            AddTabButton(_tabBar, "GameTab", "游戏");
            AddTabButton(_tabBar, "DisplayTab", "显示");
            AddTabButton(_tabBar, "DynamicTab", "动态");
            AddTabButton(_tabBar, "SystemTab", "系统");
            CreateDivider(_tabBar, "Divider", true);
        }

        void BuildPages(RectTransform parent)
        {
            var pages = CreateEmpty("Pages", parent);
            _pagesRoot = (RectTransform)pages.transform;
            _pagesRoot.anchorMin = Vector2.zero;
            _pagesRoot.anchorMax = Vector2.one;
            _pagesRoot.offsetMin = new Vector2(0f, 8f);
            _pagesRoot.offsetMax = new Vector2(0f, -(HeaderHeight + TabBarHeight));

            var gamePage = CreatePage(_pagesRoot, "GamePage");
            _scaleText = AddScaleRow(gamePage);
            _testModeText = AddToggleRow(gamePage, "TestModeRow", "测试模式");
            _flipText = AddToggleRow(gamePage, "FlipHorizontalRow", "水平翻转");
            _inputIconsText = AddToggleRow(gamePage, "InputIconsRow", "按键图标");
            AddActionRow(gamePage, "ResetLayoutRow", "复位头像位置");
            AddActionRow(gamePage, "ResetScaleRow", "复位缩放");

            var displayPage = CreatePage(_pagesRoot, "DisplayPage");
            displayPage.gameObject.SetActive(false);
            BuildThemeSelector(displayPage);
            BuildColorSelector(displayPage);

            var dynamicPage = CreatePage(_pagesRoot, "DynamicPage");
            dynamicPage.gameObject.SetActive(false);
            var dynamicHost = CreateEmpty("DynamicHost", dynamicPage);
            var dynamicHostLayout = dynamicHost.AddComponent<LayoutElement>();
            dynamicHostLayout.minHeight = 300f;
            dynamicHostLayout.preferredHeight = 300f;
            dynamicHostLayout.flexibleHeight = 1f;
            dynamicHostLayout.flexibleWidth = 1f;
            BuildAvatarSetup(dynamicHost.transform);
            OverlayAvatarCropUi.CreateEmbedded(dynamicHost.transform);

            var systemPage = CreatePage(_pagesRoot, "SystemPage");
            systemPage.gameObject.SetActive(false);
            _topmostText = AddToggleRow(systemPage, "AlwaysOnTopRow", "始终置顶");
            _autoStartText = AddToggleRow(systemPage, "AutoStartRow", "开机自启");
            _displayDropdown = AddDisplayDropdownRow(systemPage);
            AddActionRow(systemPage, "QuitGameRow", "退出游戏", danger: true);
        }

        void BuildThemeSelector(Transform displayPage)
        {
            var row = CreateEmpty("ThemeRow", displayPage);
            var rowRt = (RectTransform)row.transform;
            var layout = row.AddComponent<LayoutElement>();
            layout.minHeight = 72f;
            layout.preferredHeight = 72f;
            layout.flexibleHeight = 0f;

            var sectionTitle = PlaceLabel(rowRt, "外观", 12, OverlaySkin.ThemeMuted(1),
                new Vector2(0f, 28f), new Vector2(220f, 18f));
            sectionTitle.gameObject.name = "SectionTitle";
            sectionTitle.alignment = TextAnchor.MiddleLeft;

            var card = CreateImage("ThemeCard", rowRt, ThemeControl, _controlSprite);
            card.raycastTarget = false;
            SetCenteredRect(card.rectTransform, new Vector2(0f, -8f), new Vector2(220f, 50f));

            AddAppearanceText(card.rectTransform, "ThemeTitle", "主题",
                new Vector2(-51f, 8f), 12, false, 102f);
            AddAppearanceText(card.rectTransform, "ThemeHint", "选择浅色或深色主题",
                new Vector2(-51f, -10f), 10, true, 102f);

            _themeDropdown = CreateCompactDropdown(
                card.rectTransform, "ThemeDropdown", 76f, "Light", "Dark");
            SetCenteredRect(_themeDropdown.GetComponent<RectTransform>(),
                new Vector2(68f, 7f), new Vector2(76f, 24f));
        }

        void BuildColorSelector(Transform displayPage)
        {
            var row = CreateEmpty("ColorRow", displayPage);
            var rowRt = (RectTransform)row.transform;
            var layout = row.AddComponent<LayoutElement>();
            layout.minHeight = 136f;
            layout.preferredHeight = 136f;
            layout.flexibleHeight = 0f;

            var title = PlaceLabel(rowRt, "颜色", 12, OverlaySkin.ThemeMuted(1),
                new Vector2(0f, 58f), new Vector2(220f, 18f));
            title.gameObject.name = "SectionTitle";
            title.alignment = TextAnchor.MiddleLeft;

#if UNITY_EDITOR
            var circle = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
#else
            var circle = OverlaySprites.Circle;
#endif

            var card = CreateImage("ColorCard", rowRt, ThemeControl, _controlSprite);
            card.raycastTarget = false;
            var cardRt = card.rectTransform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = new Vector2(0f, -5f);
            cardRt.sizeDelta = new Vector2(220f, 100f);

            AddAppearanceText(cardRt, "HueTitle", "色相", new Vector2(-46f, 26f), 12, false, 112f);
            AddAppearanceText(cardRt, "HueHint", "选择界面染色色相", new Vector2(-46f, 10f), 10, true, 112f);
            _hueSlider = CreateAppearanceSlider(cardRt, "Hue", new Vector2(32f, 26f), 93f, circle);
            _hueSwatch = CreateImage("HueSwatch", cardRt,
                OverlaySkin.ColorFromHue(
                    _view?.Settings?.ThemeHue ?? 0.95f,
                    _view?.Settings?.ThemeIntensity ?? 0.24f), circle);
            _hueSwatch.type = Image.Type.Simple;
            _hueSwatch.preserveAspect = true;
            _hueSwatch.raycastTarget = false;
            SetCenteredRect(_hueSwatch.rectTransform, new Vector2(99f, 26f), new Vector2(20f, 20f));

            AddAppearanceDivider(cardRt, 0f);

            AddAppearanceText(cardRt, "IntensityTitle", "强度", new Vector2(-46f, -24f), 12, false, 112f);
            AddAppearanceText(cardRt, "IntensityHint", "控制染色应用强度", new Vector2(-46f, -40f), 10, true, 112f);
            _intensitySlider = CreateAppearanceSlider(cardRt, "Intensity", new Vector2(32f, -24f), 93f, circle);
            _intensityValue = PlaceLabel(cardRt, "24%", 12, OverlaySkin.SettingsText,
                new Vector2(97f, -24f), new Vector2(24f, 20f));
            _intensityValue.gameObject.name = "IntensityValue";
            _intensityValue.alignment = TextAnchor.MiddleRight;
        }

        Slider CreateAppearanceSlider(Transform parent, string name, Vector2 position, float width, Sprite circle)
        {
            var track = CreateImage(name + "Track", parent, OverlaySkin.ThemeDivider(1), _controlSprite);
            track.raycastTarget = true;
            SetCenteredRect(track.rectTransform, position, new Vector2(width, 6f));

            var handle = CreateImage(name + "Handle", track.rectTransform,
                OverlaySkin.ColorFromHue(
                    _view?.Settings?.ThemeHue ?? 0.95f,
                    _view?.Settings?.ThemeIntensity ?? 0.24f), circle);
            handle.type = Image.Type.Simple;
            handle.preserveAspect = true;
            handle.raycastTarget = true;
            var handleRt = handle.rectTransform;
            handleRt.anchorMin = handleRt.anchorMax = new Vector2(0f, 0.5f);
            handleRt.sizeDelta = new Vector2(16f, 16f);

            var slider = track.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handle;
            slider.handleRect = handleRt;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.transition = Selectable.Transition.None;
            return slider;
        }

        void AddAppearanceText(
            Transform parent, string name, string text, Vector2 position, int size, bool muted,
            float width = 150f)
        {
            var label = PlaceLabel(parent, text, size,
                muted ? OverlaySkin.ThemeMuted(1) : OverlaySkin.SettingsThemeText(1),
                position, new Vector2(width, 20f));
            label.gameObject.name = name;
            label.alignment = TextAnchor.MiddleLeft;
        }

        void AddAppearanceDivider(Transform parent, float y)
        {
            var divider = CreateImage("Divider", parent, OverlaySkin.ThemeDivider(1), _themeSprite);
            divider.raycastTarget = false;
            SetCenteredRect(divider.rectTransform, new Vector2(0f, y), new Vector2(204f, 1f));
        }

        static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        void BindAppearanceControls()
        {
            _themeDropdown = FindNode(_cardRt, "Pages/DisplayPage/ThemeRow/ThemeCard/ThemeDropdown")
                ?.GetComponent<Dropdown>();
            if (_themeDropdown != null)
            {
                _themeDropdown.onValueChanged.RemoveAllListeners();
                _themeDropdown.onValueChanged.AddListener(index => SelectAppearanceTheme(index == 0 ? 2 : 1));
            }

            var colorRow = FindNode(_cardRt, "Pages/DisplayPage/ColorRow");
            var card = colorRow?.Find("ColorCard");
            _hueSlider = card?.Find("HueTrack")?.GetComponent<Slider>();
            _intensitySlider = card?.Find("IntensityTrack")?.GetComponent<Slider>();
            _hueSwatch = card?.Find("HueSwatch")?.GetComponent<Image>();
            _hueHandle = card?.Find("HueTrack/HueHandle")?.GetComponent<Image>();
            _intensityHandle = card?.Find("IntensityTrack/IntensityHandle")?.GetComponent<Image>();
            _intensityValue = card?.Find("IntensityValue")?.GetComponent<Text>();

            if (_hueSlider != null)
            {
                _hueSlider.onValueChanged.RemoveAllListeners();
                _hueSlider.onValueChanged.AddListener(value =>
                {
                    if (_view == null || _view.Settings == null)
                    {
                        return;
                    }
                    _view.Settings.SetThemeHue(value);
                    _view.ApplyUserSettings();
                    RefreshLabels();
                });
            }
            if (_intensitySlider != null)
            {
                _intensitySlider.onValueChanged.RemoveAllListeners();
                _intensitySlider.onValueChanged.AddListener(value =>
                {
                    if (_view == null || _view.Settings == null)
                    {
                        return;
                    }
                    _view.Settings.SetThemeIntensity(value);
                    _view.ApplyUserSettings();
                    RefreshLabels();
                });
            }
        }

        void SelectAppearanceTheme(int theme)
        {
            if (_view == null || _view.Settings == null)
            {
                return;
            }

            _view.Settings.SetSettingsTheme(theme);
            _view.ApplyUserSettings();
            RefreshLabels();
        }

        void RefreshAppearanceControls()
        {
            if (_view == null || _view.Settings == null)
            {
                return;
            }

            if (_themeDropdown != null)
            {
                _themeDropdown.SetValueWithoutNotify(_view.Settings.SettingsTheme == 2 ? 0 : 1);
                _themeDropdown.RefreshShownValue();
            }

            if (_hueSlider != null)
            {
                _hueSlider.SetValueWithoutNotify(_view.Settings.ThemeHue);
            }
            if (_intensitySlider != null)
            {
                _intensitySlider.SetValueWithoutNotify(_view.Settings.ThemeIntensity);
            }

            var tint = OverlaySkin.ColorFromHue(
                _view.Settings.ThemeHue, _view.Settings.ThemeIntensity);
            if (_hueSwatch != null)
            {
                _hueSwatch.color = tint;
            }
            if (_hueHandle != null)
            {
                _hueHandle.color = tint;
            }
            if (_intensityHandle != null)
            {
                _intensityHandle.color = tint;
            }
            if (_intensityValue != null)
            {
                _intensityValue.text = Mathf.RoundToInt(_view.Settings.ThemeIntensity * 100f) + "%";
            }
        }

        void BuildAvatarSetup(Transform host)
        {
            _avatarSetup = new GameObject("AvatarSetup", typeof(RectTransform));
            _avatarSetup.transform.SetParent(host, false);
            Stretch((RectTransform)_avatarSetup.transform);
            var cardRt = (RectTransform)_avatarSetup.transform;

            var title = PlaceLabel(cardRt, "A/B 动态形象", 16, OverlaySkin.Text, new Vector2(0f, 132f), new Vector2(220f, 28f));
            title.gameObject.name = "Title";
            var status = PlaceLabel(cardRt, "", 12, OverlaySkin.TextMuted, new Vector2(0f, 104f),
                new Vector2(230f, 20f));
            status.gameObject.name = "Status";

            CreateSlot(cardRt, new Vector2(-56f, 22f), true);
            CreateSlot(cardRt, new Vector2(56f, 22f), false);
            AddSetupBtn(cardRt, "Clear", "清除", new Vector2(0f, -72f));
        }

        void BindAvatarSetup()
        {
            _cropUi = FindNode(_cardRt, "Pages/DynamicPage/DynamicHost/AvatarCrop")
                ?.GetComponent<OverlayAvatarCropUi>();
            _cropUi?.Bind();

            _avatarSetup = FindNode(_cardRt, "Pages/DynamicPage/DynamicHost/AvatarSetup")?.gameObject;
            if (_avatarSetup == null)
            {
                return;
            }

            var root = _avatarSetup.transform;
            _avatarSetupStatus = FindNode(root, "Status")?.GetComponent<Text>();
            _slotAImage = FindNode(root, "SlotA/Preview")?.GetComponent<Image>();
            _slotBImage = FindNode(root, "SlotB/Preview")?.GetComponent<Image>();
            BindSlot(FindNode(root, "SlotA"), true);
            BindSlot(FindNode(root, "SlotB"), false);
            BindClick(FindNode(root, "Clear"), () =>
            {
                if (_view == null || _view.Settings == null)
                {
                    return;
                }

                _view.Settings.ClearAvatarPresence();
                _view.ApplyUserSettings();
                _view.NotifyAvatarPresenceChanged();
                RefreshAvatarSlots();
                RefreshLabels();
            });
        }

        void BindSlot(Transform slot, bool slotA)
        {
            if (slot == null)
            {
                return;
            }

            BindClick(slot, () => BeginPickSlot(slotA));
        }

        void CreateSlot(Transform parent, Vector2 pos, bool slotA)
        {
            var bg = CreateImage(slotA ? "SlotA" : "SlotB", parent, OverlaySprites.Button, OverlaySprites.RoundedSquare);
            OverlaySkin.ApplyButton(bg);
            bg.raycastTarget = true;
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(88f, 88f);

            var preview = CreateImage("Preview", rt, Color.white, OverlaySprites.RoundedSquare);
            preview.preserveAspect = true;
            preview.raycastTarget = false;
            var pRt = preview.rectTransform;
            Stretch(pRt);
            pRt.offsetMin = new Vector2(6f, 6f);
            pRt.offsetMax = new Vector2(-6f, -6f);

            var tipBg = CreateImage("TipBg", rt, new Color(0f, 0f, 0f, 0.65f), OverlaySprites.RoundedRect);
            tipBg.raycastTarget = false;
            var tipBgRt = tipBg.rectTransform;
            tipBgRt.anchorMin = tipBgRt.anchorMax = new Vector2(0.5f, 0.5f);
            tipBgRt.sizeDelta = new Vector2(78f, 36f);

            var tip = PlaceLabel(rt, slotA ? "闲置图 A" : "动态图 B", 11, Color.white,
                new Vector2(0f, 0f), new Vector2(76f, 34f));
            tip.gameObject.name = "Tip";
        }

        void AddSetupBtn(Transform parent, string id, string title, Vector2 pos)
        {
            var img = CreateImage(id, parent, OverlaySprites.Button, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(img);
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(72f, 28f);
            FillLabel(rt, title, 13, OverlaySkin.Text);
            img.gameObject.AddComponent<Button>();
        }

        void OpenAvatarSetup()
        {
            ShowPage("DynamicPage");
        }

        void BeginPickSlot(bool slotA)
        {
            if (_pickRoutine != null)
            {
                StopCoroutine(_pickRoutine);
            }

            _pickRoutine = StartCoroutine(BeginPickSlotRoutine(slotA));
        }

        IEnumerator BeginPickSlotRoutine(bool slotA)
        {
            // Leave Button/EventSystem stack before blocking Win32 dialog.
            yield return null;

            string path = null;
            try
            {
                path = OverlayFileDialog.OpenImage();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Overlay] 选图失败: " + e.Message);
                _pickRoutine = null;
                yield break;
            }

            if (string.IsNullOrEmpty(path))
            {
                _pickRoutine = null;
                yield break;
            }

            byte[] bytes;
            try
            {
                bytes = System.IO.File.ReadAllBytes(path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Overlay] 读取形象图失败: " + e.Message);
                _pickRoutine = null;
                yield break;
            }

            if (_cropUi == null)
            {
                Debug.LogError("[Overlay] 设置 Prefab 缺少动态页裁剪界面。");
                _pickRoutine = null;
                yield break;
            }

            ShowAvatarSetup(false);
            _cropUi.Open(bytes, png =>
            {
                if (_view == null || _view.Settings == null || !_view.Settings.TrySetAvatarSlot(slotA, png))
                {
                    Debug.LogWarning("[Overlay] 形象图保存失败。");
                    ShowAvatarSetup(true);
                    return;
                }

                _view.ApplyUserSettings();
                _view.NotifyAvatarPresenceChanged();
                RefreshAvatarSlots();
                RefreshLabels();
                ShowAvatarSetup(true);
            }, () => ShowAvatarSetup(true));
            _pickRoutine = null;
        }

        void RefreshAvatarSlots()
        {
            if (_slotAImage == null || _slotBImage == null)
            {
                return;
            }

            SetSlotPreview(_slotAImage, OverlayAvatarCodec.LocalPathA);
            SetSlotPreview(_slotBImage, OverlayAvatarCodec.LocalPathB);
            if (_avatarSetupStatus != null && _view != null && _view.Settings != null)
            {
                var s = _view.Settings;
                _avatarSetupStatus.text = s.AvatarEnabled
                    ? "已启用（闲置 + 动态） v" + s.AvatarVersion
                    : "需设置两张图后才会启用";
                _avatarSetupStatus.color = OverlaySkin.TextMuted;
            }
        }

        static void SetSlotPreview(Image target, string path)
        {
            ReleaseRuntimeSlotSprite(target);

            if (!System.IO.File.Exists(path))
            {
                target.sprite = OverlaySprites.RoundedSquare;
                target.color = new Color(1f, 1f, 1f, 0.25f);
                return;
            }

            var sp = OverlayAvatarCodec.LoadSprite(path);
            if (sp == null)
            {
                target.sprite = OverlaySprites.RoundedSquare;
                target.color = new Color(1f, 1f, 1f, 0.25f);
                return;
            }

            target.sprite = sp;
            target.color = Color.white;
        }

        static void ReleaseRuntimeSlotSprite(Image target)
        {
            if (target == null)
            {
                return;
            }

            var sprite = target.sprite;
            target.sprite = OverlaySprites.RoundedSquare;
            if (sprite == null || (sprite.hideFlags & HideFlags.HideAndDontSave) == 0)
            {
                return;
            }

            var tex = sprite.texture;
            Destroy(sprite);
            if (tex != null && (tex.hideFlags & HideFlags.HideAndDontSave) != 0)
            {
                Destroy(tex);
            }
        }

        void CloseAvatarOverlays()
        {
            _cropUi?.ForceClose();
            ShowAvatarSetup(true);
        }

        void ShowAvatarSetup(bool show)
        {
            if (_avatarSetup != null)
            {
                _avatarSetup.SetActive(show);
            }
        }

        Transform CreatePage(Transform parent, string name)
        {
            var go = CreateEmpty(name, parent);
            Stretch((RectTransform)go.transform);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 4, 8);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go.transform;
        }

        Image AddTabButton(Transform parent, string id, string title)
        {
            var tab = CreateImage(id, parent, Color.clear, _controlSprite);
            tab.raycastTarget = true;
            var le = tab.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 28f;
            le.preferredHeight = 28f;
            le.flexibleHeight = 0f;
            le.flexibleWidth = 1f;
            FillLabel(tab.rectTransform, title, 14, OverlaySkin.SettingsText);
            tab.gameObject.AddComponent<Button>();
            return tab;
        }

        void ShowPage(string pageName)
        {
            if (string.IsNullOrEmpty(pageName))
            {
                pageName = DefaultPage;
            }

            _page = pageName;
            if (_pagesRoot != null)
            {
                for (var i = 0; i < _pagesRoot.childCount; i++)
                {
                    var page = _pagesRoot.GetChild(i);
                    page.gameObject.SetActive(page.name == pageName);
                }
            }

            if (_tabBar != null)
            {
                for (var i = 0; i < _tabBar.childCount; i++)
                {
                    var tab = _tabBar.GetChild(i);
                    var image = tab.GetComponent<Image>();
                    image.color = TabToPage(tab.name) == pageName
                        ? WithAlpha(ThemeAccent, 0.3f)
                        : Color.clear;
                }
            }

            ApplyFixedCardSize();
            if (pageName == "DisplayPage")
            {
                RefreshAppearanceControls();
            }
            else if (pageName == "DynamicPage")
            {
                RefreshAvatarSlots();
            }
            else
            {
                CloseAvatarOverlays();
            }
        }

        void ApplyFixedCardSize()
        {
            if (_cardRt == null)
            {
                return;
            }

            _cardRt.sizeDelta = new Vector2(CardWidth, CardHeight);
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        Text AddScaleRow(Transform parent)
        {
            var row = CreateRow(parent, "ScaleRow", RowHeight);
            AddRowTitle(row, "放大倍数", muted: true);
            AddRowButton(row, "Minus", "-", 24f, 14);
            var value = CreateRowLabel(row, "Value", "1.0x", 48f, 14);
            AddRowButton(row, "Plus", "+", 24f, 14);
            return value;
        }

        Text AddToggleRow(Transform parent, string id, string title)
        {
            var row = CreateRow(parent, id, RowHeight);
            AddRowTitle(row, title);
            var toggle = AddRowButton(row, "Toggle", "开", 48f, 13);
            return toggle.GetComponentInChildren<Text>();
        }

        Dropdown AddDisplayDropdownRow(Transform parent)
        {
            var row = CreateRow(parent, "DisplayRow", RowHeight);
            AddRowTitle(row, "显示器");

            var background = CreateImage("DisplayDropdown", row, OverlaySkin.ThemeInputBackground(1), _controlSprite);
            background.raycastTarget = true;
            var element = background.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 64f;
            element.preferredWidth = 64f;
            element.minHeight = 24f;
            element.preferredHeight = 24f;

            var caption = FillLabel(background.rectTransform, "1", 13, OverlaySkin.SettingsText);
            caption.gameObject.name = "Label";
            caption.alignment = TextAnchor.MiddleLeft;
            caption.rectTransform.offsetMin = new Vector2(8f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-20f, 0f);

            var arrow = PlaceLabel(background.rectTransform, "▾", 12, OverlaySkin.SettingsText,
                new Vector2(23f, 0f), new Vector2(16f, 20f));
            arrow.gameObject.name = "Arrow";

            var template = CreateImage("Template", background.rectTransform, OverlaySkin.ThemeBackground(1), _themeSprite);
            template.raycastTarget = true;
            var templateRt = template.rectTransform;
            templateRt.anchorMin = new Vector2(0f, 0f);
            templateRt.anchorMax = new Vector2(1f, 0f);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.anchoredPosition = new Vector2(0f, -2f);
            templateRt.sizeDelta = new Vector2(0f, 120f);

            var content = CreateEmpty("Content", templateRt);
            var contentRt = (RectTransform)content.transform;
            Stretch(contentRt);

            var item = CreateImage("Item", contentRt, Color.clear, _controlSprite);
            item.raycastTarget = true;
            var itemRt = item.rectTransform;
            itemRt.anchorMin = new Vector2(0f, 1f);
            itemRt.anchorMax = new Vector2(1f, 1f);
            itemRt.pivot = new Vector2(0.5f, 1f);
            itemRt.anchoredPosition = Vector2.zero;
            itemRt.sizeDelta = new Vector2(0f, 24f);
            var itemToggle = item.gameObject.AddComponent<Toggle>();
            itemToggle.targetGraphic = item;
            itemToggle.transition = Selectable.Transition.None;

            var itemLabel = FillLabel(itemRt, "1", 13, OverlaySkin.SettingsText);
            itemLabel.gameObject.name = "Item Label";
            itemLabel.alignment = TextAnchor.MiddleLeft;
            itemLabel.rectTransform.offsetMin = new Vector2(8f, 0f);
            itemLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);

            var dropdown = background.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.captionText = caption;
            dropdown.template = templateRt;
            dropdown.itemText = itemLabel;
            dropdown.options.Add(new Dropdown.OptionData("1"));
            dropdown.value = 0;
            dropdown.transition = Selectable.Transition.None;
            template.gameObject.SetActive(false);
            return dropdown;
        }

        Dropdown CreateCompactDropdown(
            Transform row, string name, float width, params string[] options)
        {
            var background = CreateImage(name, row, OverlaySkin.ThemeInputBackground(1), _controlSprite);
            background.raycastTarget = true;
            var element = background.gameObject.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = 24f;
            element.preferredHeight = 24f;

            var initial = options != null && options.Length > 0 ? options[0] : string.Empty;
            var caption = FillLabel(background.rectTransform, initial, 12, OverlaySkin.SettingsText);
            caption.gameObject.name = "Label";
            caption.alignment = TextAnchor.MiddleLeft;
            caption.rectTransform.offsetMin = new Vector2(8f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-20f, 0f);

            var arrow = PlaceLabel(background.rectTransform, "▾", 12, OverlaySkin.SettingsText,
                new Vector2(31f, 0f), new Vector2(16f, 20f));
            arrow.gameObject.name = "Arrow";
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.anchoredPosition = new Vector2(-3f, 0f);

            var template = CreateImage("Template", background.rectTransform,
                OverlaySkin.ThemeBackground(1), _themeSprite);
            template.raycastTarget = true;
            var templateRt = template.rectTransform;
            templateRt.anchorMin = new Vector2(0f, 0f);
            templateRt.anchorMax = new Vector2(1f, 0f);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.anchoredPosition = new Vector2(0f, -1f);
            templateRt.sizeDelta = new Vector2(0f, 48f);

            var content = CreateEmpty("Content", templateRt);
            Stretch((RectTransform)content.transform);
            var item = CreateImage("Item", content.transform, Color.clear, _controlSprite);
            item.raycastTarget = true;
            var itemRt = item.rectTransform;
            itemRt.anchorMin = new Vector2(0f, 1f);
            itemRt.anchorMax = new Vector2(1f, 1f);
            itemRt.pivot = new Vector2(0.5f, 1f);
            itemRt.anchoredPosition = Vector2.zero;
            itemRt.sizeDelta = new Vector2(0f, 24f);
            var itemToggle = item.gameObject.AddComponent<Toggle>();
            itemToggle.targetGraphic = item;
            itemToggle.transition = Selectable.Transition.None;

            var itemLabel = FillLabel(itemRt, initial, 12, OverlaySkin.SettingsText);
            itemLabel.gameObject.name = "Item Label";
            itemLabel.alignment = TextAnchor.MiddleLeft;
            itemLabel.rectTransform.offsetMin = new Vector2(8f, 0f);
            itemLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);

            var dropdown = background.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.captionText = caption;
            dropdown.template = templateRt;
            dropdown.itemText = itemLabel;
            if (options != null)
            {
                for (var i = 0; i < options.Length; i++)
                {
                    dropdown.options.Add(new Dropdown.OptionData(options[i]));
                }
            }
            dropdown.transition = Selectable.Transition.None;
            template.gameObject.SetActive(false);
            return dropdown;
        }

        Text AddStatusRow(Transform parent, string id, string title)
        {
            var row = CreateRow(parent, id, 22f);
            var label = CreateRowLabel(row, "Muted", title, -1f, 13);
            label.alignment = TextAnchor.MiddleLeft;
            return label;
        }

        void AddActionRow(Transform parent, string id, string title, bool danger = false)
        {
            var button = CreateImage(id, parent, Color.clear, _controlSprite);
            button.raycastTarget = true;
            var le = button.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = ActionRowHeight;
            le.minHeight = ActionRowHeight;
            le.flexibleHeight = 0f;
            FillLabel(button.rectTransform, title, 13, danger ? OverlaySkin.ThemeDanger(1) : OverlaySkin.SettingsText);
            button.gameObject.AddComponent<Button>();
        }

        static RectTransform CreateRow(Transform parent, string name, float height)
        {
            var go = CreateEmpty(name, parent);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            le.flexibleHeight = 0f;
            return (RectTransform)go.transform;
        }

        static void AddRowTitle(Transform row, string title, bool muted = false)
        {
            var label = CreateRowLabel(row, muted ? "Muted" : "Title", title, -1f, 14);
            label.alignment = TextAnchor.MiddleLeft;
            var le = label.gameObject.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;
        }

        Image AddRowButton(Transform row, string id, string title, float width, int fontSize)
        {
            var button = CreateImage(id, row, Color.clear, _controlSprite);
            button.raycastTarget = true;
            var le = button.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
            le.minHeight = 24f;
            le.preferredHeight = 24f;
            le.flexibleHeight = 0f;
            FillLabel(button.rectTransform, title, fontSize, OverlaySkin.SettingsText);
            button.gameObject.AddComponent<Button>();
            return button;
        }

        static Text CreateRowLabel(Transform parent, string name, string text, float width, int size)
        {
            var label = CreateLabel(parent, text, size, OverlaySkin.Text);
            label.gameObject.name = name;
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.flexibleHeight = 0f;
            if (width > 0f)
            {
                le.preferredWidth = width;
                le.minWidth = width;
            }
            else
            {
                le.flexibleWidth = 1f;
            }

            return label;
        }

        void TogglePanel()
        {
            if (_panel != null && _panel.activeSelf)
            {
                Hide();
                return;
            }

            Show();
        }

        void Show()
        {
            if (_panel == null)
            {
                return;
            }

            _view?.HideInteractMenu();
            _panel.SetActive(true);
            RefreshLabels();
        }

        public void Hide()
        {
            CloseAvatarOverlays();
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        void RefreshLabels()
        {
            var settings = _view != null ? _view.Settings : null;
            if (settings == null)
            {
                return;
            }

            if (_scaleText != null)
            {
                _scaleText.text = settings.Scale.ToString("0.0") + "x";
            }

            SetToggle(_topmostText, settings.AlwaysOnTop);
            SetToggle(_testModeText, settings.TestMode);
            SetToggle(_flipText, settings.FlipHorizontal);
            SetToggle(_autoStartText, settings.AutoStart);
            SetToggle(_inputIconsText, settings.ShowInputIcons);
            RefreshAppearanceControls();

            if (_avatarStatusText != null)
            {
                var ready = settings.AvatarEnabled;
                var hasA = System.IO.File.Exists(OverlayAvatarCodec.LocalPathA);
                var hasB = System.IO.File.Exists(OverlayAvatarCodec.LocalPathB);
                _avatarStatusText.text = ready
                    ? "形象已启用 v" + settings.AvatarVersion
                    : "形象未启用（闲置:" + (hasA ? "有" : "无") + " 动态:" + (hasB ? "有" : "无") + "）";
                _avatarStatusText.color = ThemeMuted;
            }

            RefreshDisplayOptions();
        }

        void ApplyIcons()
        {
            _settingsIcon = Resources.Load<Sprite>(SettingsIconResource);
            _closeIcon = Resources.Load<Sprite>(CloseIconResource);

            ApplyIcon(EnsureIcon(_buttonRt, SettingsIconSize), _settingsIcon);
            ApplyIcon(EnsureIcon(FindNode(_cardRt, "Header/Close"), 16f), _closeIcon);
        }

        static Image EnsureIcon(Transform parent, float size)
        {
            if (parent == null)
            {
                return null;
            }

            var icon = parent.Find("Icon")?.GetComponent<Image>();
            if (icon == null)
            {
                icon = CreateIconPlaceholder(parent, size);
            }

            icon.rectTransform.sizeDelta = new Vector2(size, size);
            return icon;
        }

        void RestoreTextTabs()
        {
            RestoreTextTab("GameTab");
            RestoreTextTab("DisplayTab");
            RestoreTextTab("DynamicTab");
            RestoreTextTab("SystemTab");
        }

        void RestoreTextTab(string tabName)
        {
            var tab = FindNode(_tabBar, tabName);
            if (tab == null)
            {
                return;
            }

            var icon = tab.Find("Icon");
            if (icon != null)
            {
                icon.gameObject.SetActive(false);
            }

            var label = tab.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.gameObject.SetActive(true);
            }
        }

        void ApplyTypography()
        {
            var labels = _cardRt.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                var parentName = label.transform.parent.name;
                var compactAppearanceHint =
                    label.gameObject.name.EndsWith("Hint") &&
                    (parentName == "ThemeCard" || parentName == "ColorCard");
                label.fontSize = parentName == "Header"
                    ? 14
                    : parentName == "Close"
                        ? 14
                        : label.gameObject.name == "SectionTitle" ||
                          label.gameObject.name == "IntensityValue"
                            ? 10
                        : compactAppearanceHint
                            ? 10
                        : (label.gameObject.name == "Muted" || label.gameObject.name == "Status" ||
                           label.gameObject.name.EndsWith("Hint"))
                            ? 12
                            : 13;
            }

            var buttons = _cardRt.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                var name = buttons[i].gameObject.name;
                if (name != "Toggle" && name != "Minus" && name != "Plus")
                {
                    continue;
                }

                var element = buttons[i].GetComponent<LayoutElement>();
                if (element != null)
                {
                    element.minHeight = 24f;
                    element.preferredHeight = 24f;
                }
            }
        }

        static void ApplyIcon(Image image, Sprite sprite)
        {
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }

        public void ApplyTheme()
        {
            if (_cardRt == null)
            {
                return;
            }

            if (_themeSprite == null)
            {
                _themeSprite = Resources.Load<Sprite>(ThemeSpriteResource);
                _controlSprite = Resources.Load<Sprite>(ControlSpriteResource);
            }

            if (_themeImages.Count == 0)
            {
                _themeImages.AddRange(_cardRt.GetComponentsInChildren<Image>(true));
                _themeLabels.AddRange(_cardRt.GetComponentsInChildren<Text>(true));
            }

            if (_themeSprite == null || _controlSprite == null)
            {
                Debug.LogWarning("[Overlay] 缺少设置主题图片。");
                return;
            }

            for (var i = 0; i < _themeImages.Count; i++)
            {
                var image = _themeImages[i];
                if (image == null)
                {
                    continue;
                }

                var name = image.gameObject.name;
                if (name == "Icon")
                {
                    image.color = ThemeText;
                    continue;
                }

                if (name == "HueSwatch" || name == "HueHandle" || name == "IntensityHandle" ||
                    name == "Preview" || name == "Handle" ||
                    name == "Frame" || name == "Mask" || name == "TipBg")
                {
                    continue;
                }

                if (name == "Divider")
                {
                    image.sprite = _themeSprite;
                    image.type = Image.Type.Sliced;
                    image.color = OverlaySkin.ThemeDivider(ThemeId);
                    continue;
                }

                var circularButton =
                    name == "Close" || name == "Minus" || name == "Plus";
                var roundedSurface =
                    name == "Background" || name == "Header" || name == "TabBar" ||
                    name == "ThemeCard" || name == "ColorCard" ||
                    name == "Template" || name == "DisplayDropdown" ||
                    name == "ThemeDropdown" || name == "Toggle" ||
                    name.EndsWith("Tab");
                image.sprite = circularButton
                    ? OverlaySprites.Circle
                    : roundedSurface
                        ? OverlaySprites.RoundedRect
                        : _controlSprite;
                image.type = circularButton ? Image.Type.Simple : Image.Type.Sliced;
                image.preserveAspect = circularButton;
                var selectedTab = name.EndsWith("Tab") && TabToPage(name) == _page;
                if (name == "Background")
                {
                    image.color = ThemeBackground;
                }
                else if (name == "Header")
                {
                    image.color = ThemeBackground;
                }
                else if (name == "TabBar")
                {
                    image.color = ThemeBackground;
                }
                else if (name == "Template")
                {
                    image.color = ThemeBackground;
                }
                else if (name == "DisplayDropdown" || name == "ThemeDropdown")
                {
                    image.color = OverlaySkin.ThemeInputBackground(ThemeId);
                }
                else if (name == "HueTrack" || name == "IntensityTrack")
                {
                    image.color = OverlaySkin.ThemeDivider(ThemeId);
                }
                else if (name == "Item")
                {
                    image.color = Color.clear;
                }
                else if (name == "Close")
                {
                    image.color = Color.clear;
                }
                else if (name.EndsWith("Tab"))
                {
                    image.color = selectedTab ? WithAlpha(ThemeAccent, 0.3f) : Color.clear;
                }
                else if (name == "Toggle")
                {
                    image.color = ToggleRestColor(image);
                }
                else if (name == "Minus" || name == "Plus" || name == "Reset" || name == "Confirm" ||
                         name == "Cancel" || name == "Shrink" || name == "Grow" || name == "Clear" ||
                         name.EndsWith("Row") && image.GetComponent<Button>() != null)
                {
                    image.color = Color.clear;
                }
                else
                {
                    image.color = ThemeControl;
                }
            }

            for (var i = 0; i < _themeLabels.Count; i++)
            {
                var label = _themeLabels[i];
                if (label != null)
                {
                    label.color = label.gameObject.name == "Tip"
                        ? Color.white
                        : label.transform.parent.name == "QuitGameRow"
                        ? OverlaySkin.ThemeDanger(ThemeId)
                        : label.gameObject.name == "Muted" || label.gameObject.name == "Status" ||
                          label.gameObject.name.EndsWith("Hint")
                            ? ThemeMuted
                            : ThemeText;
                }
            }

            var outline = _cardRt.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }

            if (_buttonImage != null)
            {
                _buttonImage.sprite = OverlaySprites.Circle;
                _buttonImage.type = Image.Type.Simple;
                _buttonImage.preserveAspect = true;
                _buttonImage.color = Color.clear;
                var icon = _buttonRt != null ? _buttonRt.Find("Icon")?.GetComponent<Image>() : null;
                if (icon != null)
                {
                    icon.color = OverlaySkin.SettingsEntryIconColor(ThemeId);
                }
            }

            RefreshAppearanceControls();
        }

        void SetToggle(Text label, bool on)
        {
            if (label == null)
            {
                return;
            }

            label.text = on ? "开" : "关";
            label.color = ThemeText;
            var image = label.transform.parent.GetComponent<Image>();
            if (image != null)
            {
                image.color = on ? WithAlpha(ThemeAccent, 0.3f) : Color.clear;
            }
        }

        void LateUpdate()
        {
            var chip = _view != null ? _view.LocalChip : null;
            var show = chip != null;
            if (_buttonRt != null)
            {
                _buttonRt.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            var pos = chip.FollowPosition;
            var scale = _view.Settings != null ? _view.Settings.Scale : 1f;
            var chipSize = _view.Config != null ? _view.Config.chipSize : 128f;
            var half = GearSize * 0.5f;
            var buttonOffset = new Vector2(
                (chipSize * 0.5f - half) * scale,
                -(chipSize * 0.5f + 22f) * scale);

            _buttonRt.localScale = new Vector3(scale, scale, 1f);
            _buttonRt.anchoredPosition = pos + buttonOffset;

            if (_panel != null && _panel.activeSelf && _cardRt != null)
            {
                PlaceCardBeside(pos);
            }
        }

        void PlaceCardBeside(Vector2 avatarPos)
        {
            var size = _cardRt.sizeDelta * CardVisualScale;
            var chipSize = _view != null && _view.Config != null ? _view.Config.chipSize : 128f;
            var avatarScale = _view != null && _view.Settings != null ? _view.Settings.Scale : 1f;
            var offsetX = chipSize * 0.5f * avatarScale + 8f + size.x * 0.5f;
            if (avatarPos.x + offsetX + size.x * 0.5f > Screen.width - 12f)
            {
                offsetX = -offsetX;
            }

            var cardPos = avatarPos + new Vector2(offsetX, 36f);
            cardPos.x = Mathf.Clamp(cardPos.x, 12f + size.x * 0.5f, Screen.width - 12f - size.x * 0.5f);
            cardPos.y = Mathf.Clamp(cardPos.y, 12f + size.y * 0.5f, Screen.height - 12f - size.y * 0.5f);
            _cardRt.anchoredPosition = cardPos;
        }

        static Transform FindNode(Transform root, string path)
        {
            return root != null ? root.Find(path) : null;
        }

        static GameObject CreateEmpty(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
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

        void CreateDivider(Transform parent, string name, bool bottom)
        {
            var divider = CreateImage(name, parent, OverlaySkin.ThemeDivider(1), _themeSprite);
            divider.raycastTarget = false;
            var rt = divider.rectTransform;
            rt.anchorMin = new Vector2(0f, bottom ? 0f : 1f);
            rt.anchorMax = new Vector2(1f, bottom ? 0f : 1f);
            rt.pivot = new Vector2(0.5f, bottom ? 0f : 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 1f);
            divider.transform.SetAsLastSibling();
        }

        static Text FillLabel(Transform parent, string text, int size, Color color)
        {
            var label = CreateLabel(parent, text, size, color);
            Stretch((RectTransform)label.transform);
            return label;
        }

        static Text PlaceLabel(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 sizeDelta)
        {
            var label = CreateLabel(parent, text, size, color);
            var rt = (RectTransform)label.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            return label;
        }

        static Text CreateLabel(Transform parent, string text, int size, Color color)
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
