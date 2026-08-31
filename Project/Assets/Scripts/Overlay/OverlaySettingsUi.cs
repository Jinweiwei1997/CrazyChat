using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class OverlaySettingsUi : MonoBehaviour
    {
        static readonly Color TabOn = new Color(0.28f, 0.48f, 0.86f, 1f);
        static readonly Color TabOff = new Color(1f, 1f, 1f, 0.12f);

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
        SettingsTab _tab = SettingsTab.Game;

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
            var button = CreateImage("SettingsButton", transform, new Color(0.12f, 0.13f, 0.16f, 0.88f), OverlaySprites.RoundedRect);
            button.raycastTarget = true;
            _buttonRt = button.rectTransform;
            _buttonRt.anchorMin = new Vector2(0f, 0f);
            _buttonRt.anchorMax = new Vector2(0f, 0f);
            _buttonRt.pivot = new Vector2(0.5f, 0.5f);
            _buttonRt.sizeDelta = new Vector2(56f, 28f);
            FillLabel(_buttonRt, "设置", 15, Color.white);
            button.gameObject.AddComponent<Button>().onClick.AddListener(Toggle);

            _panel = new GameObject("SettingsPanel", typeof(RectTransform));
            _panel.transform.SetParent(modal != null ? modal : transform, false);
            Stretch((RectTransform)_panel.transform);
            _panel.SetActive(false);

            var dim = CreateImage("Dim", _panel.transform, new Color(0f, 0f, 0f, 0.35f), OverlaySprites.RoundedRect);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Hide);

            var card = CreateImage("Card", _panel.transform, new Color(0.12f, 0.13f, 0.16f, 0.96f), OverlaySprites.RoundedRect);
            card.raycastTarget = true;
            _cardRt = card.rectTransform;
            _cardRt.anchorMin = new Vector2(0f, 0f);
            _cardRt.anchorMax = new Vector2(0f, 0f);
            _cardRt.pivot = new Vector2(0.5f, 0.5f);
            _cardRt.sizeDelta = new Vector2(280f, 436f);

            PlaceLabel(_cardRt, "设置", 20, Color.white, new Vector2(0f, 186f), new Vector2(180f, 28f));

            var close = CreateImage("Close", _cardRt, new Color(1f, 1f, 1f, 0.12f), OverlaySprites.RoundedRect);
            close.raycastTarget = true;
            var closeRt = close.rectTransform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-10f, -10f);
            closeRt.sizeDelta = new Vector2(48f, 28f);
            FillLabel(closeRt, "关闭", 13, Color.white);
            close.gameObject.AddComponent<Button>().onClick.AddListener(Hide);

            _gameTabImage = AddTabButton(_cardRt, "游戏", new Vector2(-58f, 146f), () => ShowTab(SettingsTab.Game));
            _systemTabImage = AddTabButton(_cardRt, "系统", new Vector2(58f, 146f), () => ShowTab(SettingsTab.System));

            _gamePage = CreatePage(_cardRt, "GamePage");
            _systemPage = CreatePage(_cardRt, "SystemPage");
            BuildGamePage(_gamePage.transform);
            BuildSystemPage(_systemPage.transform);
            ShowTab(SettingsTab.Game);
        }

        void BuildGamePage(Transform parent)
        {
            var y = 104f;
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
            AddActionButton(parent, "复位头像位置", ref y, () => _view.ResetVisibleToDefault());
            AddActionButton(parent, "复位缩放", ref y, () =>
            {
                _view.ResetScale();
                RefreshLabels();
            });
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
            var tab = CreateImage(title + "Tab", parent, TabOff, OverlaySprites.RoundedRect);
            tab.raycastTarget = true;
            var rt = tab.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(104f, 28f);
            FillLabel(rt, title, 14, Color.white);
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
            if (image != null)
            {
                image.color = on ? TabOn : TabOff;
            }
        }

        void AddQuitButton(Transform parent, ref float y)
        {
            var button = CreateImage("退出游戏", parent, new Color(0.78f, 0.28f, 0.28f, 1f), OverlaySprites.RoundedRect);
            button.raycastTarget = true;
            var rt = button.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(200f, 32f);
            FillLabel(rt, "退出游戏", 14, Color.white);
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
            PlaceLabel(parent, "放大倍数", 14, new Color(1f, 1f, 1f, 0.7f), new Vector2(-70f, y), new Vector2(100f, 24f));
            AddSmallButton(parent, "-", new Vector2(20f, y), () =>
            {
                _view.Settings.AddScale(-0.1f);
                _view.ApplyUserSettings();
                RefreshLabels();
            });
            var value = PlaceLabel(parent, "1.0x", 15, Color.white, new Vector2(70f, y), new Vector2(56f, 24f));
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
            PlaceLabel(parent, title, 14, new Color(1f, 1f, 1f, 0.85f), new Vector2(-50f, y), new Vector2(140f, 24f));
            var toggle = CreateImage(title + "Toggle", parent, new Color(1f, 1f, 1f, 0.12f), OverlaySprites.RoundedRect);
            toggle.raycastTarget = true;
            var rt = toggle.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(90f, y);
            rt.sizeDelta = new Vector2(52f, 26f);
            var label = FillLabel(rt, "开", 13, Color.white);
            toggle.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            y -= 36f;
            return label;
        }

        Text AddChoiceRow(Transform parent, string title, ref float y, UnityEngine.Events.UnityAction onClick)
        {
            PlaceLabel(parent, title, 14, new Color(1f, 1f, 1f, 0.85f), new Vector2(-50f, y), new Vector2(140f, 24f));
            var choice = CreateImage(title + "Choice", parent, new Color(0.28f, 0.48f, 0.86f, 1f), OverlaySprites.RoundedRect);
            choice.raycastTarget = true;
            var rt = choice.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(90f, y);
            rt.sizeDelta = new Vector2(64f, 26f);
            var label = FillLabel(rt, "弹性", 13, Color.white);
            choice.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            y -= 36f;
            return label;
        }

        void AddActionButton(Transform parent, string title, ref float y, UnityEngine.Events.UnityAction onClick)
        {
            var button = CreateImage(title, parent, new Color(0.28f, 0.48f, 0.86f, 1f), OverlaySprites.RoundedRect);
            button.raycastTarget = true;
            var rt = button.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(200f, 32f);
            FillLabel(rt, title, 14, Color.white);
            button.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            y -= 40f;
        }

        void AddSmallButton(Transform parent, string title, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var button = CreateImage(title, parent, new Color(1f, 1f, 1f, 0.14f), OverlaySprites.RoundedRect);
            button.raycastTarget = true;
            var rt = button.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(28f, 26f);
            FillLabel(rt, title, 16, Color.white);
            button.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
        }

        void Toggle()
        {
            if (_panel.activeSelf)
            {
                Hide();
                return;
            }

            _view?.HideInteractMenu();
            _panel.SetActive(true);
            RefreshLabels();
        }

        public void Hide()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
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
        }

        static void SetToggle(Text label, bool on)
        {
            if (label == null)
            {
                return;
            }

            label.text = on ? "开" : "关";
            var image = label.transform.parent.GetComponent<Image>();
            if (image != null)
            {
                image.color = on ? new Color(0.28f, 0.48f, 0.86f, 1f) : new Color(1f, 1f, 1f, 0.12f);
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
