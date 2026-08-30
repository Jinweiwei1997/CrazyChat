#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.IO;
using System.Text;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CrazyChat.Overlay
{
    public sealed class OverlayTapStats
    {
        const string FileName = "overlay_stats.json";

        public long Count { get; private set; }

        bool _dirty;

        public void Load()
        {
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
                var data = JsonUtility.FromJson<StatsFile>(json);
                if (data != null && long.TryParse(data.taps, out var taps) && taps > 0)
                {
                    Count = taps;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 读取点击计数失败: " + e.Message);
            }
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Count += amount;
            _dirty = true;
        }

        public void SaveIfDirty()
        {
            if (!_dirty)
            {
                return;
            }

            _dirty = false;
            var json = JsonUtility.ToJson(new StatsFile { taps = Count.ToString() });
            WriteLocal(json);
            WriteSteamCloud(json);
        }

        static string ReadLocal()
        {
            try
            {
                var path = Path.Combine(Application.persistentDataPath, FileName);
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            }
            catch
            {
                return null;
            }
        }

        static void WriteLocal(string json)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(Path.Combine(Application.persistentDataPath, FileName), json, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 写入点击计数失败: " + e.Message);
            }
        }

        static string ReadSteamCloud()
        {
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
#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(json);
            SteamRemoteStorage.FileWrite(FileName, bytes, bytes.Length);
#endif
        }

        [Serializable]
        class StatsFile
        {
            public string taps = "0";
        }
    }
}
