using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay.Interact
{
    public sealed class OverlayInteractFx : MonoBehaviour
    {
        RectTransform _layer;
        OverlayConfig _config;
        OverlayUserSettings _settings;
        Sprite _tomato;
        readonly List<Flight> _flights = new List<Flight>();
        readonly List<FireworkParticle> _fireworkParticles = new List<FireworkParticle>();
        readonly List<FireworkShot> _fireworkShots = new List<FireworkShot>();

        static readonly Color[] FireworkColors =
        {
            new Color(1f, 0.78f, 0.18f),
            new Color(1f, 0.28f, 0.48f),
            new Color(0.25f, 0.82f, 1f),
            new Color(0.45f, 1f, 0.52f)
        };

        public static OverlayInteractFx Create(Transform canvas, OverlayConfig config, OverlayUserSettings settings)
        {
            var root = new GameObject("InteractFx", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            var fx = root.AddComponent<OverlayInteractFx>();
            fx._config = config;
            fx._settings = settings;
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

        /// <summary>
        /// 问号从发起者头像飞到对方头顶，再从头顶把烟花弹打向屏幕中心区域的随机落点炸开。
        /// </summary>
        public void PlayFireworks(Vector2 from, Vector2 to)
        {
            var chipSize = ChipSize();
            var avatarScale = _settings != null ? Mathf.Max(0.1f, _settings.Scale) : 1f;
            var head = to + new Vector2(0f, chipSize * avatarScale * 0.62f);

            var go = new GameObject("FireworkQuestion", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(_layer, false);
            var text = go.GetComponent<Text>();
            text.font = OverlaySprites.UiFont;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = Mathf.Max(12, Mathf.RoundToInt(chipSize * 0.315f * avatarScale));
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = "?";
            text.color = Color.white;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(chipSize * 0.45f, chipSize * 0.45f);
            rt.anchoredPosition = from;

            _fireworkShots.Add(new FireworkShot
            {
                rect = rt,
                graphic = text,
                stage = ShotStage.Question,
                from = from,
                to = head,
                head = head,
                landing = RandomCenterLanding(),
                start = Time.unscaledTime,
                duration = 0.55f,
                arc = Mathf.Clamp(Vector2.Distance(from, head) * 0.3f, 48f, 150f)
            });
        }

        /// <summary>
        /// Temporary procedural visual. Round sparks fly a tilted 3D path, then fall.
        /// </summary>
        void SpawnBurst(Vector2 center)
        {
            const int particleCount = 56;
            const float goldenAngle = 2.3999632f;
            var chipSize = ChipSize();
            var sizeScale = FireworkScale();
            var maxRadius = chipSize * BurstRadiusScale() * sizeScale;
            var start = Time.unscaledTime;
            var colorOffset = Random.Range(0, FireworkColors.Length);
            var tilt = Quaternion.Euler(-26f, Random.Range(0f, 360f), 0f);

            var burst = new GameObject("FireworkBurst", typeof(RectTransform));
            burst.transform.SetParent(_layer, false);
            var burstRt = (RectTransform)burst.transform;
            burstRt.anchorMin = burstRt.anchorMax = Vector2.zero;
            burstRt.pivot = new Vector2(0.5f, 0.5f);
            burstRt.anchoredPosition = Vector2.zero;
            burstRt.sizeDelta = Vector2.zero;

            for (var i = 0; i < particleCount; i++)
            {
                var y = 1f - (i + 0.5f) / particleCount * 2f;
                var ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var theta = goldenAngle * i + Random.Range(-0.08f, 0.08f);
                var direction = tilt * new Vector3(Mathf.Cos(theta) * ring, y, Mathf.Sin(theta) * ring);

                var go = new GameObject("FireworkParticle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(burstRt, false);

                var image = go.GetComponent<Image>();
                image.sprite = OverlaySprites.Circle;
                image.raycastTarget = false;
                var color = FireworkColors[(i + colorOffset) % FireworkColors.Length];
                image.color = color;

                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                var size = chipSize * Random.Range(0.08f, 0.13f) * sizeScale;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = center;
                rt.SetSiblingIndex(Mathf.RoundToInt((direction.z + 1f) * 40f));

                _fireworkParticles.Add(new FireworkParticle
                {
                    rect = rt,
                    image = image,
                    center = center,
                    direction = direction,
                    distance = maxRadius * Random.Range(0.72f, 1f),
                    fall = maxRadius * 0.38f,
                    duration = Random.Range(0.95f, 1.25f),
                    start = start + Random.Range(0f, 0.05f),
                    color = color
                });
            }
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

            for (var i = _fireworkShots.Count - 1; i >= 0; i--)
            {
                if (!UpdateShot(_fireworkShots[i]))
                {
                    _fireworkShots.RemoveAt(i);
                }
            }

            for (var i = _fireworkParticles.Count - 1; i >= 0; i--)
            {
                var particle = _fireworkParticles[i];
                if (particle.rect == null || particle.image == null)
                {
                    _fireworkParticles.RemoveAt(i);
                    continue;
                }

                var elapsed = Time.unscaledTime - particle.start;
                if (elapsed < 0f)
                {
                    var hidden = particle.color;
                    hidden.a = 0f;
                    particle.image.color = hidden;
                    continue;
                }

                var t = Mathf.Clamp01(elapsed / particle.duration);
                var spread = particle.distance * (1f - (1f - t) * (1f - t));
                var world = particle.direction * spread;
                world.y -= particle.fall * t * t;
                var zNorm = Mathf.Clamp(world.z / Mathf.Max(1f, particle.distance), -1f, 1f);
                var perspective = 1.25f / (1.25f - zNorm * 0.72f);
                particle.rect.anchoredPosition = particle.center + new Vector2(world.x, world.y) * perspective;
                var near = (zNorm + 1f) * 0.5f;
                particle.rect.localScale = Vector3.one * (perspective * Mathf.Lerp(1.2f, 0.28f, t));
                var color = Color.Lerp(particle.color, Color.white, near * 0.2f);
                color.a = 1f - t;
                particle.image.color = color;
                if (t >= 1f)
                {
                    var parent = particle.rect.parent;
                    Destroy(particle.rect.gameObject);
                    _fireworkParticles.RemoveAt(i);
                    if (parent != null && parent != _layer && parent.childCount <= 1)
                    {
                        Destroy(parent.gameObject);
                    }
                }
            }
        }

        /// <summary>返回 false 表示这一发烟花已经走完，可以从列表里移除。</summary>
        bool UpdateShot(FireworkShot shot)
        {
            if (shot.rect == null || shot.graphic == null)
            {
                return false;
            }

            var t = Mathf.Clamp01((Time.unscaledTime - shot.start) / shot.duration);
            var pos = Vector2.Lerp(shot.from, shot.to, t);
            pos.y += shot.arc * 4f * t * (1f - t);
            shot.rect.anchoredPosition = pos;

            switch (shot.stage)
            {
                case ShotStage.Question:
                    shot.rect.localScale = Vector3.one * (t < 0.2f ? Mathf.Lerp(0.4f, 1.1f, t / 0.2f) : Mathf.Lerp(1.1f, 1f, (t - 0.2f) / 0.8f));
                    if (t >= 1f)
                    {
                        shot.stage = ShotStage.QuestionFade;
                        shot.start = Time.unscaledTime;
                        shot.duration = 0.22f;
                        shot.from = shot.head;
                        shot.to = shot.head;
                        shot.arc = 0f;
                    }

                    return true;

                case ShotStage.QuestionFade:
                    shot.rect.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, t);
                    var faded = shot.graphic.color;
                    faded.a = 1f - t;
                    shot.graphic.color = faded;
                    if (t >= 1f)
                    {
                        Destroy(shot.rect.gameObject);
                        StartShell(shot);
                    }

                    return true;

                default:
                    shot.rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.75f, t);
                    if (Time.unscaledTime >= shot.nextTrail)
                    {
                        shot.nextTrail = Time.unscaledTime + 0.025f;
                        SpawnTrail(pos, shot.graphic.color);
                    }

                    if (t >= 1f)
                    {
                        Destroy(shot.rect.gameObject);
                        SpawnBurst(shot.landing);
                        return false;
                    }

                    return true;
            }
        }

        void StartShell(FireworkShot shot)
        {
            var chipSize = ChipSize();
            var sizeScale = FireworkScale();
            var go = new GameObject("FireworkShell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_layer, false);

            var image = go.GetComponent<Image>();
            image.sprite = OverlaySprites.Circle;
            image.color = new Color(1f, 0.92f, 0.62f);
            image.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var size = chipSize * ShellSizeScale() * sizeScale;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = shot.head;

            shot.rect = rt;
            shot.graphic = image;
            shot.stage = ShotStage.Shell;
            shot.from = shot.head;
            shot.to = shot.landing;
            shot.start = Time.unscaledTime;
            shot.duration = 0.8f;
            shot.arc = Mathf.Clamp(Vector2.Distance(shot.head, shot.landing) * 0.45f, 140f, 360f);
            shot.nextTrail = 0f;
        }

        void SpawnTrail(Vector2 position, Color color)
        {
            var size = ChipSize() * ShellSizeScale() * 0.5f * FireworkScale();
            var go = new GameObject("FireworkTrail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_layer, false);

            var image = go.GetComponent<Image>();
            image.sprite = OverlaySprites.Circle;
            image.color = color;
            image.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = position;

            _fireworkParticles.Add(new FireworkParticle
            {
                rect = rt,
                image = image,
                center = position,
                direction = Vector3.zero,
                distance = 0f,
                fall = size * 1.5f,
                duration = 0.32f,
                start = Time.unscaledTime,
                color = color
            });
        }

        /// <summary>落点落在屏幕中心那一块占全屏 1/4 面积的区域内。</summary>
        Vector2 RandomCenterLanding()
        {
            var size = _layer != null ? _layer.rect.size : Vector2.zero;
            if (size.x < 1f || size.y < 1f)
            {
                size = new Vector2(Screen.width, Screen.height);
            }

            return new Vector2(
                Random.Range(size.x * 0.25f, size.x * 0.75f),
                Random.Range(size.y * 0.25f, size.y * 0.75f));
        }

        float ChipSize() => _config != null ? Mathf.Max(32f, _config.chipSize) : 128f;

        float FireworkScale() => _settings != null ? Mathf.Max(0.1f, _settings.FireworkScale) : 1f;

        float BurstRadiusScale() => _config != null ? Mathf.Max(0.1f, _config.fireworkBurstRadius) : 2.4f;

        float ShellSizeScale() => _config != null ? Mathf.Max(0.01f, _config.fireworkShellSize) : 0.11f;

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

        enum ShotStage
        {
            Question,
            QuestionFade,
            Shell
        }

        sealed class FireworkShot
        {
            public RectTransform rect;
            public Graphic graphic;
            public ShotStage stage;
            public Vector2 from;
            public Vector2 to;
            public Vector2 head;
            public Vector2 landing;
            public float start;
            public float duration;
            public float arc;
            public float nextTrail;
        }

        struct FireworkParticle
        {
            public RectTransform rect;
            public Image image;
            public Vector2 center;
            public Vector3 direction;
            public float distance;
            public float fall;
            public float duration;
            public float start;
            public Color color;
        }
    }
}
