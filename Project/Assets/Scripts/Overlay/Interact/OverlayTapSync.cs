namespace CrazyChat.Overlay.Interact
{
    /// <summary>
    /// 按键图标同步走互动通道，不进聊天、不进互动菜单。
    /// </summary>
    public static class OverlayTapSync
    {
        public const string Prefix = "tap|";

        public static string Encode(int vk)
        {
            return Prefix + "i|" + vk;
        }

        public static bool TryDecode(string actionId, out int vk)
        {
            vk = 0;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(Prefix))
            {
                return false;
            }

            var raw = actionId.Substring(Prefix.Length);
            var split = raw.IndexOf('|');
            if (split < 0)
            {
                return true;
            }

            // Also accepts the old tap|effect|vk payload during rolling upgrades.
            int.TryParse(raw.Substring(split + 1), out vk);
            return true;
        }
    }
}
