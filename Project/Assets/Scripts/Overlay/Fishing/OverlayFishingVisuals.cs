using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay.Fishing
{
    /// <summary>Rod / water / bubble / catch visuals for local and remote chips.</summary>
    public sealed class OverlayFishingVisuals : MonoBehaviour
    {
        const float RodSize = 154f;
        const float RodTilt = -28f;
        const float RodOffsetX = 70f;
        // Line tip measured on BambooFishingRod.png, as a fraction of the drawn rect from its center
        // (y already folded with the 1312x1199 aspect), so the water follows any rod size or tilt.
        static readonly Vector2 RodLineTip = new Vector2(0.284f, -0.227f);

        FriendOverlayView _view;
        Transform _chrome;
        Transform _under;
        readonly Dictionary<ulong, RodPair> _remote = new Dictionary<ulong, RodPair>();
        RodPair _local;
        RectTransform _bubble;
        Text _bubbleText;
        Action _bubbleClick;
        float _bubbleEnd = -1f;
        Image _catchImage;
        Text _rewardText;
        RectTransform _catchRt;

        struct RodPair
        {
            public RectTransform root;
            public Image rod;
            public Image water;
        }

        /// <summary>Rods live under the avatars; bubble, QTE prompt and catch UI stay on chrome.</summary>
        public static OverlayFishingVisuals Create(Transform chrome, Transform under, FriendOverlayView view)
        {
            var go = new GameObject("FishingVisuals", typeof(RectTransform));
            go.transform.SetParent(chrome, false);
            var v = go.AddComponent<OverlayFishingVisuals>();
            v._view = view;
            v._chrome = chrome;
            v._under = under != null ? under : chrome;
            v.BuildLocalUi();
            return v;
        }

        public void Tick()
        {
            FollowLocal();
            FollowRemotes();
            if (_bubble != null && _bubble.gameObject.activeSelf && _bubbleEnd > 0f)
            {
                var left = Mathf.Max(0f, _bubbleEnd - Time.unscaledTime);
                if (_bubbleText != null)
                    _bubbleText.text = "上鱼! " + Mathf.CeilToInt(left);
            }
        }

        public void ShowLocalRod(bool on)
        {
            EnsureLocal();
            if (_local.root != null) _local.root.gameObject.SetActive(on);
            if (!on)
            {
                HideBubble();
                HideCatchUi();
            }
        }

        public void SetRemoteFishing(ulong id, bool on)
        {
            if (id == 0) return;
            if (!on)
            {
                if (_remote.TryGetValue(id, out var pair) && pair.root != null)
                    pair.root.gameObject.SetActive(false);
                return;
            }

            var p = EnsureRemote(id);
            if (p.root != null) p.root.gameObject.SetActive(true);
        }

        public void PlayRemoteCatch(ulong id)
        {
            if (!_remote.TryGetValue(id, out var pair) || pair.rod == null) return;
            pair.root.gameObject.StartCoroutineSafe(ReelPunch(pair.rod.rectTransform, 0.6f));
        }

        public void PlayLocalReel(float seconds)
        {
            EnsureLocal();
            if (_local.rod != null)
                StartCoroutine(ReelPunch(_local.rod.rectTransform, seconds));
        }

        public void ShowAdvancedBubble(float seconds, Action onClick)
        {
            EnsureBubble();
            _bubbleClick = onClick;
            _bubbleEnd = Time.unscaledTime + seconds;
            _bubble.gameObject.SetActive(true);
            if (_bubbleText != null) _bubbleText.text = "上鱼! " + Mathf.CeilToInt(seconds);
        }

        public void HideBubble()
        {
            _bubbleClick = null;
            _bubbleEnd = -1f;
            if (_bubble != null) _bubble.gameObject.SetActive(false);
        }

        public IEnumerator PlayLowFishFade(OverlayFishDef fish, float seconds)
        {
            EnsureCatchUi();
            ApplyFishSprite(_catchImage, fish);
            _catchImage.color = Color.white;
            if (_rewardText != null) _rewardText.gameObject.SetActive(false);
            _catchRt.gameObject.SetActive(true);
            var t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                var a = 1f - Mathf.Clamp01(t / seconds);
                var c = _catchImage.color;
                c.a = a;
                _catchImage.color = c;
                yield return null;
            }
            HideCatchUi();
        }

        public IEnumerator PlayReward(OverlayFishDef fish, int points, float seconds)
        {
            EnsureCatchUi();
            ApplyFishSprite(_catchImage, fish);
            _catchImage.color = Color.white;
            if (_rewardText != null)
            {
                _rewardText.gameObject.SetActive(true);
                _rewardText.text = "+" + points;
                _rewardText.color = new Color(1f, 0.92f, 0.35f, 1f);
            }
            _catchRt.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(seconds);
            HideCatchUi();
        }

        void HideCatchUi()
        {
            if (_catchRt != null) _catchRt.gameObject.SetActive(false);
        }

        void BuildLocalUi()
        {
            EnsureLocal();
            EnsureBubble();
            EnsureCatchUi();
            ShowLocalRod(false);
            HideBubble();
            HideCatchUi();
        }

        void EnsureLocal()
        {
            if (_local.root != null) return;
            _local = BuildRodPair("LocalRod", _under);
            _local.root.gameObject.SetActive(false);
        }

        RodPair EnsureRemote(ulong id)
        {
            if (_remote.TryGetValue(id, out var existing) && existing.root != null) return existing;
            var pair = BuildRodPair("RemoteRod_" + id, _under);
            _remote[id] = pair;
            return pair;
        }

        RodPair BuildRodPair(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;

            // Drop the rod so the line tip — and the water with it — lands on the avatar's bottom edge.
            var lineTip = (Vector2)(Quaternion.Euler(0f, 0f, RodTilt) * (RodLineTip * RodSize));
            var chipHalf = (_view != null && _view.Config != null ? _view.Config.chipSize : 128f) * 0.5f;
            var rodPos = new Vector2(RodOffsetX, -chipHalf - lineTip.y);

            var water = CreateImage("Water", root, new Color(0.35f, 0.7f, 1f, 0.45f), OverlaySprites.Circle);
            water.raycastTarget = false;
            water.rectTransform.sizeDelta = new Vector2(72f, 28f);
            water.rectTransform.anchoredPosition = rodPos + lineTip;

            var rod = CreateImage("Rod", root, Color.white, OverlayFishingArt.Rod());
            rod.raycastTarget = false;
            rod.preserveAspect = true;
            rod.rectTransform.sizeDelta = new Vector2(RodSize, RodSize);
            rod.rectTransform.anchoredPosition = rodPos;
            rod.rectTransform.localEulerAngles = new Vector3(0f, 0f, RodTilt);

            return new RodPair { root = root, rod = rod, water = water };
        }

        void EnsureBubble()
        {
            if (_bubble != null) return;
                var img = CreateImage("FishBubble", _chrome, OverlaySkin.ThemeBackground(1), OverlaySprites.RoundedRect);
            img.raycastTarget = true;
            _bubble = img.rectTransform;
            _bubble.sizeDelta = new Vector2(88f, 36f);
            _bubbleText = CreateLabel(_bubble, "上鱼!", 13);
            _bubbleText.alignment = TextAnchor.MiddleCenter;
            if (_bubbleText != null) _bubbleText.color = OverlaySkin.SettingsThemeText(1);
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => _bubbleClick?.Invoke());
            _bubble.gameObject.SetActive(false);
        }

        void EnsureCatchUi()
        {
            if (_catchRt != null) return;
            var root = new GameObject("FishCatch", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(_chrome, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(96f, 64f);
            _catchRt = root;
            _catchImage = CreateImage("Icon", root, Color.white, OverlaySprites.Circle);
            _catchImage.raycastTarget = false;
            _catchImage.rectTransform.sizeDelta = new Vector2(48f, 48f);
            _catchImage.rectTransform.anchoredPosition = new Vector2(-18f, 0f);
            _rewardText = CreateLabel(root, "+0", 16);
            _rewardText.alignment = TextAnchor.MiddleLeft;
            _rewardText.rectTransform.anchoredPosition = new Vector2(28f, 0f);
            _rewardText.rectTransform.sizeDelta = new Vector2(56f, 28f);
            root.gameObject.SetActive(false);
        }

        void FollowLocal()
        {
            if (_local.root == null || _view == null || _view.LocalChip == null) return;
            PlaceAt(_local.root, _view.LocalChip);
            if (_bubble != null && _bubble.gameObject.activeSelf)
                PlaceOffset(_bubble, _view.LocalChip, new Vector2(56f, 70f));
            if (_catchRt != null && _catchRt.gameObject.activeSelf)
                PlaceOffset(_catchRt, _view.LocalChip, new Vector2(56f, 70f));
        }

        void FollowRemotes()
        {
            if (_view == null) return;
            foreach (var pair in _remote)
            {
                if (pair.Value.root == null || !pair.Value.root.gameObject.activeSelf) continue;
                if (!_view.TryGetChip(pair.Key, out var chip) || chip == null)
                {
                    pair.Value.root.gameObject.SetActive(false);
                    continue;
                }
                PlaceAt(pair.Value.root, chip);
            }
        }

        void PlaceAt(RectTransform rt, FriendAvatarChip chip)
        {
            var scale = _view.Settings != null ? _view.Settings.Scale : 1f;
            rt.anchoredPosition = chip.FollowPosition;
            rt.localScale = Vector3.one * scale;
        }

        void PlaceOffset(RectTransform rt, FriendAvatarChip chip, Vector2 localOffset)
        {
            var scale = _view.Settings != null ? _view.Settings.Scale : 1f;
            rt.anchoredPosition = chip.FollowPosition + localOffset * scale;
            rt.localScale = Vector3.one * scale;
        }

        static IEnumerator ReelPunch(RectTransform rod, float seconds)
        {
            if (rod == null) yield break;
            var baseEuler = rod.localEulerAngles;
            var t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                var u = t / seconds;
                var wave = Mathf.Sin(u * Mathf.PI * 4f) * (1f - u) * 18f;
                rod.localEulerAngles = baseEuler + new Vector3(0f, 0f, wave);
                yield return null;
            }
            rod.localEulerAngles = baseEuler;
        }

        static Sprite LoadRodSprite() => OverlayFishingArt.Rod();

        static void ApplyFishSprite(Image image, OverlayFishDef fish)
        {
            if (image == null) return;
            var sprite = OverlayFishingArt.FishOrFallback(fish != null ? fish.spriteResource : null);
            image.sprite = sprite;
            image.preserveAspect = true;
            // Keep authored cartoon colors; only tint placeholder circles.
            image.color = sprite != null && sprite != OverlaySprites.Circle
                ? Color.white
                : fish != null && fish.highTier
                    ? new Color(1f, 0.85f, 0.35f, 1f)
                    : new Color(0.55f, 0.75f, 1f, 1f);
        }

        static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite != null ? sprite : OverlaySprites.RoundedRect;
            image.type = Image.Type.Simple;
            image.color = color;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return image;
        }

        static Text CreateLabel(Transform parent, string value, int size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = OverlaySprites.UiFont;
            text.fontSize = size;
            text.text = value;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return text;
        }
    }

    static class FishingCoroutineHost
    {
        public static void StartCoroutineSafe(this GameObject go, IEnumerator routine)
        {
            if (go == null) return;
            var host = go.GetComponent<MonoBehaviour>();
            if (host == null) host = go.AddComponent<FishingFxHost>();
            host.StartCoroutine(routine);
        }
    }

    sealed class FishingFxHost : MonoBehaviour { }
}
