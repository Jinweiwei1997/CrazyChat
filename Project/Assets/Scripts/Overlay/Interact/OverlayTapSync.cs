namespace CrazyChat.Overlay.Interact
{
    /// <summary>
    /// 点击反应走互动通道，不进聊天、不进互动菜单。
    /// </summary>
    public static class OverlayTapSync
    {
        public const string Prefix = "tap|";

        public static string Encode(OverlayClickEffect effect, int vk = 0)
        {
            var payload = Prefix + (int)effect;
            if (vk != 0)
            {
                payload += "|" + vk;
            }

            return payload;
        }

        public static bool TryDecode(string actionId, out OverlayClickEffect effect, out int vk)
        {
            effect = OverlayClickEffect.Elastic;
            vk = 0;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(Prefix))
            {
                return false;
            }

            var raw = actionId.Substring(Prefix.Length);
            var split = raw.IndexOf('|');
            var effectRaw = split >= 0 ? raw.Substring(0, split) : raw;
            if (effectRaw == ((int)OverlayClickEffect.Flip).ToString())
            {
                effect = OverlayClickEffect.Flip;
            }

            if (split >= 0)
            {
                int.TryParse(raw.Substring(split + 1), out vk);
            }

            return true;
        }
    }
}
