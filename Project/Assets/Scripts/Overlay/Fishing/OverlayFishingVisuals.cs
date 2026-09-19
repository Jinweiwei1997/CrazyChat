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
        // Pixel store rods are 16px art; keep near chip-scale, not bamboo-sized.
        const float RodSize = 64f;
        const float RodTilt = -8f;
        // Rest pose: inside avatar, ~10% up from bottom edge, slightly to the right.
        const float RodFromBottom = 0.10f;
        const float RodOffsetXFrac = 0.18f;
        // Zro cartoon water strip is 2:1; keep a readable pool under the chip.
        const float WaterHeightFrac = 0.36f;
        const float WaterFps = 12f;
        const float ReelLiftDegrees = 42f;
        const float ReelLiftUpPx = 16f;

        FriendOverlayView _view;
        Transform _chrome;
        RectTransform _rodLayer;
        readonly Dictionary<ulong, RodPair> _remote = new Dictionary<ulong, RodPair>();
        RodPair _local;
        RectTransform _bubble;
        Text _bubbleText;
        Action _bubbleClick;
        float _bubbleEnd = -1f;
        Image _catchImage;
        Text _rewardText;
        RectTransform _catchRt;
        Coroutine _localReel;
        float _waterAnimT;

        struct RodPair
        {
            public RectTransform root;
            public Image rod;
            public Image water;
            public Vector2 rodRestPos;
            public Vector3 rodRestEuler;
        }

        /// <summary>Bubble / catch live on chrome; rod and water live on UnderFriendLayer, behind the avatars.</summary>
        public static OverlayFishingVisuals Create(Transform chrome, Transform underFriendLayer, FriendOverlayView view)
        {
            var go = new GameObject("FishingVisuals", typeof(RectTransform));
            go.transform.SetParent(chrome, false);
            // Full-screen space so FollowPosition matches FriendLayer chip coords.
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            // Keep under other chrome UI (settings/chat/interact) but above FriendLayer.
            go.transform.SetAsFirstSibling();
            var v = go.AddComponent<OverlayFishingVisuals>();
            v._view = view;
            v._chrome = chrome;
            v._rodLayer = CreateFullScreenRoot("FishingRods", underFriendLayer != null ? underFriendLayer : chrome);
            v.BuildLocalUi();
            return v;
        }

        static RectTransform CreateFullScreenRoot(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public void Tick()
        {
            FollowLocal();
            FollowRemotes();
            TickWaterAnim();
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
            pair.root.gameObject.StartCoroutineSafe(ReelLift(pair, 0.6f));
        }

        public void PlayLocalReel(float seconds)
        {
            EnsureLocal();
            if (_local.rod == null) return;
            if (_localReel != null) StopCoroutine(_localReel);
            _localReel = StartCoroutine(ReelLift(_local, seconds));
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

        Transform RodParent => _rodLayer != null ? _rodLayer : transform;

        void OnDestroy()
        {
            if (_rodLayer != null) Destroy(_rodLayer.gameObject);
        }

        void EnsureLocal()
        {
            if (_local.root != null) return;
            _local = BuildRodPair("LocalRod", RodParent);
            _local.root.gameObject.SetActive(false);
        }

        RodPair EnsureRemote(ulong id)
        {
            if (_remote.TryGetValue(id, out var existing) && existing.root != null) return existing;
            var pair = BuildRodPair("RemoteRod_" + id, RodParent);
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

            var chip = _view != null && _view.Config != null ? _view.Config.chipSize : 128f;
            var chipHalf = chip * 0.5f;
            var waterH = chip * WaterHeightFrac;
            // Full avatar width; sit across the bottom edge so it reads as a pool under the chip.
            var waterPos = new Vector2(0f, -chipHalf + waterH * 0.35f);
            // Rod rests inside the avatar, ~10% up from the bottom.
            var rodPos = new Vector2(chip * RodOffsetXFrac, -chipHalf + chip * RodFromBottom);
            var rodEuler = new Vector3(0f, 0f, RodTilt);

            var waterSprite = OverlayFishingArt.Water();
            var waterTint = waterSprite != null && waterSprite != OverlaySprites.Circle
                ? Color.white
                : new Color(0.25f, 0.55f, 0.95f, 0.55f);
            var water = CreateImage("Water", root, waterTint, waterSprite);
            water.raycastTarget = false;
            water.preserveAspect = false;
            water.rectTransform.sizeDelta = new Vector2(chip, waterH);
            water.rectTransform.anchoredPosition = waterPos;

            var rod = CreateImage("Rod", root, Color.white, OverlayFishingArt.Rod());
            rod.raycastTarget = false;
            rod.preserveAspect = true;
            rod.rectTransform.sizeDelta = new Vector2(RodSize, RodSize);
            rod.rectTransform.anchoredPosition = rodPos;
            rod.rectTransform.localEulerAngles = rodEuler;
            // Draw rod above the pool.
            rod.rectTransform.SetAsLastSibling();

            return new RodPair
            {
                root = root,
                rod = rod,
                water = water,
                rodRestPos = rodPos,
                rodRestEuler = rodEuler
            };
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
            root.sizeDelta = new Vector2(120f, 80f);
            _catchRt = root;
            _catchImage = CreateImage("Icon", root, Color.white, OverlaySprites.Circle);
            _catchImage.raycastTarget = false;
            _catchImage.preserveAspect = true;
            _catchImage.rectTransform.sizeDelta = new Vector2(72f, 72f);
            _catchImage.rectTransform.anchoredPosition = new Vector2(-12f, 0f);
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

        void TickWaterAnim()
        {
            var frames = OverlayFishingArt.WaterFrames();
            if (frames == null || frames.Length == 0) return;

            _waterAnimT += Time.unscaledDeltaTime;
            var idx = Mathf.FloorToInt(_waterAnimT * WaterFps) % frames.Length;
            var sprite = frames[idx];
            if (sprite == null) return;

            if (_local.water != null && _local.root != null && _local.root.gameObject.activeSelf)
                _local.water.sprite = sprite;

            foreach (var pair in _remote)
            {
                if (pair.Value.water == null || pair.Value.root == null) continue;
                if (!pair.Value.root.gameObject.activeSelf) continue;
                pair.Value.water.sprite = sprite;
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

        /// <summary>Catch reel: tip lifts up, holds briefly, then settles back into the pool.</summary>
        static IEnumerator ReelLift(RodPair pair, float seconds)
        {
            if (pair.rod == null) yield break;
            var rod = pair.rod.rectTransform;
            var restPos = pair.rodRestPos;
            var restEuler = pair.rodRestEuler;
            var liftEuler = restEuler + new Vector3(0f, 0f, ReelLiftDegrees);
            var liftPos = restPos + new Vector2(0f, ReelLiftUpPx);
            var dur = Mathf.Max(0.15f, seconds);
            var up = dur * 0.35f;
            var hold = dur * 0.25f;
            var down = dur - up - hold;

            var t = 0f;
            while (t < up)
            {
                t += Time.unscaledDeltaTime;
                var u = EaseOutCubic(Mathf.Clamp01(t / up));
                rod.anchoredPosition = Vector2.LerpUnclamped(restPos, liftPos, u);
                rod.localEulerAngles = Vector3.LerpUnclamped(restEuler, liftEuler, u);
                yield return null;
            }

            rod.anchoredPosition = liftPos;
            rod.localEulerAngles = liftEuler;
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

            t = 0f;
            while (t < down)
            {
                t += Time.unscaledDeltaTime;
                var u = EaseInCubic(Mathf.Clamp01(t / down));
                rod.anchoredPosition = Vector2.LerpUnclamped(liftPos, restPos, u);
                rod.localEulerAngles = Vector3.LerpUnclamped(liftEuler, restEuler, u);
                yield return null;
            }

            rod.anchoredPosition = restPos;
            rod.localEulerAngles = restEuler;
        }

        static float EaseOutCubic(float u)
        {
            var inv = 1f - u;
            return 1f - inv * inv * inv;
        }

        static float EaseInCubic(float u) => u * u * u;

        static Sprite LoadRodSprite() => OverlayFishingArt.Rod();

        static void ApplyFishSprite(Image image, OverlayFishDef fish)
        {
            if (image == null) return;
            var sprite = OverlayFishingArt.FishOrFallback(fish != null ? fish.spriteResource : null);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
            // Keep authored colors; only tint true placeholder circles.
            var placeholder = sprite == null
                              || sprite == OverlaySprites.Circle
                              || string.IsNullOrEmpty(sprite.name)
                              || sprite.name == "Circle"
                              || sprite.texture == null
                              || sprite.texture.width <= 8;
            image.color = !placeholder
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
