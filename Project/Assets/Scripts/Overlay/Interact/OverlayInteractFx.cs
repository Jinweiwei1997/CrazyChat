using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay.Interact
{
    public sealed class OverlayInteractFx : MonoBehaviour
    {
        RectTransform _layer;
        Sprite _tomato;
        readonly List<Flight> _flights = new List<Flight>();

        public static OverlayInteractFx Create(Transform canvas)
        {
            var root = new GameObject("InteractFx", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            var fx = root.AddComponent<OverlayInteractFx>();
            fx._layer = (RectTransform)root.transform;
            Stretch(fx._layer);
            return fx;
        }

        public void PlayTomato(Vector2 from, Vector2 to)
        {
            if (_tomato == null)
            {
                _tomato = CreateTomatoSprite();
            }

            var go = new GameObject("Tomato", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_layer, false);
            var image = go.GetComponent<Image>();
            image.sprite = _tomato;
            image.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(28f, 28f);
            rt.anchoredPosition = from;
            _flights.Add(new Flight
            {
                rect = rt,
                from = from,
                to = to,
                duration = 0.7f,
                start = Time.unscaledTime,
                height = Mathf.Clamp(Vector2.Distance(from, to) * 0.35f, 64f, 160f)
            });
        }

        void Update()
        {
            for (var i = _flights.Count - 1; i >= 0; i--)
            {
                var flight = _flights[i];
                if (flight.rect == null)
                {
                    _flights.RemoveAt(i);
                    continue;
                }

                var t = Mathf.Clamp01((Time.unscaledTime - flight.start) / flight.duration);
                var mid = Vector2.Lerp(flight.from, flight.to, t);
                mid.y += flight.height * 4f * t * (1f - t);
                flight.rect.anchoredPosition = mid;
                flight.rect.localEulerAngles = new Vector3(0f, 0f, t * 360f);
                var land = t >= 1f ? 1f - Mathf.Clamp01((Time.unscaledTime - flight.start - flight.duration) / 0.12f) : 1f;
                if (t >= 1f)
                {
                    flight.rect.localScale = Vector3.one * Mathf.Max(0.01f, land);
                    if (land <= 0f)
                    {
                        Destroy(flight.rect.gameObject);
                        _flights.RemoveAt(i);
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (_tomato != null && _tomato.texture != null)
            {
                Destroy(_tomato.texture);
                Destroy(_tomato);
            }
        }

        static Sprite CreateTomatoSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Tomato"
            };

            var pixels = new Color[size * size];
            var body = new Vector2(32f, 28f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var db = Vector2.Distance(p, body);
                    if (db < 22f)
                    {
                        var shade = 1f - (22f - db) * 0.012f;
                        pixels[y * size + x] = new Color(0.86f * shade, 0.18f, 0.14f, Mathf.Clamp01(22f - db));
                    }

                    var stem = Vector2.Distance(p, new Vector2(32f, 50f));
                    if (stem < 5.5f && p.y > 42f)
                    {
                        pixels[y * size + x] = new Color(0.28f, 0.62f, 0.22f, Mathf.Clamp01(5.5f - stem));
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        struct Flight
        {
            public RectTransform rect;
            public Vector2 from;
            public Vector2 to;
            public float duration;
            public float start;
            public float height;
        }
    }
}
