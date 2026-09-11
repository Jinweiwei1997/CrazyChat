using UnityEngine;

namespace CrazyChat.Overlay
{
    [CreateAssetMenu(fileName = "OverlayConfig", menuName = "CrazyChat/Overlay Config")]
    public sealed class OverlayConfig : ScriptableObject
    {
        public const string AssetPath = "Assets/Resources/OverlayConfig.asset";

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
