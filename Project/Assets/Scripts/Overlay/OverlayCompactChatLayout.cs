namespace CrazyChat.Overlay
{
    /// <summary>
    /// Pure compact-chat sizing/scroll policy (no Unity types) for tests and OverlayChatUi.
    /// </summary>
    public static class OverlayCompactChatLayout
    {
        public static float FixedChromeHeight(float headerHeight, float statusHeight, float composerHeight)
        {
            return headerHeight + statusHeight + composerHeight + 1f;
        }

        public static float MaxBodyHeight(float chatHeight, float fixedChrome, float minBody)
        {
            var max = chatHeight - fixedChrome;
            return max > minBody ? max : minBody;
        }

        public static float ClampBodyHeight(float contentHeight, float minBody, float maxBody)
        {
            if (contentHeight < minBody)
            {
                return minBody;
            }

            if (contentHeight > maxBody)
            {
                return maxBody;
            }

            return contentHeight;
        }

        public static float CardHeight(float fixedChrome, float bodyHeight)
        {
            return fixedChrome + bodyHeight;
        }

        /// <summary>
        /// Scroll only when content cannot fit inside the compact max body.
        /// </summary>
        public static bool ShouldEnableVerticalScroll(float contentHeight, float maxBody)
        {
            return contentHeight > maxBody + 0.01f;
        }
    }
}
