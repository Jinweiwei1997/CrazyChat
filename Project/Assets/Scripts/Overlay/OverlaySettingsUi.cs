using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class OverlaySettingsUi : MonoBehaviour
    {
        const string PrefabResource = "Prefab/UI/SettingsMenu";
        const string DefaultPage = "GamePage";
        const float CardWidth = 300f;
        const float CardHeight = 540f;
        const float HeaderHeight = 44f;
        const float TabBarHeight = 36f;
        const float RowHeight = 32f;
        const float ActionRowHeight = 36f;
        const float GearSize = 48f;
        const float CloseSize = 32f;

        FriendOverlayView _view;
        [SerializeField] GameObject _panel;
        [SerializeField] RectTransform _buttonRt;
        [SerializeField] RectTransform _cardRt;
        [SerializeField] RectTransform _tabBar;
        [SerializeField] RectTransform _pagesRoot;
        [SerializeField] Text _scaleText;
        [SerializeField] Text _topmostText;
        [SerializeField] Text _dragText;
        [SerializeField] Text _flipText;
        [SerializeField] Text _autoStartText;
        [SerializeField] Text _clickEffectText;
        [SerializeField] Text _inputIconsText;
        [SerializeField] Text _avatarStatusText;
        Text _avatarSetupStatus;
        Image _slotAImage;
        Image _slotBImage;
        GameObject _avatarSetup;
        OverlayAvatarCropUi _cropUi;
        [SerializeField] Text _titleText;
        [SerializeField] Image _buttonImage;
        [SerializeField] Image _cardImage;
        [SerializeField] Image _closeImage;
        string _page = DefaultPage;
        bool _hoverButton;
        bool _hoverCard;
        float _hideAt = -1f;
        float _showAt = -1f;

        public static OverlaySettingsUi Create(Transform chrome, Transform modal, FriendOverlayView view)
        {
            var prefab = Resources.Load<GameObject>(PrefabResource);
            OverlaySettingsUi ui = null;
            if (prefab != null)
            {
                var root = Instantiate(prefab, chrome, false);
                root.name = "SettingsUi";
                Stretch((RectTransform)root.transform);
                ui = root.GetComponent<OverlaySettingsUi>();
                if (ui == null)
                {
                    Destroy(root);
                }
                else if (ui._panel != null)
                {
                    ui._panel.transform.SetParent(modal != null ? modal : ui.transform, false);
                    Stretch((RectTransform)ui._panel.transform);
                }
            }

            if (ui == null)
            {
                var root = new GameObject("SettingsUi", typeof(RectTransform));
                root.transform.SetParent(chrome, false);
                Stretch((RectTransform)root.transform);
                ui = root.AddComponent<OverlaySettingsUi>();
                ui.Build(modal);
            }

            ui._view = view;
            ui.Resolve();
            ui.Bind();
            ui.ShowPage(DefaultPage);
            // ui.ApplySkin();
            ui.Hide();
            return ui;
        }

#if UNITY_EDITOR
        public void EditorPopulate()
        {
            Build(null);
        }
#endif

        void Resolve()
        {
            if (_buttonImage == null)
            {
                _buttonImage = FindImage(transform, "SettingsButton");
            }

            if (_buttonRt == null && _buttonImage != null)
            {
                _buttonRt = _buttonImage.rectTransform;
            }

            if (_panel == null)
            {
                var panel = transform.Find("SettingsPanel");
                if (panel != null)
                {
                    _panel = panel.gameObject;
                }
            }

            if (_cardImage == null && _panel != null)
            {
                _cardImage = FindImage(_panel.transform, "Background");
            }

            if (_cardRt == null && _cardImage != null)
            {
                _cardRt = _cardImage.rectTransform;
            }

            if (_closeImage == null && _cardRt != null)
            {
                _closeImage = FindImage(_cardRt, "Header/Close");
            }

            if (_titleText == null && _cardRt != null)
            {
                _titleText = FindLabel(_cardRt, "Header/Title");
            }

            if (_tabBar == null && _cardRt != null)
            {
                var tabBar = _cardRt.Find("TabBar");
                if (tabBar != null)
                {
                    _tabBar = (RectTransform)tabBar;
                }
            }

            if (_pagesRoot == null && _cardRt != null)
            {
                var pages = _cardRt.Find("Pages");
                if (pages != null)
                {
                    _pagesRoot = (RectTransform)pages;
                }
            }

            _scaleText = First(_scaleText, FindLabel(_cardRt, "Pages/GamePage/ScaleRow/Value"));
            _dragText = First(_dragText, FindLabel(_cardRt, "Pages/GamePage/DisableDragRow/Toggle/Label"));
            _flipText = First(_flipText, FindLabel(_cardRt, "Pages/GamePage/FlipHorizontalRow/Toggle/Label"));
            _clickEffectText = First(_clickEffectText, FindLabel(_cardRt, "Pages/GamePage/ClickEffectRow/Choice/Label"));
            _inputIconsText = First(_inputIconsText, FindLabel(_cardRt, "Pages/GamePage/InputIconsRow/Toggle/Label"));
            _avatarStatusText = First(_avatarStatusText, FindLabel(_cardRt, "Pages/GamePage/AvatarStatusRow/Muted"));
            var skinRow = FindNode(_cardRt, "Pages/GamePage/UiSkinRow");
            if (skinRow != null)
            {
                skinRow.gameObject.SetActive(false);
            }
            _topmostText = First(_topmostText, FindLabel(_cardRt, "Pages/SystemPage/AlwaysOnTopRow/Toggle/Label"));
            _autoStartText = First(_autoStartText, FindLabel(_cardRt, "Pages/SystemPage/AutoStartRow/Toggle/Label"));
        }

        static Text First(Text current, Text found)
        {
            return current != null ? current : found;
        }

        void Bind()
        {
            OverlayHoverRelay.Bind(_buttonImage != null ? _buttonImage.gameObject : null,
                HoverEnterFromButton, HoverLeaveFromButton);
            OverlayHoverRelay.Bind(_cardImage != null ? _cardImage.gameObject : null,
                HoverEnterFromCard, HoverLeaveFromCard);
            BindClick(_closeImage, Hide);
            BindTabs();
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
            BindClick(FindNode(_cardRt, "Pages/GamePage/DisableDragRow/Toggle"), () =>
            {
                _view.Settings.SetDisableDrag(!_view.Settings.DisableDrag);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            BindClick(FindNode(_cardRt, "Pages/GamePage/FlipHorizontalRow/Toggle"), () =>
            {
                _view.Settings.SetFlipHorizontal(!_view.Settings.FlipHorizontal);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            BindClick(FindNode(_cardRt, "Pages/GamePage/ClickEffectRow/Choice"), () =>
            {
                _view.Settings.CycleClickEffect();
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            BindClick(FindNode(_cardRt, "Pages/GamePage/InputIconsRow/Toggle"), () =>
            {
                _view.Settings.SetShowInputIcons(!_view.Settings.ShowInputIcons);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            BindClick(FindNode(_cardRt, "Pages/GamePage/AvatarSetupRow"), OpenAvatarSetup);
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
            BindClick(FindNode(_cardRt, "Pages/SystemPage/QuitGameRow"), QuitGame);
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
        }

        void Build(Transform modal)
        {
            _buttonImage = CreateImage("SettingsButton", transform, OverlaySprites.Panel, OverlaySprites.RoundedRect);
            OverlaySkin.ApplySettingsGear(_buttonImage);
            _buttonImage.raycastTarget = true;
            _buttonRt = _buttonImage.rectTransform;
            _buttonRt.anchorMin = new Vector2(0f, 0f);
            _buttonRt.anchorMax = new Vector2(0f, 0f);
            _buttonRt.pivot = new Vector2(0.5f, 0.5f);
            _buttonRt.sizeDelta = new Vector2(GearSize, GearSize);

            _panel = new GameObject("SettingsPanel", typeof(RectTransform));
            _panel.transform.SetParent(modal != null ? modal : transform, false);
            Stretch((RectTransform)_panel.transform);
            _panel.SetActive(false);

            var dim = CreateImage("Dim", _panel.transform, new Color(0f, 0f, 0f, 0.22f), OverlaySprites.RoundedRect);
            dim.raycastTarget = false;
            Stretch(dim.rectTransform);

            _cardImage = CreateImage("Background", _panel.transform, OverlaySprites.Panel, OverlaySprites.RoundedRect);
            OverlaySkin.ApplySettingsPanel(_cardImage);
            _cardImage.raycastTarget = true;
            _cardRt = _cardImage.rectTransform;
            _cardRt.anchorMin = new Vector2(0f, 0f);
            _cardRt.anchorMax = new Vector2(0f, 0f);
            _cardRt.pivot = new Vector2(0.5f, 0.5f);
            _cardRt.sizeDelta = new Vector2(CardWidth, CardHeight);

            BuildHeader(_cardRt);
            BuildTabBar(_cardRt);
            BuildPages(_cardRt);
        }

        void BuildHeader(RectTransform parent)
        {
            var header = CreateEmpty("Header", parent);
            var headerRt = (RectTransform)header.transform;
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0f, HeaderHeight);

            _titleText = PlaceLabel(headerRt, "设置", 18, OverlaySkin.SettingsText, Vector2.zero, new Vector2(160f, 28f));
            _titleText.gameObject.name = "Title";

            _closeImage = CreateImage("Close", headerRt, OverlaySprites.Button, OverlaySprites.RoundedRect);
            OverlaySkin.ApplySettingsClose(_closeImage);
            _closeImage.raycastTarget = true;
            var closeRt = _closeImage.rectTransform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-10f, 0f);
            closeRt.sizeDelta = new Vector2(CloseSize, CloseSize);
            _closeImage.gameObject.AddComponent<Button>();
        }

        void BuildTabBar(RectTransform parent)
        {
            var tabBar = CreateEmpty("TabBar", parent);
            _tabBar = (RectTransform)tabBar.transform;
            _tabBar.anchorMin = new Vector2(0f, 1f);
            _tabBar.anchorMax = new Vector2(1f, 1f);
            _tabBar.pivot = new Vector2(0.5f, 1f);
            _tabBar.anchoredPosition = new Vector2(0f, -HeaderHeight);
            _tabBar.sizeDelta = new Vector2(0f, TabBarHeight);
            var layout = _tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 4, 4);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            AddTabButton(_tabBar, "GameTab", "游戏");
            AddTabButton(_tabBar, "SystemTab", "系统");
        }

        void BuildPages(RectTransform parent)
        {
            var pages = CreateEmpty("Pages", parent);
            _pagesRoot = (RectTransform)pages.transform;
            _pagesRoot.anchorMin = Vector2.zero;
            _pagesRoot.anchorMax = Vector2.one;
            _pagesRoot.offsetMin = new Vector2(0f, 10f);
            _pagesRoot.offsetMax = new Vector2(0f, -(HeaderHeight + TabBarHeight));

            var gamePage = CreatePage(_pagesRoot, "GamePage");
            _scaleText = AddScaleRow(gamePage);
            _dragText = AddToggleRow(gamePage, "DisableDragRow", "禁止拖动");
            _flipText = AddToggleRow(gamePage, "FlipHorizontalRow", "水平翻转");
            _clickEffectText = AddChoiceRow(gamePage, "ClickEffectRow", "点击效果");
            _inputIconsText = AddToggleRow(gamePage, "InputIconsRow", "按键图标");
            _avatarStatusText = AddStatusRow(gamePage, "AvatarStatusRow", "动态形象");
            AddActionRow(gamePage, "AvatarSetupRow", "设置动态图");
            AddActionRow(gamePage, "ResetLayoutRow", "复位头像位置");
            AddActionRow(gamePage, "ResetScaleRow", "复位缩放");

            var systemPage = CreatePage(_pagesRoot, "SystemPage");
            systemPage.gameObject.SetActive(false);
            _topmostText = AddToggleRow(systemPage, "AlwaysOnTopRow", "始终置顶");
            _autoStartText = AddToggleRow(systemPage, "AutoStartRow", "开机自启");
            AddActionRow(systemPage, "QuitGameRow", "退出游戏", danger: true);
        }

        public void ApplySkin()
        {
            OverlaySkin.ApplySettingsGear(_buttonImage);
            if (_buttonRt != null)
            {
                _buttonRt.sizeDelta = new Vector2(GearSize, GearSize);
                HideChildLabels(_buttonRt);
            }

            OverlaySkin.ApplySettingsPanel(_cardImage);
            OverlaySkin.ApplySettingsClose(_closeImage);
            if (_closeImage != null)
            {
                _closeImage.rectTransform.sizeDelta = new Vector2(CloseSize, CloseSize);
                HideChildLabels(_closeImage.rectTransform);
            }

            if (_cardRt != null)
            {
                var images = _cardRt.GetComponentsInChildren<Image>(true);
                for (var i = 0; i < images.Length; i++)
                {
                    var image = images[i];
                    if (image == _cardImage || image == _closeImage)
                    {
                        continue;
                    }

                    var name = image.gameObject.name;
                    if (name.EndsWith("Tab") || name == "Header" || name == "TabBar" || name == "Pages" ||
                        name == "Dim" || name.EndsWith("Page") || name.EndsWith("Row"))
                    {
                        if (name.EndsWith("Tab"))
                        {
                            continue;
                        }

                        if (!name.EndsWith("Row") || image.GetComponent<Button>() == null)
                        {
                            continue;
                        }
                    }

                    if (name == "QuitGameRow")
                    {
                        OverlaySkin.ApplySettingsButton(image, danger: true);
                    }
                    else if (name == "Choice" || name == "AvatarSetupRow" || name == "ResetLayoutRow" ||
                             name == "ResetScaleRow")
                    {
                        OverlaySkin.ApplySettingsButton(image, on: true);
                    }
                    else if (name == "Toggle")
                    {
                        OverlaySkin.ApplySettingsToggle(image, false);
                    }
                    else if (name == "Minus" || name == "Plus")
                    {
                        OverlaySkin.ApplySettingsButton(image);
                    }
                }
            }

            ApplyFonts();
            PaintTexts();
            ShowPage(_page);
            RefreshLabels();
            ApplyAvatarSetupSkin();
        }

        void ApplyFonts()
        {
            ApplyFontsOn(_buttonRt);
            ApplyFontsOn(_cardRt);
        }

        static void ApplyFontsOn(Transform root)
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

        void PaintTexts()
        {
            if (_titleText != null)
            {
                _titleText.color = OverlaySkin.SettingsText;
            }

            PaintSubtreeTexts(_cardRt);
        }

        static void PaintSubtreeTexts(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var labels = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].color = labels[i].gameObject.name == "Muted"
                    ? OverlaySkin.SettingsMuted
                    : OverlaySkin.SettingsText;
            }
        }

        void EnsureAvatarSetup()
        {
            if (_avatarSetup != null)
            {
                return;
            }

            var modal = _panel != null ? _panel.transform.parent : transform;
            _cropUi = OverlayAvatarCropUi.Create(modal);

            _avatarSetup = new GameObject("AvatarSetup", typeof(RectTransform));
            _avatarSetup.transform.SetParent(modal, false);
            Stretch((RectTransform)_avatarSetup.transform);
            _avatarSetup.SetActive(false);

            var dim = CreateImage("Dim", _avatarSetup.transform, new Color(0f, 0f, 0f, 0.35f), OverlaySprites.RoundedRect);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);

            var card = CreateImage("Card", _avatarSetup.transform, OverlaySprites.Panel, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyPanel(card);
            card.raycastTarget = true;
            var cardRt = card.rectTransform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(300f, 260f);

            PlaceLabel(cardRt, "设置动态图", 18, OverlaySkin.Text, new Vector2(0f, 100f), new Vector2(200f, 28f));
            _avatarSetupStatus = PlaceLabel(cardRt, "", 12, OverlaySkin.TextMuted, new Vector2(0f, 72f),
                new Vector2(260f, 20f));

            _slotAImage = CreateSlot(cardRt, new Vector2(-56f, 0f), true);
            _slotBImage = CreateSlot(cardRt, new Vector2(56f, 0f), false);

            AddSetupBtn(cardRt, "Clear", "清除", new Vector2(-50f, -90f), () =>
            {
                _view.Settings.ClearAvatarPresence();
                _view.ApplyUserSettings();
                _view.NotifyAvatarPresenceChanged();
                RefreshAvatarSlots();
                RefreshLabels();
            });
            AddSetupBtn(cardRt, "Close", "关闭", new Vector2(50f, -90f), () => _avatarSetup.SetActive(false));
        }

        void ApplyAvatarSetupSkin()
        {
            if (_avatarSetup == null)
            {
                return;
            }

            var card = FindImage(_avatarSetup.transform, "Card");
            OverlaySkin.ApplyPanel(card);
            OverlaySkin.ApplyButton(FindImage(_avatarSetup.transform, "Card/SlotA"));
            OverlaySkin.ApplyButton(FindImage(_avatarSetup.transform, "Card/SlotB"));
            OverlaySkin.ApplyButton(FindImage(_avatarSetup.transform, "Card/Clear"));
            OverlaySkin.ApplyButton(FindImage(_avatarSetup.transform, "Card/Close"));
            ApplyFontsOn(_avatarSetup.transform);
            if (_avatarSetupStatus != null)
            {
                _avatarSetupStatus.color = OverlaySkin.TextMuted;
            }
        }

        Image CreateSlot(Transform parent, Vector2 pos, bool slotA)
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
            tipBg.gameObject.SetActive(false);

            var tip = PlaceLabel(rt, slotA ? "设置闲置图" : "设置动态图", 11, Color.white,
                new Vector2(0f, 0f), new Vector2(76f, 34f));
            tip.gameObject.SetActive(false);

            OverlayHoverRelay.Bind(bg.gameObject,
                () =>
                {
                    tipBg.gameObject.SetActive(true);
                    tip.gameObject.SetActive(true);
                    tipBg.transform.SetAsLastSibling();
                    tip.transform.SetAsLastSibling();
                },
                () =>
                {
                    tipBg.gameObject.SetActive(false);
                    tip.gameObject.SetActive(false);
                });
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => BeginPickSlot(slotA));
            return preview;
        }

        void AddSetupBtn(Transform parent, string id, string title, Vector2 pos, UnityEngine.Events.UnityAction click)
        {
            var img = CreateImage(id, parent, OverlaySprites.Button, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(img);
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(72f, 28f);
            FillLabel(rt, title, 13, OverlaySkin.Text);
            img.gameObject.AddComponent<Button>().onClick.AddListener(click);
        }

        void OpenAvatarSetup()
        {
            EnsureAvatarSetup();
            RefreshAvatarSlots();
            _hideAt = -1f;
            _avatarSetup.SetActive(true);
            _avatarSetup.transform.SetAsLastSibling();
        }

        bool AvatarOverlayOpen =>
            (_avatarSetup != null && _avatarSetup.activeSelf) ||
            (_cropUi != null && _cropUi.gameObject.activeSelf);

        void BeginPickSlot(bool slotA)
        {
            var path = OverlayFileDialog.OpenImage();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            byte[] bytes;
            try
            {
                bytes = System.IO.File.ReadAllBytes(path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Overlay] 读取形象图失败: " + e.Message);
                return;
            }

            EnsureAvatarSetup();
            _hideAt = -1f;
            _cropUi.Open(bytes, png =>
            {
                if (!_view.Settings.TrySetAvatarSlot(slotA, png))
                {
                    Debug.LogWarning("[Overlay] 形象图保存失败。");
                    return;
                }

                _view.ApplyUserSettings();
                _view.NotifyAvatarPresenceChanged();
                RefreshAvatarSlots();
                RefreshLabels();
            });
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
            if (target.sprite != null && target.sprite.texture != null &&
                target.sprite != OverlaySprites.RoundedSquare)
            {
                var tex = target.sprite.texture;
                Destroy(target.sprite);
                Destroy(tex);
                target.sprite = OverlaySprites.RoundedSquare;
            }

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

        void CloseAvatarOverlays()
        {
            if (_cropUi != null)
            {
                _cropUi.ForceClose();
            }

            if (_avatarSetup != null)
            {
                _avatarSetup.SetActive(false);
            }
        }

        Transform CreatePage(Transform parent, string name)
        {
            var go = CreateEmpty(name, parent);
            Stretch((RectTransform)go.transform);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 6, 10);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go.transform;
        }

        Image AddTabButton(Transform parent, string id, string title)
        {
            var tab = CreateImage(id, parent, OverlaySprites.Button, OverlaySprites.RoundedRect);
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

            if (_tabBar == null)
            {
                return;
            }

            for (var i = 0; i < _tabBar.childCount; i++)
            {
                var tab = _tabBar.GetChild(i);
                PaintTab(tab.GetComponent<Image>(), TabToPage(tab.name) == pageName);
            }
        }

        static void PaintTab(Image image, bool on)
        {
            OverlaySkin.ApplySettingsTab(image, on);
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
            AddRowButton(row, "Minus", "-", 28f, 16);
            var value = CreateRowLabel(row, "Value", "1.0x", 48f, 15);
            AddRowButton(row, "Plus", "+", 28f, 16);
            return value;
        }

        Text AddToggleRow(Transform parent, string id, string title)
        {
            var row = CreateRow(parent, id, RowHeight);
            AddRowTitle(row, title);
            var toggle = AddRowButton(row, "Toggle", "开", 56f, 13);
            return toggle.GetComponentInChildren<Text>();
        }

        Text AddChoiceRow(Transform parent, string id, string title)
        {
            var row = CreateRow(parent, id, RowHeight);
            AddRowTitle(row, title);
            var choice = AddRowButton(row, "Choice", "弹性", 72f, 13, accent: true);
            return choice.GetComponentInChildren<Text>();
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
            var button = CreateImage(id, parent, danger ? OverlaySprites.Danger : OverlaySprites.Accent,
                OverlaySprites.RoundedRect);
            button.raycastTarget = true;
            OverlaySkin.ApplySettingsButton(button, on: !danger, danger: danger);
            var le = button.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = ActionRowHeight;
            le.minHeight = ActionRowHeight;
            le.flexibleHeight = 0f;
            FillLabel(button.rectTransform, title, 14, OverlaySkin.SettingsText);
            button.gameObject.AddComponent<Button>();
        }

        static RectTransform CreateRow(Transform parent, string name, float height)
        {
            var go = CreateEmpty(name, parent);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
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

        Image AddRowButton(Transform row, string id, string title, float width, int fontSize, bool accent = false)
        {
            var button = CreateImage(id, row, OverlaySprites.Button, OverlaySprites.RoundedRect);
            if (id == "Toggle")
            {
                OverlaySkin.ApplySettingsToggle(button, false);
            }
            else
            {
                OverlaySkin.ApplySettingsButton(button, on: accent);
            }

            button.raycastTarget = true;
            var le = button.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
            le.minHeight = 26f;
            le.preferredHeight = 26f;
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

        void HoverEnterFromButton()
        {
            _hoverButton = true;
            _hideAt = -1f;
            ScheduleShow();
        }

        void HoverLeaveFromButton()
        {
            _hoverButton = false;
            _showAt = -1f;
            ScheduleHide();
        }

        void HoverEnterFromCard()
        {
            _hoverCard = true;
            _hideAt = -1f;
        }

        void HoverLeaveFromCard()
        {
            _hoverCard = false;
            ScheduleHide();
        }

        void ScheduleShow()
        {
            if (_panel != null && _panel.activeSelf)
            {
                return;
            }

            var delay = HoverOpenDelay();
            if (delay <= 0f)
            {
                Show();
                return;
            }

            _showAt = Time.unscaledTime + delay;
        }

        void Show()
        {
            if (_panel == null)
            {
                return;
            }

            _showAt = -1f;
            _view?.HideInteractMenu();
            _panel.SetActive(true);
            RefreshLabels();
        }

        public void Hide()
        {
            _hoverButton = false;
            _hoverCard = false;
            _hideAt = -1f;
            _showAt = -1f;
            CloseAvatarOverlays();
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        void ScheduleHide()
        {
            if (_hoverButton || _hoverCard || AvatarOverlayOpen)
            {
                return;
            }

            _hideAt = Time.unscaledTime + 0.22f;
        }

        void Update()
        {
            if (_showAt > 0f && Time.unscaledTime >= _showAt)
            {
                _showAt = -1f;
                if (_hoverButton)
                {
                    Show();
                }
            }

            if (_panel != null && _panel.activeSelf && _hideAt > 0f && Time.unscaledTime >= _hideAt)
            {
                if (!_hoverButton && !_hoverCard && !AvatarOverlayOpen)
                {
                    Hide();
                }
            }
        }

        float HoverOpenDelay()
        {
            return _view != null && _view.Config != null
                ? Mathf.Max(0f, _view.Config.hoverOpenSeconds)
                : 0.2f;
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
            SetToggle(_dragText, settings.DisableDrag);
            SetToggle(_flipText, settings.FlipHorizontal);
            SetToggle(_autoStartText, settings.AutoStart);
            SetToggle(_inputIconsText, settings.ShowInputIcons);
            if (_clickEffectText != null)
            {
                _clickEffectText.text = settings.ClickEffect == OverlayClickEffect.Flip ? "反转" : "弹性";
            }

            if (_avatarStatusText != null)
            {
                var ready = settings.AvatarEnabled;
                var hasA = System.IO.File.Exists(OverlayAvatarCodec.LocalPathA);
                var hasB = System.IO.File.Exists(OverlayAvatarCodec.LocalPathB);
                _avatarStatusText.text = ready
                    ? "形象已启用 v" + settings.AvatarVersion
                    : "形象未启用（闲置:" + (hasA ? "有" : "无") + " 动态:" + (hasB ? "有" : "无") + "）";
                _avatarStatusText.color = OverlaySkin.SettingsMuted;
            }
        }

        static void SetToggle(Text label, bool on)
        {
            if (label == null)
            {
                return;
            }

            label.text = on ? "开" : "关";
            label.color = OverlaySkin.SettingsText;
            var image = label.transform.parent.GetComponent<Image>();
            if (image != null)
            {
                OverlaySkin.ApplySettingsToggle(image, on);
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
            var buttonOffset = new Vector2(chipSize * 0.5f * scale + half + 8f, 8f * scale);
            if (pos.x + buttonOffset.x + half > Screen.width)
            {
                buttonOffset.x = -buttonOffset.x;
            }

            _buttonRt.anchoredPosition = pos + buttonOffset;

            if (_panel != null && _panel.activeSelf && _cardRt != null)
            {
                PlaceCardBeside(pos);
            }
        }

        void PlaceCardBeside(Vector2 avatarPos)
        {
            var size = _cardRt.sizeDelta;
            var chipSize = _view != null && _view.Config != null ? _view.Config.chipSize : 128f;
            var offsetX = chipSize * 0.5f + 8f + size.x * 0.5f;
            if (avatarPos.x + offsetX + size.x * 0.5f > Screen.width - 12f)
            {
                offsetX = -offsetX;
            }

            var cardPos = avatarPos + new Vector2(offsetX, 36f);
            cardPos.x = Mathf.Clamp(cardPos.x, 12f + size.x * 0.5f, Screen.width - 12f - size.x * 0.5f);
            cardPos.y = Mathf.Clamp(cardPos.y, 12f + size.y * 0.5f, Screen.height - 12f - size.y * 0.5f);
            _cardRt.anchoredPosition = cardPos;
        }

        static void HideChildLabels(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == "Label")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        static Image FindImage(Transform root, string path)
        {
            var t = FindNode(root, path);
            return t != null ? t.GetComponent<Image>() : null;
        }

        static Text FindLabel(Transform root, string path)
        {
            var t = FindNode(root, path);
            return t != null ? t.GetComponent<Text>() : null;
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
