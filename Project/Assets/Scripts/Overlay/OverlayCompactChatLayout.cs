namespace CrazyChat.Overlay
{
    /// <summary>
    /// Pure compact-chat sizing/scroll/session policy (no Unity types).
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

        public static bool ShouldEnableVerticalScroll(float contentHeight, float maxBody)
        {
            return contentHeight > maxBody + 0.01f;
        }

        /// <summary>
        /// Re-opening the same friend must keep the session floor; otherwise each Open+Send
        /// pair collapses the compact list to only the newest message.
        /// </summary>
        public static bool ShouldReuseCompactSession(bool isOpen, ulong openFriendId, ulong friendId)
        {
            return isOpen && openFriendId != 0UL && openFriendId == friendId;
        }

        /// <summary>
        /// firstUnreadPeerIndex: index of earliest unread peer message, or -1.
        /// latestPeerIndex: index of latest peer message, or -1.
        /// </summary>
        public static int ResolveSessionStartIndex(
            int messageCount,
            int firstUnreadPeerIndex,
            int latestPeerIndex)
        {
            if (messageCount <= 0)
            {
                return 0;
            }

            if (firstUnreadPeerIndex >= 0 && firstUnreadPeerIndex < messageCount)
            {
                return firstUnreadPeerIndex;
            }

            if (latestPeerIndex >= 0 && latestPeerIndex < messageCount)
            {
                return latestPeerIndex;
            }

            // Outbound-only thread: show only messages sent after this open.
            return messageCount;
        }

        public static int ClampVisibleStart(int sessionStart, int messageCount, int maxVisible)
        {
            if (messageCount <= 0)
            {
                return 0;
            }

            var max = maxVisible < 1 ? 1 : maxVisible;
            var start = sessionStart;
            if (start < 0)
            {
                start = 0;
            }

            if (start > messageCount)
            {
                start = messageCount;
            }

            if (messageCount - start > max)
            {
                start = messageCount - max;
            }

            return start;
        }
    }
}
