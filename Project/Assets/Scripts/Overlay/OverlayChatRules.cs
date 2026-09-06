using System.Collections.Generic;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Chat list windowing rules (compact vs full).
    /// </summary>
    public static class OverlayChatRules
    {
        /// <summary>
        /// Index of the latest peer (non-mine) message, or messages.Count if none.
        /// Compact UI shows [index .. end).
        /// </summary>
        public static int StartIndexFromLastPeer(IReadOnlyList<OverlayChatMessage> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return 0;
            }

            for (var i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i] != null && !messages[i].mine)
                {
                    return i;
                }
            }

            return messages.Count;
        }
    }
}
