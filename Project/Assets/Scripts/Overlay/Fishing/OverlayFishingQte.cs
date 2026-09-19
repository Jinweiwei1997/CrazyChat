using System;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay.Fishing
{
    /// <summary>Local-only QTE bar (炫舞-style). Not synced to friends.</summary>
    public sealed class OverlayFishingQte : MonoBehaviour
    {
        const float BarWidth = 220f;
        const float BarHeight = 18f;
        const float KnobWidth = 10f;
        const float ZoneFraction = 0.2f;

        FriendOverlayView _view;
        RectTransform _root;
        RectTransform _bar;
        RectTransform _zone;
        RectTransform _knob;
        Image _barImage;
        Image _zoneImage;
        Image _knobImage;
        Action<bool> _onResult;
        bool _active;
        float _deadline = -1f;
        float _knobT;
        float _knobDir = 1f;
        float _zoneStart;
        float _speed = 1.35f;

        public static OverlayFishingQte Create(Transform windowLayer, OverlayFishingController ctrl)
        {
            var go = new GameObject("FishingQte", typeof(RectTransform));
            go.transform.SetParent(windowLayer != null ? windowLayer : ctrl.transform, false);
            var qte = go.AddComponent<OverlayFishingQte>();
            qte.Build();
            go.SetActive(false);
            return qte;
        }

        public void BindView(FriendOverlayView view) => _view = view;

        public void Show(FriendAvatarChip chip, Action<bool> onResult)
        {
            if (chip == null)
            {
                onResult?.Invoke(false);
                return;
            }

            _onResult = onResult;
            _active = true;
            var cfg = _view != null ? _view.Config : null;
            var timeout = cfg != null ? Mathf.Max(0.5f, cfg.fishingQteSeconds) : 5f;
            _deadline = Time.unscaledTime + timeout;
            _knobT = 0f;
            _knobDir = 1f;
            _zoneStart = UnityEngine.Random.Range(0f, 1f - ZoneFraction);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ApplyTheme();
            Follow(chip);
            LayoutZone();
        }

        public void Hide()
        {
            _active = false;
            _onResult = null;
            _deadline = -1f;
            if (gameObject != null) gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_active) return;
            var chip = _view != null ? _view.LocalChip : null;
            if (chip == null)
            {
                Finish(false);
                return;
            }

            Follow(chip);

            if (_deadline > 0f && Time.unscaledTime >= _deadline)
            {
                Finish(false);
                return;
            }

            _knobT += Time.unscaledDeltaTime * _speed * _knobDir;
            if (_knobT >= 1f)
            {
                _knobT = 1f;
                _knobDir = -1f;
            }
            else if (_knobT <= 0f)
            {
                _knobT = 0f;
                _knobDir = 1f;
            }

            if (_knob != null)
                _knob.anchoredPosition = new Vector2((_knobT - 0.5f) * (BarWidth - KnobWidth), 0f);

            if (Input.GetMouseButtonDown(0))
            {
                // Only settle when press is on the QTE panel; outside clicks do not fail early.
                if (!IsPointerOverPanel()) return;
                var hit = _knobT >= _zoneStart && _knobT <= _zoneStart + ZoneFraction;
                Finish(hit);
            }
        }

        void Finish(bool success)
        {
            if (!_active) return;
            var cb = _onResult;
            Hide();
            cb?.Invoke(success);
        }

        void Build()
        {
            _root = (RectTransform)transform;
            _root.anchorMin = _root.anchorMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(BarWidth + 24f, BarHeight + 28f);

            _barImage = CreateImage("Bar", _root, Color.white, OverlayFishingArt.QteBar());
            _barImage.raycastTarget = true;
            _barImage.type = Image.Type.Sliced;
            _bar = _barImage.rectTransform;
            _bar.sizeDelta = new Vector2(BarWidth, BarHeight + 6f);
            _bar.anchoredPosition = Vector2.zero;

            _zoneImage = CreateImage("Zone", _bar, Color.white, OverlayFishingArt.QteZone());
            _zoneImage.raycastTarget = false;
            _zoneImage.type = Image.Type.Sliced;
            _zone = _zoneImage.rectTransform;
            _zone.sizeDelta = new Vector2(BarWidth * ZoneFraction, BarHeight);

            _knobImage = CreateImage("Knob", _bar, Color.white, OverlayFishingArt.QteKnob());
            _knobImage.raycastTarget = false;
            _knobImage.preserveAspect = true;
            _knob = _knobImage.rectTransform;
            _knob.sizeDelta = new Vector2(KnobWidth + 8f, BarHeight + 14f);
        }

        void LayoutZone()
        {
            if (_zone == null) return;
            var w = BarWidth * ZoneFraction;
            _zone.sizeDelta = new Vector2(w, BarHeight - 4f);
            var centerX = (_zoneStart + ZoneFraction * 0.5f - 0.5f) * BarWidth;
            _zone.anchoredPosition = new Vector2(centerX, 0f);
        }

        void Follow(FriendAvatarChip chip)
        {
            if (chip == null || _root == null) return;
            var scale = _view != null && _view.Settings != null ? _view.Settings.Scale : 1f;
            _root.anchoredPosition = chip.FollowPosition + new Vector2(0f, 78f) * scale;
            _root.localScale = Vector3.one * scale;
        }

        void ApplyTheme()
        {
            // Cartoon Kenney slides keep their authored colors; only fallback rects follow theme.
            if (_barImage != null && _barImage.sprite == OverlaySprites.RoundedRect)
            {
                var theme = _view != null && _view.Settings != null ? _view.Settings.SettingsTheme : 1;
                _barImage.color = OverlaySkin.ThemeSection(theme);
            }
            else if (_barImage != null)
            {
                _barImage.color = Color.white;
            }

            if (_zoneImage != null && _zoneImage.sprite == OverlaySprites.RoundedRect)
            {
                var theme = _view != null && _view.Settings != null ? _view.Settings.SettingsTheme : 1;
                var a = OverlaySkin.ThemeAccent(theme);
                a.a = 0.55f;
                _zoneImage.color = a;
            }
            else if (_zoneImage != null)
            {
                _zoneImage.color = Color.white;
            }

            if (_knobImage != null)
                _knobImage.color = Color.white;
        }

        bool IsPointerOverPanel()
        {
            if (_root == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(_root, Input.mousePosition, null);
        }

        static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite != null ? sprite : OverlaySprites.RoundedRect;
            image.type = Image.Type.Sliced;
            image.color = color;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return image;
        }
    }
}
