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
            _avatarStatusText = PlaceLabel(parent, "形象 A/B", 13, OverlaySkin.TextMuted, new Vector2(0f, y),
                new Vector2(240f, 20f));
            y -= 28f;
            AddActionButton(parent, "上传闲置图 A", ref y, () => PickAvatar(true));
            AddActionButton(parent, "上传活动图 B", ref y, () => PickAvatar(false));
            AddActionButton(parent, "清除形象图", ref y, () =>
            {
                _view.Settings.ClearAvatarPresence();
                _view.ApplyUserSettings();
                _view.NotifyAvatarPresenceChanged();
                RefreshLabels();
            });
            AddActionButton(parent, "复位头像位置", ref y, () => _view.ResetVisibleToDefault());
            AddActionButton(parent, "复位缩放", ref y, () =>
            {
                _view.ResetScale();
                RefreshLabels();
            });
        }

        void PickAvatar(bool slotA)
        {
            var path = OverlayFileDialog.OpenImage();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                if (!_view.Settings.TrySetAvatarSlot(slotA, bytes))
                {
                    Debug.LogWarning("[Overlay] 形象图无效或过大，未能保存。");
                    return;
                }

                _view.ApplyUserSettings();
                _view.NotifyAvatarPresenceChanged();
                RefreshLabels();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Overlay] 读取形象图失败: " + e.Message);
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
                    : "形象未启用（A:" + (hasA ? "有" : "无") + " B:" + (hasB ? "有" : "无") + "）";
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
