using System;
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

        public float InputPopScale { get; private set; } = 1f;
        public float InputPopSpeedScale { get; private set; } = 1f;
        public float SelectionStarScale { get; private set; } = 1f;
        public float SelectionStarOffsetX { get; private set; } = 2f;
        public float SelectionStarOffsetY { get; private set; } = -2f;

        public void LoadTemporarySettings()
        {
            InputPopScale = 1f;
            InputPopSpeedScale = 1f;
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
                        case "inputPopScale":
                        case "selectionStarScale": min = 0.1f; max = 10f; break;
                        case "inputPopSpeedScale": min = 0.1f; max = 10f; break;
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
                        case "inputPopScale": InputPopScale = value; break;
                        case "inputPopSpeedScale": InputPopSpeedScale = value; break;
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
