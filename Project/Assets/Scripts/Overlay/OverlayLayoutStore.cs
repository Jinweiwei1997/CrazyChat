#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CrazyChat.Overlay
{
    /// <summary>
    /// 记录每个好友头像的位置：本地文件。云存档等有正式 AppID 再开。
    /// 坐标用 0~1，换分辨率或换电脑也能对上。
    /// </summary>
    public sealed class OverlayLayoutStore
    {
        const string FileName = "overlay_layout.json";

        readonly Dictionary<ulong, Vector2> _normalized = new Dictionary<ulong, Vector2>();

        public static string LocalPath => Path.Combine(Application.persistentDataPath, FileName);

        public void Load()
        {
            _normalized.Clear();
            var json = ReadSteamCloud();
            if (string.IsNullOrEmpty(json))
            {
                json = ReadLocal();
            }

            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<LayoutFile>(json);
                if (data?.friends == null)
                {
                    return;
                }

                for (var i = 0; i < data.friends.Count; i++)
                {
                    var entry = data.friends[i];
                    if (ulong.TryParse(entry.id, out var id))
                    {
                        _normalized[id] = new Vector2(entry.x, entry.y);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 读取头像位置存档失败: " + e.Message);
            }
        }

        public bool Has(ulong steamId)
        {
            return _normalized.ContainsKey(steamId);
        }

        public bool TryGetPixel(ulong steamId, out Vector2 pixel)
        {
            var size = FriendOverlayView.OverlayPixelSize;
            if (_normalized.TryGetValue(steamId, out var n) && size.x > 0f && size.y > 0f)
            {
                pixel = new Vector2(n.x * size.x, n.y * size.y);
                return true;
            }

            pixel = default;
            return false;
        }

        public void SetPixel(ulong steamId, Vector2 pixel)
        {
            var size = FriendOverlayView.OverlayPixelSize;
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            _normalized[steamId] = new Vector2(pixel.x / size.x, pixel.y / size.y);
        }

        public void Remove(ulong steamId)
        {
            _normalized.Remove(steamId);
        }

        public void Save()
        {
            var data = new LayoutFile { version = 1, friends = new List<LayoutEntry>(_normalized.Count) };
            foreach (var pair in _normalized)
            {
                data.friends.Add(new LayoutEntry
                {
                    id = pair.Key.ToString(),
                    x = pair.Value.x,
                    y = pair.Value.y
                });
            }

            var json = JsonUtility.ToJson(data, true);
            WriteLocal(json);
            WriteSteamCloud(json);
        }

        public static Vector2 LocalDefaultPixel(float size = 128f)
        {
            return new Vector2(48f + size * 0.5f, 48f + size * 0.5f);
        }

        public static Vector2 DefaultPixel(int index, float size = 128f)
        {
            var gap = size + 8f;
            var x = 48f + size * 0.5f;
            var y = 48f + size * 0.5f + index * gap;
            return new Vector2(x, y);
        }

        static string ReadLocal()
        {
            try
            {
                var path = LocalPath;
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 读取本地存档失败: " + e.Message);
                return null;
            }
        }

        static void WriteLocal(string json)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(LocalPath, json, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 写入本地存档失败: " + e.Message);
            }
        }

        static string ReadSteamCloud()
        {
            if (!OverlayConfig.SteamCloud)
            {
                return null;
            }

#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized || !SteamRemoteStorage.FileExists(FileName))
            {
                return null;
            }

            var size = SteamRemoteStorage.GetFileSize(FileName);
            if (size <= 0)
            {
                return null;
            }

            var buffer = new byte[size];
            var read = SteamRemoteStorage.FileRead(FileName, buffer, size);
            return read > 0 ? Encoding.UTF8.GetString(buffer, 0, read) : null;
#else
            return null;
#endif
        }

        static void WriteSteamCloud(string json)
        {
            if (!OverlayConfig.SteamCloud)
            {
                return;
            }

#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(json);
            if (!SteamRemoteStorage.FileWrite(FileName, bytes, bytes.Length))
            {
                Debug.LogWarning("[Overlay] Steam 云存档写入失败。请在 Steamworks 后台打开 Cloud，并勾选账号的云存档。");
            }
#endif
        }

        [Serializable]
        class LayoutFile
        {
            public int version = 1;
            public List<LayoutEntry> friends = new List<LayoutEntry>();
        }

        [Serializable]
        class LayoutEntry
        {
            public string id;
            public float x;
            public float y;
        }
    }
}
