using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CrazyChat.Overlay
{
    public sealed class OverlayTapStats
    {
        const string FileName = "overlay_stats.json";

        public long Count { get; private set; }

        bool _dirty;

        public void Load()
        {
            var json = ReadLocal();

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
            if (amount == 0)
            {
                return;
            }

            var next = Count + amount;
            if (next < 0)
            {
                next = 0;
            }

            if (next == Count)
            {
                return;
            }

            Count = next;
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

        [Serializable]
        class StatsFile
        {
            public string taps = "0";
        }
    }
}
