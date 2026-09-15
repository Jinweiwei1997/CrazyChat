using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CrazyChat.Overlay
{
    [CreateAssetMenu(fileName = "OverlayConfig", menuName = "CrazyChat/Overlay Config")]
    public sealed class OverlayConfig : ScriptableObject
    {
        public const string AssetPath = "Assets/Resources/OverlayConfig.asset";
        public const float AvatarSnapBounceSeconds = 0.16f;
        public const float AvatarReleaseBounceSeconds = 0.18f;
        public float AvatarSnapDistance { get; private set; } = 36f;
        public float AvatarSnapStartDistance { get; private set; } = 18f;
        public float AvatarSnapGap { get; private set; } = 0f;

        public float InputPopScale { get; private set; } = 1f;
        public Vector2[] InputPopRiseCurve { get; private set; } = DefaultRiseCurve();
        public Vector2[] InputPopFadeCurve { get; private set; } = DefaultFadeCurve();
        public float SelectionStarScale { get; private set; } = 1f;
        public float SelectionStarOffsetX { get; private set; } = 2f;
        public float SelectionStarOffsetY { get; private set; } = -2f;

        public void LoadTemporarySettings()
        {
            AvatarSnapDistance = 36f;
            AvatarSnapStartDistance = 18f;
            AvatarSnapGap = 0f;
            InputPopScale = 1f;
            InputPopRiseCurve = DefaultRiseCurve();
            InputPopFadeCurve = DefaultFadeCurve();
            SelectionStarScale = 1f;
            SelectionStarOffsetX = 2f;
            SelectionStarOffsetY = -2f;
            var path = Path.Combine(Application.streamingAssetsPath, "config.txt");
            try
            {
                if (!File.Exists(path)) return;
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Split('#')[0].Trim();
                    if (line.Length == 0) continue;
                    if (line.StartsWith("["))
                    {
                        var headerLine = i + 1;
                        var rows = new List<string>();
                        while (i + 1 < lines.Length)
                        {
                            var row = lines[i + 1].Split('#')[0].Trim();
                            if (row.StartsWith("[") || row.Contains("=")) break;
                            i++;
                            if (row.Length > 0) rows.Add(row);
                        }
                        if (line != "[inputPopRise]" && line != "[inputPopFade]")
                        {
                            Debug.LogWarning($"config.txt 第 {headerLine} 行：未知表格 {line}。");
                            continue;
                        }
                        var fade = line == "[inputPopFade]";
                        if (!TryParsePopCurve(rows.ToArray(), fade, out var points))
                        {
                            Debug.LogWarning($"config.txt 第 {headerLine} 行：{line} 表格无效，已忽略。每行两列（秒数 数值），空格或Tab分隔；需要2～64行，从0秒开始且时间严格递增（最多60秒）；透明度0～1，速度倍率0～10。");
                            continue;
                        }
                        if (fade) InputPopFadeCurve = points;
                        else InputPopRiseCurve = points;
                        continue;
                    }
                    var separator = line.IndexOf('=');
                    if (separator <= 0)
                    {
                        Debug.LogWarning($"config.txt 第 {i + 1} 行格式错误，应为 名称=数值。");
                        continue;
                    }
                    var key = line.Substring(0, separator).Trim();
                    var raw = line.Substring(separator + 1).Trim();
                    float min, max;
                    switch (key)
                    {
                        case "avatarSnapDistance": min = 0f; max = 256f; break;
                        case "avatarSnapStartDistance":
                        case "avatarSnapGap": min = 0f; max = 256f; break;
                        case "inputPopScale":
                        case "selectionStarScale": min = 0.1f; max = 10f; break;
                        case "selectionStarOffsetX":
                        case "selectionStarOffsetY": min = -1000f; max = 1000f; break;
                        default:
                            Debug.LogWarning($"config.txt 第 {i + 1} 行：未知参数 {key}。");
                            continue;
                    }
                    if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                        || float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                    {
                        Debug.LogWarning($"config.txt 第 {i + 1} 行：{key} 应为 {min}～{max} 的数字，本行已忽略。");
                        continue;
                    }
                    switch (key)
                    {
                        case "avatarSnapDistance": AvatarSnapDistance = value; break;
                        case "avatarSnapStartDistance": AvatarSnapStartDistance = value; break;
                        case "avatarSnapGap": AvatarSnapGap = value; break;
                        case "inputPopScale": InputPopScale = value; break;
                        case "selectionStarScale": SelectionStarScale = value; break;
                        case "selectionStarOffsetX": SelectionStarOffsetX = value; break;
                        case "selectionStarOffsetY": SelectionStarOffsetY = value; break;
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"无法读取临时配置 {path}，使用默认参数：{e.Message}");
            }
        }

        static Vector2[] DefaultRiseCurve() => new[] { new Vector2(0f, 1f), new Vector2(0.55f, 1f) };
        static Vector2[] DefaultFadeCurve() => new[]
        {
            new Vector2(0f, 0f), new Vector2(0.1375f, 0f), new Vector2(0.55f, 1f)
        };

        internal static bool TryParsePopCurve(string[] entries, bool fade, out Vector2[] points)
        {
            points = null;
            if (entries.Length < 2 || entries.Length > 64) return false;
            var parsed = new Vector2[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var pair = entries[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (pair.Length != 2
                    || !float.TryParse(pair[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var time)
                    || !float.TryParse(pair[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    || float.IsNaN(time) || float.IsInfinity(time)
                    || float.IsNaN(value) || float.IsInfinity(value)
                    || time < 0f || time > 60f || value < 0f || value > (fade ? 1f : 10f)) return false;
                if (i == 0 ? time != 0f : time <= parsed[i - 1].x) return false;
                parsed[i] = new Vector2(time, value);
            }
            points = parsed;
            return true;
        }

        [Header("系统")]
        [Tooltip("桌上最多几个好友")]
        [Range(1, 30)]
        public int maxDesktopFriends = 30;

        [Tooltip("一共拉取多少好友（桌上+麻袋）")]
        [Range(1, 64)]
        public int maxCollectFriends = 64;

        [Tooltip("桌面头像边长")]
        [Range(48, 256)]
        public float chipSize = 128f;

        [Tooltip("右键长按隐藏时间（秒）")]
        [Min(0.1f)]
        public float stealthHoldSeconds = 2f;

        [Header("聊天")]
        [Tooltip("每条会话最多存几条")]
        [Min(1)]
        public int maxMessagesPerFriend = 200;

        [Tooltip("简易聊天最多显示几条")]
        [Min(1)]
        public int maxCompactChatMessages = 5;

        [Tooltip("未读显示封顶")]
        [Min(1)]
        public int unreadCap = 99;

        [Tooltip("未读气泡轮播间隔（秒）")]
        [Min(0.1f)]
        public float bubbleRotateSeconds = 5f;

        [Tooltip("双击 Ctrl 最大间隔（秒）")]
        [Min(0.1f)]
        public float doubleControlSeconds = 0.4f;

        [Header("交互")]
        [Tooltip("互动连点间隔（秒）")]
        [Min(0f)]
        public float interactCooldown = 0.1f;

        [Tooltip("悬停打开（秒）")]
        [Min(0f)]
        public float hoverOpenSeconds = 0.2f;

        [Tooltip("扔番茄扣点击")]
        [Min(0)]
        public int tomatoTapCost = 1000;

        public string FormatUnread(int count)
        {
            var cap = Mathf.Max(1, unreadCap);
            return count > cap ? cap + "+" : count.ToString();
        }

        public static OverlayConfig LoadOrDefault()
        {
            var resource = Resources.Load<OverlayConfig>("OverlayConfig");
            if (resource != null)
            {
                return resource;
            }

            var loaded = Resources.FindObjectsOfTypeAll<OverlayConfig>();
            for (var i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null)
                {
                    return loaded[i];
                }
            }

#if UNITY_EDITOR
            var fromDisk = UnityEditor.AssetDatabase.LoadAssetAtPath<OverlayConfig>(AssetPath);
            if (fromDisk != null)
            {
                return fromDisk;
            }
#endif

            return CreateInstance<OverlayConfig>();
        }
    }
}
