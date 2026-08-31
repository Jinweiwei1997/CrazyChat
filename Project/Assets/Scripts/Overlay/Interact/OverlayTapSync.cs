namespace CrazyChat.Overlay.Interact
{
    /// <summary>
    /// 点击反应走互动通道，不进聊天、不进互动菜单。
    /// </summary>
    public static class OverlayTapSync
    {
        public const string Prefix = "tap|";

        public static string Encode(OverlayClickEffect effect)
        {
            return Prefix + (int)effect;
        }

        public static bool TryDecode(string actionId, out OverlayClickEffect effect)
        {
            effect = OverlayClickEffect.Elastic;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(Prefix))
            {
                return false;
            }

            var raw = actionId.Substring(Prefix.Length);
            if (raw == ((int)OverlayClickEffect.Flip).ToString())
            {
                effect = OverlayClickEffect.Flip;
            }

            return true;
        }
    }
}
