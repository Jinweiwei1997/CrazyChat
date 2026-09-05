using System;
using System.IO;
using UnityEngine;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Local A/B avatar file helpers (resize + enable rules).
    /// </summary>
    public static class OverlayAvatarCodec
    {
        public const int MaxEdge = 256;
        public const int MaxBytes = 200 * 1024;
        public const string FileA = "avatar_presence_a.png";
        public const string FileB = "avatar_presence_b.png";

        public static string LocalPathA => Path.Combine(Application.persistentDataPath, FileA);
        public static string LocalPathB => Path.Combine(Application.persistentDataPath, FileB);

        public static string CachePath(ulong steamId, bool isA)
        {
            var dir = Path.Combine(Application.persistentDataPath, "avatar_cache");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, steamId + (isA ? "_a.png" : "_b.png"));
        }

        public static bool FilesReady(string pathA, string pathB)
        {
            return OverlayAvatarRules.FilesReady(pathA, pathB);
        }

        public static bool IsEnabled(int version, string pathA, string pathB)
        {
            return OverlayAvatarRules.IsEnabled(version, pathA, pathB);
        }

        /// <summary>
        /// Load image bytes, shrink to MaxEdge / MaxBytes, return PNG bytes or null.
        /// </summary>
        public static byte[] ProcessToPng(byte[] source)
        {
            if (source == null || source.Length == 0)
            {
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(source))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            var edge = Mathf.Max(tex.width, tex.height);
            var scale = edge > MaxEdge ? MaxEdge / (float)edge : 1f;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var w = Mathf.Max(1, Mathf.RoundToInt(tex.width * scale));
                var h = Mathf.Max(1, Mathf.RoundToInt(tex.height * scale));
                var resized = Resize(tex, w, h);
                var png = resized.EncodeToPNG();
                if (resized != tex)
                {
                    UnityEngine.Object.Destroy(resized);
                }

                if (png != null && png.Length > 0 && png.Length <= MaxBytes)
                {
                    UnityEngine.Object.Destroy(tex);
                    return png;
                }

                scale *= 0.75f;
            }

            UnityEngine.Object.Destroy(tex);
            return null;
        }

        public static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
        }

        public static bool TryWrite(string path, byte[] png)
        {
            if (png == null || png.Length == 0)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Application.persistentDataPath);
                File.WriteAllBytes(path, png);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 写入形象图失败: " + e.Message);
                return false;
            }
        }

        public static void DeleteQuiet(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
            }
        }

        static Texture2D Resize(Texture2D source, int w, int h)
        {
            if (source.width == w && source.height == h)
            {
                return source;
            }

            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }
    }
}
