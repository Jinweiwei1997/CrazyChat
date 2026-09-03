using UnityEngine;

namespace CrazyChat.Overlay
{
    [CreateAssetMenu(fileName = "OverlayConfig", menuName = "CrazyChat/Overlay Config")]
    public sealed class OverlayConfig : ScriptableObject
    {
        public const string AssetPath = "Assets/OverlayConfig.asset";

        public const bool SteamCloud = false;

        [Header("关=所有在线好友，开=只显示玩本游戏的")]
        public bool requireSameGame = true;

        [Header("只显示在线好友")]
        public bool onlineOnly = true;

        [Header("角落显示自己")]
        public bool includeLocalPlayer = true;

        [Header("桌上最多几个好友")]
        [Range(1, 30)]
        public int maxDesktopFriends = 30;

        [Header("一共拉取多少好友（桌上+麻袋）")]
        [Range(1, 64)]
        public int maxCollectFriends = 64;

        [Header("刷新间隔（秒）")]
        [Min(0.5f)]
        public float pollSeconds = 3f;

        [Header("桌面头像边长")]
        [Range(48, 256)]
        public float chipSize = 128f;

        [Header("捕捉回落（秒）")]
        [Min(0.05f)]
        public float reactionSeconds = 0.12f;

        [Header("每条会话最多存几条")]
        [Min(1)]
        public int maxMessagesPerFriend = 200;

        [Header("未读显示封顶")]
        [Min(1)]
        public int unreadCap = 99;

        [Header("彩蛋间隔（敲击次数）")]
        [Min(1)]
        public int easterEggEvery = 1000;

        [Header("互动连点间隔（秒）")]
        [Min(0f)]
        public float interactCooldown = 0.1f;

        [Header("悬停打开（秒）")]
        [Min(0f)]
        public float hoverOpenSeconds = 0.2f;

        [Header("扔番茄扣点击")]
        [Min(0)]
        public int tomatoTapCost = 1000;

        public string FormatUnread(int count)
        {
            var cap = Mathf.Max(1, unreadCap);
            return count > cap ? cap + "+" : count.ToString();
        }

        public static OverlayConfig LoadOrDefault()
        {
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
