using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class OverlaySettingsUi : MonoBehaviour
    {
        enum SettingsTab
        {
            Game,
            System
        }

        FriendOverlayView _view;
        GameObject _panel;
        GameObject _gamePage;
        GameObject _systemPage;
        RectTransform _buttonRt;
        RectTransform _cardRt;
        Image _gameTabImage;
        Image _systemTabImage;
        Text _scaleText;
        Text _topmostText;
        Text _dragText;
        Text _flipText;
        Text _autoStartText;
        Text _clickEffectText;
        Text _inputIconsText;
        Text _skinText;
        Text _avatarStatusText;
        Text _avatarSetupStatus;
        Image _slotAImage;
        Image _slotBImage;
        GameObject _avatarSetup;
        OverlayAvatarCropUi _cropUi;
        Text _titleText;
        Image _buttonImage;
        Image _cardImage;
        Image _closeImage;
        SettingsTab _tab = SettingsTab.Game;
        bool _hoverButton;
        bool _hoverCard;
        float _hideAt = -1f;
        float _showAt = -1f;

        public static OverlaySettingsUi Create(Transform chrome, Transform modal, FriendOverlayView view)
        {
            var root = new GameObject("SettingsUi", typeof(RectTransform));
            root.transform.SetParent(chrome, false);
            Stretch((RectTransform)root.transform);
            var ui = root.AddComponent<OverlaySettingsUi>();
            ui._view = view;
            ui.Build(modal);
            return ui;
        }

        void Build(Transform modal)
        {
            _buttonImage = CreateImage("SettingsButton", transform, OverlaySprites.Panel, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyPanel(_buttonImage);
            _buttonImage.raycastTarget = true;
            _buttonRt = _buttonImage.rectTransform;
            _buttonRt.anchorMin = new Vector2(0f, 0f);
            _buttonRt.anchorMax = new Vector2(0f, 0f);
            _buttonRt.pivot = new Vector2(0.5f, 0.5f);
            _buttonRt.sizeDelta = new Vector2(56f, 28f);
            FillLabel(_buttonRt, "设置", 14, OverlaySkin.Text);
            OverlayHoverRelay.Bind(_buttonImage.gameObject, HoverEnterFromButton, HoverLeaveFromButton);

            _panel = new GameObject("SettingsPanel", typeof(RectTransform));
            _panel.transform.SetParent(modal != null ? modal : transform, false);
            Stretch((RectTransform)_panel.transform);
            _panel.SetActive(false);

            var dim = CreateImage("Dim", _panel.transform, new Color(0f, 0f, 0f, 0.22f), OverlaySprites.RoundedRect);
            dim.raycastTarget = false;
            Stretch(dim.rectTransform);

            _cardImage = CreateImage("Card", _panel.transform, OverlaySprites.Panel, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyPanel(_cardImage);
            _cardImage.raycastTarget = true;
            OverlayHoverRelay.Bind(_cardImage.gameObject, HoverEnterFromCard, HoverLeaveFromCard);
            _cardRt = _cardImage.rectTransform;
            _cardRt.anchorMin = new Vector2(0f, 0f);
            _cardRt.anchorMax = new Vector2(0f, 0f);
            _cardRt.pivot = new Vector2(0.5f, 0.5f);
            _cardRt.sizeDelta = new Vector2(280f, 520f);

            _titleText = PlaceLabel(_cardRt, "设置", 20, OverlaySkin.Text, new Vector2(0f, 218f), new Vector2(180f, 28f));

            _closeImage = CreateImage("Close", _cardRt, OverlaySprites.Button, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(_closeImage);
            _closeImage.raycastTarget = true;
            var close = _closeImage;
            var closeRt = close.rectTransform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-10f, -10f);
            closeRt.sizeDelta = new Vector2(48f, 28f);
            FillLabel(closeRt, "关闭", 13, OverlaySkin.Text);
            close.gameObject.AddComponent<Button>().onClick.AddListener(Hide);

            _gameTabImage = AddTabButton(_cardRt, "游戏", new Vector2(-58f, 178f), () => ShowTab(SettingsTab.Game));
            _systemTabImage = AddTabButton(_cardRt, "系统", new Vector2(58f, 178f), () => ShowTab(SettingsTab.System));

            _gamePage = CreatePage(_cardRt, "GamePage");
            _systemPage = CreatePage(_cardRt, "SystemPage");
            BuildGamePage(_gamePage.transform);
            BuildSystemPage(_systemPage.transform);
            ShowTab(SettingsTab.Game);
            ApplySkin();
        }

        public void ApplySkin()
        {
            OverlaySkin.ApplyPanel(_buttonImage);
            OverlaySkin.ApplyPanel(_cardImage);
            OverlaySkin.ApplyButton(_closeImage);
            if (_cardRt != null)
            {
                var images = _cardRt.GetComponentsInChildren<Image>(true);
                for (var i = 0; i < images.Length; i++)
                {
                    var image = images[i];
                    if (image == _cardImage || image == _closeImage || image == _gameTabImage || image == _systemTabImage)
                    {
                        continue;
                    }

                    var name = image.gameObject.name;
                    if (name == "退出游戏")
                    {
                        OverlaySkin.ApplyButton(image, danger: true);
                    }
                    else if (name.Contains("Choice") || name == "复位头像位置" || name == "复位缩放")
                    {
                        OverlaySkin.ApplyButton(image, accent: true);
                    }
                    else
                    {
                        OverlaySkin.ApplyButton(image);
                    }
                }
            }

            PaintTexts();
            ShowTab(_tab);
            RefreshLabels();
        }

        void PaintTexts()
        {
            if (_titleText != null)
            {
                _titleText.color = OverlaySkin.Text;
            }

            PaintSubtreeTexts(_buttonRt);
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
                    ? OverlaySkin.TextMuted
                    : OverlaySkin.Text;
            }
        }

        void BuildGamePage(Transform parent)
        {
            var y = 136f;
            _scaleText = AddScaleRow(parent, ref y);
            _dragText = AddToggleRow(parent, "禁止拖动", ref y, () =>
            {
                _view.Settings.SetDisableDrag(!_view.Settings.DisableDrag);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            _flipText = AddToggleRow(parent, "水平翻转", ref y, () =>
            {
                _view.Settings.SetFlipHorizontal(!_view.Settings.FlipHorizontal);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            _clickEffectText = AddChoiceRow(parent, "点击效果", ref y, () =>
            {
                _view.Settings.CycleClickEffect();
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            _inputIconsText = AddToggleRow(parent, "按键图标", ref y, () =>
            {
                _view.Settings.SetShowInputIcons(!_view.Settings.ShowInputIcons);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            _skinText = AddChoiceRow(parent, "界面皮肤", ref y, () =>
            {
                _view.Settings.CycleUiSkin();
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            _avatarStatusText = PlaceLabel(parent, "动态形象", 13, OverlaySkin.TextMuted, new Vector2(0f, y),
                new Vector2(240f, 20f));
            y -= 28f;
            AddActionButton(parent, "设置动态图", ref y, OpenAvatarSetup);
            AddActionButton(parent, "复位头像位置", ref y, () => _view.ResetVisibleToDefault());
            AddActionButton(parent, "复位缩放", ref y, () =>
            {
                _view.ResetScale();
                RefreshLabels();
            });
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

            AddSetupBtn(cardRt, "清除", new Vector2(-50f, -90f), () =>
            {
                _view.Settings.ClearAvatarPresence();
                _view.ApplyUserSettings();
                _view.NotifyAvatarPresenceChanged();
                RefreshAvatarSlots();
                RefreshLabels();
            });
            AddSetupBtn(cardRt, "关闭", new Vector2(50f, -90f), () => _avatarSetup.SetActive(false));
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

        void AddSetupBtn(Transform parent, string title, Vector2 pos, UnityEngine.Events.UnityAction click)
        {
            var img = CreateImage(title, parent, OverlaySprites.Button, OverlaySprites.RoundedRect);
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
            _avatarSetup.SetActive(true);
            _avatarSetup.transform.SetAsLastSibling();
        }

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

        void BuildSystemPage(Transform parent)
        {
            var y = 86f;
            _topmostText = AddToggleRow(parent, "始终置顶", ref y, () =>
            {
                _view.Settings.SetAlwaysOnTop(!_view.Settings.AlwaysOnTop);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            _autoStartText = AddToggleRow(parent, "开机自启", ref y, () =>
            {
                _view.Settings.SetAutoStart(!_view.Settings.AutoStart);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            AddQuitButton(parent, ref y);
        }

        static GameObject CreatePage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            return go;
        }

        Image AddTabButton(Transform parent, string title, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var tab = CreateImage(title + "Tab", parent, OverlaySprites.Button, OverlaySprites.RoundedRect);
            tab.raycastTarget = true;
            var rt = tab.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(104f, 28f);
            FillLabel(rt, title, 14, OverlaySkin.Text);
            tab.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            return tab;
        }

        void ShowTab(SettingsTab tab)
        {
            _tab = tab;
            var game = tab == SettingsTab.Game;
            if (_gamePage != null)
            {
                _gamePage.SetActive(game);
            }

            if (_systemPage != null)
            {
                _systemPage.SetActive(!game);
            }

            PaintTab(_gameTabImage, game);
            PaintTab(_systemTabImage, !game);
        }

        static void PaintTab(Image image, bool on)
        {
            OverlaySkin.ApplyButton(image, accent: on);
        }

        void AddQuitButton(Transform parent, ref float y)
        {
            var button = CreateImage("退出游戏", parent, OverlaySprites.Danger, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(button, danger: true);
            button.raycastTarget = true;
            var rt = button.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(200f, 32f);
            FillLabel(rt, "退出游戏", 14, OverlaySkin.Text);
            button.gameObject.AddComponent<Button>().onClick.AddListener(QuitGame);
            y -= 40f;
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        Text AddScaleRow(Transform parent, ref float y)
        {
            PlaceLabel(parent, "放大倍数", 14, OverlaySkin.TextMuted, new Vector2(-70f, y), new Vector2(100f, 24f), true);
            AddSmallButton(parent, "-", new Vector2(20f, y), () =>
            {
                _view.Settings.AddScale(-0.1f);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            var value = PlaceLabel(parent, "1.0x", 15, OverlaySkin.Text, new Vector2(70f, y), new Vector2(56f, 24f));
            AddSmallButton(parent, "+", new Vector2(118f, y), () =>
            {
                _view.Settings.AddScale(0.1f);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            y -= 40f;
            return value;
        }

        Text AddToggleRow(Transform parent, string title, ref float y, UnityEngine.Events.UnityAction onClick)
        {
            PlaceLabel(parent, title, 14, OverlaySkin.Text, new Vector2(-50f, y), new Vector2(140f, 24f));
            var toggle = CreateImage(title + "Toggle", parent, OverlaySprites.Button, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(toggle);
            toggle.raycastTarget = true;
            var rt = toggle.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(90f, y);
            rt.sizeDelta = new Vector2(52f, 26f);
            var label = FillLabel(rt, "开", 13, OverlaySkin.Text);
            toggle.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            y -= 36f;
            return label;
        }

        Text AddChoiceRow(Transform parent, string title, ref float y, UnityEngine.Events.UnityAction onClick)
        {
            PlaceLabel(parent, title, 14, OverlaySkin.Text, new Vector2(-50f, y), new Vector2(140f, 24f));
            var choice = CreateImage(title + "Choice", parent, OverlaySprites.Accent, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(choice, accent: true);
            choice.raycastTarget = true;
            var rt = choice.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(90f, y);
            rt.sizeDelta = new Vector2(72f, 26f);
            var label = FillLabel(rt, "弹性", 13, OverlaySkin.Text);
            choice.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            y -= 36f;
            return label;
        }

        void AddActionButton(Transform parent, string title, ref float y, UnityEngine.Events.UnityAction onClick)
        {
            var button = CreateImage(title, parent, OverlaySprites.Accent, OverlaySprites.RoundedRect);
            button.raycastTarget = true;
            var rt = button.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(200f, 32f);
            OverlaySkin.ApplyButton(button, accent: title != "退出游戏", danger: title == "退出游戏");
            FillLabel(rt, title, 14, OverlaySkin.Text);
            button.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            y -= 40f;
        }

        void AddSmallButton(Transform parent, string title, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var button = CreateImage(title, parent, OverlaySprites.Button, OverlaySprites.RoundedRect);
            button.raycastTarget = true;
            var rt = button.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(28f, 26f);
            OverlaySkin.ApplyButton(button);
            FillLabel(rt, title, 16, OverlaySkin.Text);
            button.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
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
            if (_hoverButton || _hoverCard)
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
                if (!_hoverButton && !_hoverCard)
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
            var settings = _view.Settings;
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

            if (_skinText != null)
            {
                _skinText.text = OverlaySkin.Label;
            }

            if (_avatarStatusText != null)
            {
                var ready = settings.AvatarEnabled;
                var hasA = System.IO.File.Exists(OverlayAvatarCodec.LocalPathA);
                var hasB = System.IO.File.Exists(OverlayAvatarCodec.LocalPathB);
                _avatarStatusText.text = ready
                    ? "形象已启用 v" + settings.AvatarVersion
                    : "形象未启用（闲置:" + (hasA ? "有" : "无") + " 动态:" + (hasB ? "有" : "无") + "）";
                _avatarStatusText.color = OverlaySkin.TextMuted;
            }
        }

        static void SetToggle(Text label, bool on)
        {
            if (label == null)
            {
                return;
            }

            label.text = on ? "开" : "关";
            label.color = OverlaySkin.Text;
            var image = label.transform.parent.GetComponent<Image>();
            if (image != null)
            {
                OverlaySkin.ApplyButton(image, accent: on);
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
            var buttonOffset = new Vector2(chipSize * 0.5f * scale + 28f, 12f * scale);
            if (pos.x + buttonOffset.x + 40f > Screen.width)
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

        static Text PlaceLabel(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 sizeDelta, bool muted = false)
        {
            var label = CreateLabel(parent, text, size, color);
            label.gameObject.name = muted ? "Muted" : "Label";
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
