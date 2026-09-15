using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class OverlayInputPopFx : MonoBehaviour
    {
        const float BaseRiseSpeed = 63f / 0.55f;
        const int PoolSize = 12;

        RectTransform _root;
        OverlayConfig _config;
        readonly List<Pop> _pops = new List<Pop>(PoolSize);

        public static OverlayInputPopFx Create(Transform parent, OverlayConfig config)
        {
            var go = new GameObject("InputPopFx", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<OverlayInputPopFx>();
            fx._config = config;
            config.LoadTemporarySettings();
            fx._root = (RectTransform)go.transform;
            Stretch(fx._root);
            return fx;
        }

        public void Play(Vector2 origin, Sprite icon, Color color, float scale = 1f)
        {
            if (icon == null)
            {
                return;
            }

            var pop = Rent();
            pop.Image.sprite = icon;
            pop.Image.color = color;
            scale = Mathf.Max(0.1f, scale);
            pop.From = origin + new Vector2(Random.Range(-12f, 12f) * scale, 0f);
            pop.RiseCurve = _config.InputPopRiseCurve;
            pop.FadeCurve = _config.InputPopFadeCurve;
            pop.Group.alpha = 1f - pop.FadeCurve[0].y;
            pop.Scale = scale;
            pop.StartedAt = Time.unscaledTime;
            pop.Until = pop.StartedAt + pop.FadeCurve[pop.FadeCurve.Length - 1].x;
            pop.Rt.sizeDelta = Vector2.one * 48f * _config.InputPopScale * scale;
            pop.Rt.anchoredPosition = pop.From;
            pop.Rt.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            var now = Time.unscaledTime;
            for (var i = 0; i < _pops.Count; i++)
            {
                var pop = _pops[i];
                if (!pop.Rt.gameObject.activeSelf)
                {
                    continue;
                }

                var left = pop.Until - now;
                if (left <= 0f)
                {
                    pop.Rt.gameObject.SetActive(false);
                    continue;
                }

                var elapsed = now - pop.StartedAt;
                var distance = IntegrateRiseCurve(pop.RiseCurve, elapsed) * BaseRiseSpeed * pop.Scale;
                pop.Rt.anchoredPosition = pop.From + Vector2.up * distance;
                pop.Group.alpha = 1f - EvaluateFadeCurve(pop.FadeCurve, elapsed);
            }
        }

        internal static float EvaluateFadeCurve(Vector2[] points, float time)
        {
            for (var i = 1; i < points.Length; i++)
            {
                if (time > points[i].x) continue;
                var u = Mathf.Clamp01((time - points[i - 1].x) / (points[i].x - points[i - 1].x));
                return Mathf.Lerp(points[i - 1].y, points[i].y, u * u * (3f - 2f * u));
            }
            return points[points.Length - 1].y;
        }

        internal static float IntegrateRiseCurve(Vector2[] points, float time)
        {
            var distance = 0f;
            for (var i = 1; i < points.Length; i++)
            {
                var start = points[i - 1];
                var end = points[i];
                if (time <= start.x) return distance;
                var duration = end.x - start.x;
                var u = Mathf.Clamp01((time - start.x) / duration);
                // Exact integral of the smoothstep speed, independent of frame rate.
                distance += duration * (start.y * u + (end.y - start.y) * (u * u * u - 0.5f * u * u * u * u));
                if (time <= end.x) return distance;
            }
            var last = points[points.Length - 1];
            return distance + Mathf.Max(0f, time - last.x) * last.y;
        }

        Pop Rent()
        {
            for (var i = 0; i < _pops.Count; i++)
            {
                if (!_pops[i].Rt.gameObject.activeSelf)
                {
                    return _pops[i];
                }
            }

            if (_pops.Count >= PoolSize)
            {
                var oldest = _pops[0];
                for (var i = 1; i < _pops.Count; i++)
                {
                    if (_pops[i].Until < oldest.Until)
                    {
                        oldest = _pops[i];
                    }
                }

                return oldest;
            }

            var image = CreateImage("Pop", _root);
            var pop = new Pop
            {
                Rt = image.rectTransform,
                Image = image,
                Group = image.gameObject.AddComponent<CanvasGroup>()
            };
            pop.Group.blocksRaycasts = false;
            pop.Rt.gameObject.SetActive(false);
            _pops.Add(pop);
            return pop;
        }

        static Image CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return image;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        sealed class Pop
        {
            public RectTransform Rt;
            public Image Image;
            public CanvasGroup Group;
            public Vector2 From;
            public Vector2[] RiseCurve;
            public Vector2[] FadeCurve;
            public float Scale;
            public float StartedAt;
            public float Until;
        }
    }
}
