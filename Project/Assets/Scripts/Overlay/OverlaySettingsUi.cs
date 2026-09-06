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
        const float CardWidth = 300f;
        const float CardHeight = 540f;
        const float HeaderHeight = 44f;
        const float TabBarHeight = 36f;
        const float RowHeight = 32f;
        const float ActionRowHeight = 36f;
        const float GearSize = 32f;
        const float CloseSize = 32f;
        static readonly string[] StyleResources =
        {
            "Overlay/UI/rounded_rect",
            "Overlay/UI/rounded_rect_style2",
            "Overlay/UI/rounded_rect_style3",
            "Overlay/UI/rounded_rect_style4"
        };

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
        [SerializeField] Text _inputIconsText;
        [SerializeField] Text _avatarStatusText;
        Text _settingsStyleText;
        readonly List<Image> _settingsStyleImages = new List<Image>();
        Sprite[] _settingsStyleSprites;
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
        bool _hoverButton;
        bool _hoverCard;
        float _hideAt = -1f;
        float _showAt = -1f;

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
            ui.ApplySettingsStyle();
            ui.ShowPage(DefaultPage);
            ui.Hide();
            return ui;
        }

#if UNITY_EDITOR
        public void EditorPopulate()
        {
            Build(null);
        }

        public Transform EditorEnsureSkinStyleRow()
        {
            var existing = FindNode(_cardRt, "Pages/GamePage/SkinStyleRow");
            if (existing != null)
            {
                return existing;
            }

            var gamePage = FindNode(_cardRt, "Pages/GamePage");
            if (gamePage == null)
            {
                return null;
            }

            AddActionRow(gamePage, "SkinStyleRow", "皮肤风格 1");
            return FindNode(_cardRt, "Pages/GamePage/SkinStyleRow");
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
            BindClick(FindNode(_cardRt, "Pages/GamePage/InputIconsRow/Toggle"), () =>
            {
                _view.Settings.SetShowInputIcons(!_view.Settings.ShowInputIcons);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            var styleRow = FindNode(_cardRt, "Pages/GamePage/SkinStyleRow");
            _settingsStyleText = styleRow != null ? styleRow.GetComponentInChildren<Text>(true) : null;
            BindClick(styleRow, () =>
            {
                _view.Settings.CycleSettingsStyle();
                ApplySettingsStyle();
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

            _cardImage = CreateImage("Background", _panel.transform, OverlaySprites.Panel, OverlaySprites.RoundedRect);
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

            var title = PlaceLabel(headerRt, "设置", 18, OverlaySkin.SettingsText, Vector2.zero, new Vector2(160f, 28f));
            title.gameObject.name = "Title";

            _closeImage = CreateImage("Close", headerRt, OverlaySprites.Button, OverlaySprites.RoundedRect);
            _closeImage.raycastTarget = true;
            var closeRt = _closeImage.rectTransform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-10f, 0f);
            closeRt.sizeDelta = new Vector2(CloseSize, CloseSize);
            FillLabel(closeRt, "×", 18, OverlaySkin.Text);
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
            _inputIconsText = AddToggleRow(gamePage, "InputIconsRow", "按键图标");
            AddActionRow(gamePage, "SkinStyleRow", "皮肤风格 1");
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
            _hideAt = -1f;

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

            if (_tabBar != null)
            {
                for (var i = 0; i < _tabBar.childCount; i++)
                {
                    var tab = _tabBar.GetChild(i);
                    var image = tab.GetComponent<Image>();
                    OverlaySprites.StyleFill(
                        image,
                        TabToPage(tab.name) == pageName ? OverlaySprites.Accent : OverlaySprites.Button);
                }
            }
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

        Image AddRowButton(Transform row, string id, string title, float width, int fontSize)
        {
            var button = CreateImage(id, row, OverlaySprites.Button, OverlaySprites.RoundedRect);
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
            if (_settingsStyleText != null)
            {
                _settingsStyleText.text = "皮肤风格 " + settings.SettingsStyle;
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

        void ApplySettingsStyle()
        {
            var settings = _view != null ? _view.Settings : null;
            if (settings == null || _cardRt == null)
            {
                return;
            }

            if (_settingsStyleSprites == null)
            {
                _settingsStyleSprites = new Sprite[StyleResources.Length];
                for (var i = 0; i < StyleResources.Length; i++)
                {
                    _settingsStyleSprites[i] = Resources.Load<Sprite>(StyleResources[i]);
                }
            }

            if (_settingsStyleImages.Count == 0)
            {
                var images = _cardRt.GetComponentsInChildren<Image>(true);
                for (var i = 0; i < images.Length; i++)
                {
                    var sprite = images[i].sprite;
                    if (sprite != null && (sprite.name == "rounded_rect" ||
                                           sprite.name.StartsWith("rounded_rect_style")))
                    {
                        _settingsStyleImages.Add(images[i]);
                    }
                }
            }

            var index = Mathf.Clamp(settings.SettingsStyle - 1, 0, _settingsStyleSprites.Length - 1);
            var selected = _settingsStyleSprites[index];
            if (selected == null)
            {
                Debug.LogWarning("[Overlay] 缺少设置皮肤资源: Resources/" + StyleResources[index]);
                return;
            }

            for (var i = 0; i < _settingsStyleImages.Count; i++)
            {
                if (_settingsStyleImages[i] != null)
                {
                    _settingsStyleImages[i].sprite = selected;
                }
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
            OverlaySprites.StyleFill(
                label.transform.parent.GetComponent<Image>(),
                on ? OverlaySprites.Accent : OverlaySprites.Button);
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
