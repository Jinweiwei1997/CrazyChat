using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public static class OverlaySprites
    {
        public static readonly Color Panel = new Color(0.10f, 0.11f, 0.14f, 0.94f);
        public static readonly Color Well = new Color(0f, 0f, 0f, 0.26f);
        public static readonly Color Button = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color Accent = new Color(0.36f, 0.56f, 0.93f, 1f);
        public static readonly Color Danger = new Color(0.80f, 0.33f, 0.35f, 1f);
        public static readonly Color TextMuted = new Color(1f, 1f, 1f, 0.42f);
        static Sprite _circle;
        static Sprite _dashedCircle;
        static Sprite _roundedRect;
        static Sprite _roundedSquare;
        static Sprite _dashedRoundedSquare;
        static Font _font;

        public static Sprite Circle
        {
            get
            {
                if (_circle == null)
                {
                    _circle = CreateCircle(128);
                }

                return _circle;
            }
        }

        public static Sprite DashedCircle
        {
            get
            {
                if (_dashedCircle == null)
                {
                    _dashedCircle = CreateDashedCircle(128);
                }

                return _dashedCircle;
            }
        }

        public static Sprite RoundedRect
        {
            get
            {
                if (_roundedRect == null)
                {
                    _roundedRect = CreateRoundedRect(64, 32, 10);
                }

                return _roundedRect;
            }
        }

        public static Sprite RoundedSquare
        {
            get
            {
                if (_roundedSquare == null)
                {
                    _roundedSquare = CreateRoundedRect(128, 128, 16);
                    _roundedSquare.name = "OverlayRoundedSquare";
                }

                return _roundedSquare;
            }
        }

        public static Sprite DashedRoundedSquare
        {
            get
            {
                if (_dashedRoundedSquare == null)
                {
                    _dashedRoundedSquare = CreateDashedRoundedSquare(128, 16);
                }

                return _dashedRoundedSquare;
            }
        }

        public static Font UiFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "Arial Unicode MS", "Arial" },
                        18);
                    if (_font == null)
                    {
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                }

                return _font;
            }
        }

        public static Texture2D CreateSolidAvatar(Color color, string initials)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Avatar_" + initials
            };

            var pixels = new Color[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static Sprite CreateCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "OverlayCircle"
            };

            var r = size * 0.5f;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - r;
                    var dy = y + 0.5f - r;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Clamp01(r - d);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static Sprite CreateDashedCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "OverlayDashedCircle"
            };

            var r = size * 0.5f;
            var outer = r - 1f;
            var inner = r - 8f;
            const int dashes = 18;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - r;
                    var dy = y + 0.5f - r;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > outer || d < inner)
                    {
                        continue;
                    }

                    var angle = Mathf.Atan2(dy, dx);
                    if (angle < 0f)
                    {
                        angle += Mathf.PI * 2f;
                    }

                    var slot = Mathf.FloorToInt(angle / (Mathf.PI * 2f) * dashes);
                    if ((slot & 1) == 0)
                    {
                        var a = Mathf.Clamp01(Mathf.Min(outer - d, d - inner));
                        pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static Sprite CreateDashedRoundedSquare(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "OverlayDashedRoundedSquare"
            };

            const float thickness = 7f;
            const float dash = 9f;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = x + 0.5f;
                    var py = y + 0.5f;
                    var outer = RoundedAlpha(px, py, size, size, radius);
                    var inner = RoundedAlpha(px, py, size, size, radius, thickness);
                    if (outer <= 0f || inner > 0f)
                    {
                        continue;
                    }

                    var along = NearHorizontalEdge(py, size, radius + thickness)
                        ? px
                        : py;
                    var slot = Mathf.FloorToInt(along / dash);
                    if ((slot & 1) == 0)
                    {
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(outer - inner));
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static bool NearHorizontalEdge(float y, int size, float band)
        {
            return y <= band || y >= size - band;
        }

        static Sprite CreateRoundedRect(int width, int height, int radius)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "OverlayRoundedRect"
            };

            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var a = RoundedAlpha(x + 0.5f, y + 0.5f, width, height, radius);
                    pixels[y * width + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            var edge = Mathf.Max(4, radius);
            return Sprite.Create(
                tex,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(edge, edge, edge, edge));
        }

        public static void StyleFill(Image image, Color color)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = RoundedRect;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = color;
        }

        public static void StylePanel(Image image)
        {
            StyleFill(image, Panel);
            var shadow = image.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = image.gameObject.AddComponent<Shadow>();
            }

            shadow.enabled = true;
            shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            shadow.effectDistance = new Vector2(0f, -3f);
            shadow.useGraphicAlpha = true;

            var line = image.GetComponent<Outline>();
            if (line == null)
            {
                line = image.gameObject.AddComponent<Outline>();
            }

            line.enabled = true;
            line.effectColor = new Color(1f, 1f, 1f, 0.12f);
            line.effectDistance = new Vector2(1f, -1f);
            line.useGraphicAlpha = true;
        }

        static float RoundedAlpha(float x, float y, int width, int height, int radius, float inset = 0f)
        {
            var r = Mathf.Max(1f, radius - inset);
            var left = inset;
            var right = width - inset;
            var bottom = inset;
            var top = height - inset;
            var cx = Mathf.Clamp(x, left + r, right - r);
            var cy = Mathf.Clamp(y, bottom + r, top - r);
            var dx = x - cx;
            var dy = y - cy;
            return Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
        }
    }
}
