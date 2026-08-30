using UnityEngine;

namespace CrazyChat.Overlay
{
    public static class OverlaySprites
    {
        static Sprite _circle;
        static Sprite _dashedCircle;
        static Sprite _roundedRect;
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
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        static float RoundedAlpha(float x, float y, int width, int height, int radius)
        {
            var cx = Mathf.Clamp(x, radius, width - radius);
            var cy = Mathf.Clamp(y, radius, height - radius);
            var dx = x - cx;
            var dy = y - cy;
            return Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
        }
    }
}
