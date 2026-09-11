using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// 全局右键长按隐身 / 取消隐身：驱动桌上组件 CanvasGroup 与鼠标旁环形进度。
    /// </summary>
    public sealed class OverlayStealthController : MonoBehaviour
    {
        public const float HoldSeconds = 2f;
        const float RingSize = 22f;
        const float RingFillSize = 19f;
        const float RingHoleSize = 13f;

        enum Phase
        {
            Visible,
            Hiding,
            Hidden,
            Showing
        }

        FriendOverlayView _view;
        OverlayInputWatcher _input;
        CanvasGroup _content;
        RectTransform _hudRoot;
        Image _ringFill;
        Text _label;
        Phase _phase = Phase.Visible;
        bool _holding;
        float _elapsed;
        System.Action _hiddenChanged;

        public bool IsHidden => _phase == Phase.Hidden || _phase == Phase.Showing;

        public void Bind(FriendOverlayView view, OverlayInputWatcher input, CanvasGroup content, RectTransform hudLayer)
        {
            _view = view;
            _input = input;
            _content = content;
            BuildHud(hudLayer);
            ApplyAlpha(1f, interactive: true);
            SetHud(false, 0f, null);
        }

        public void SetHiddenChangedHandler(System.Action handler)
        {
            _hiddenChanged = handler;
        }

        public void RevealNow()
        {
            _holding = false;
            _elapsed = 0f;
            SetPhase(Phase.Visible);
            ApplyAlpha(1f, interactive: true);
            SetHud(false, 0f, null);
        }

        void Update()
        {
            if (_input == null || _content == null)
            {
                return;
            }

            var held = _input.IsRightButtonHeld;
            if (held)
            {
                if (!_holding)
                {
                    _holding = true;
                    _elapsed = 0f;
                    if (_phase == Phase.Hidden || _phase == Phase.Showing)
                    {
                        SetPhase(Phase.Showing);
                    }
                    else
                    {
                        _view?.CloseTransientPanels();
                        SetPhase(Phase.Hiding);
                    }
                }

                _elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(_elapsed / HoldSeconds);
                if (_phase == Phase.Hiding)
                {
                    ApplyAlpha(1f - t, interactive: t < 0.999f);
                    SetHud(true, t, "隐藏");
                    if (t >= 1f)
                    {
                        SetPhase(Phase.Hidden);
                        ApplyAlpha(0f, interactive: false);
                        SetHud(false, 0f, null);
                        _holding = true;
                        _elapsed = HoldSeconds;
                    }
                }
                else if (_phase == Phase.Showing)
                {
                    ApplyAlpha(t, interactive: t > 0.001f);
                    SetHud(true, t, "取消隐藏");
                    if (t >= 1f)
                    {
                        SetPhase(Phase.Visible);
                        ApplyAlpha(1f, interactive: true);
                        SetHud(false, 0f, null);
                        _holding = true;
                        _elapsed = HoldSeconds;
                    }
                }

                FollowCursor();
            }
            else if (_holding)
            {
                _holding = false;
                if (_phase == Phase.Hiding || _phase == Phase.Showing)
                {
                    SetPhase(Phase.Visible);
                    ApplyAlpha(1f, interactive: true);
                    SetHud(false, 0f, null);
                }

                _elapsed = 0f;
            }
        }

        void SetPhase(Phase next)
        {
            var wasHiddenMenu = IsHidden;
            _phase = next;
            if (wasHiddenMenu != IsHidden)
            {
                _hiddenChanged?.Invoke();
            }
        }

        void ApplyAlpha(float alpha, bool interactive)
        {
            _content.alpha = alpha;
            _content.interactable = interactive;
            _content.blocksRaycasts = interactive;
        }

        void BuildHud(RectTransform hudLayer)
        {
            if (hudLayer == null)
            {
                return;
            }

            var root = new GameObject("StealthHud", typeof(RectTransform));
            root.transform.SetParent(hudLayer, false);
            _hudRoot = (RectTransform)root.transform;
            _hudRoot.sizeDelta = new Vector2(88f, 88f);
            _hudRoot.pivot = new Vector2(0.5f, 0.5f);

            var trackGo = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            trackGo.transform.SetParent(_hudRoot, false);
            var track = trackGo.GetComponent<Image>();
            track.sprite = OverlaySprites.Circle;
            track.type = Image.Type.Simple;
            track.color = new Color(0f, 0f, 0f, 0.45f);
            track.raycastTarget = false;
            var trackRt = track.rectTransform;
            trackRt.anchorMin = trackRt.anchorMax = new Vector2(0.5f, 0.5f);
            trackRt.sizeDelta = new Vector2(RingSize, RingSize);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(_hudRoot, false);
            _ringFill = fillGo.GetComponent<Image>();
            _ringFill.sprite = OverlaySprites.Circle;
            _ringFill.type = Image.Type.Filled;
            _ringFill.fillMethod = Image.FillMethod.Radial360;
            _ringFill.fillOrigin = (int)Image.Origin360.Top;
            _ringFill.fillClockwise = true;
            _ringFill.color = OverlaySprites.Accent;
            _ringFill.raycastTarget = false;
            var fillRt = _ringFill.rectTransform;
            fillRt.anchorMin = fillRt.anchorMax = new Vector2(0.5f, 0.5f);
            fillRt.sizeDelta = new Vector2(RingFillSize, RingFillSize);

            // Punch a soft hole: center disc so fill reads as a ring.
            var holeGo = new GameObject("Hole", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            holeGo.transform.SetParent(_hudRoot, false);
            var hole = holeGo.GetComponent<Image>();
            hole.sprite = OverlaySprites.Circle;
            hole.color = new Color(0.08f, 0.09f, 0.11f, 0.92f);
            hole.raycastTarget = false;
            var holeRt = hole.rectTransform;
            holeRt.anchorMin = new Vector2(0.5f, 0.5f);
            holeRt.anchorMax = new Vector2(0.5f, 0.5f);
            holeRt.sizeDelta = new Vector2(RingHoleSize, RingHoleSize);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(_hudRoot, false);
            _label = labelGo.GetComponent<Text>();
            _label.font = OverlaySprites.UiFont;
            _label.fontSize = 12;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.raycastTarget = false;
            var labelRt = _label.rectTransform;
            labelRt.anchorMin = new Vector2(0.5f, 0.5f);
            labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.sizeDelta = new Vector2(72f, 40f);
            labelRt.anchoredPosition = new Vector2(0f, -24f);

            _hudRoot.gameObject.SetActive(false);
        }

        void SetHud(bool visible, float progress, string text)
        {
            if (_hudRoot == null)
            {
                return;
            }

            _hudRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (_ringFill != null)
            {
                _ringFill.fillAmount = Mathf.Clamp01(progress);
                _ringFill.color = OverlaySprites.Accent;
            }

            if (_label != null && text != null)
            {
                _label.text = text;
            }

            FollowCursor();
        }

        void FollowCursor()
        {
            if (_hudRoot == null || !_hudRoot.gameObject.activeSelf)
            {
                return;
            }

            var pos = (Vector2)Input.mousePosition + new Vector2(18f, 18f);
            pos.x = Mathf.Clamp(pos.x, 48f, Screen.width - 48f);
            pos.y = Mathf.Clamp(pos.y, 64f, Screen.height - 48f);
            _hudRoot.position = pos;
        }
    }
}
