using System;
using System.Collections.Generic;
using System.Text;

namespace CrazyChat.Overlay.Interact
{
    /// <summary>
    /// Channel-2 payloads for A/B presence (IX1| body without prefix).
    /// </summary>
    public static class OverlayAvatarSync
    {
        public const string PresencePrefix = "ab|p|";
        public const string VersionPrefix = "ab|v|";
        public const string RequestPrefix = "ab|r|";
        public const string ChunkPrefix = "ab|c|";
        public const int ChunkChars = 12000;

        public static string EncodePresence(bool active)
        {
            return PresencePrefix + (active ? "1" : "0");
        }

        public static bool TryDecodePresence(string actionId, out bool active)
        {
            active = false;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(PresencePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            active = actionId.Length > PresencePrefix.Length && actionId[PresencePrefix.Length] == '1';
            return true;
        }

        public static string EncodeVersion(int version)
        {
            return VersionPrefix + version;
        }

        public static bool TryDecodeVersion(string actionId, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(VersionPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(actionId.Substring(VersionPrefix.Length), out version);
        }

        public static string EncodeRequest(int version)
        {
            return RequestPrefix + version;
        }

        public static bool TryDecodeRequest(string actionId, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(RequestPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(actionId.Substring(RequestPrefix.Length), out version);
        }

        public static List<string> EncodeChunks(char slot, int version, byte[] png)
        {
            var list = new List<string>();
            if (png == null || png.Length == 0)
            {
                return list;
            }

            var b64 = Convert.ToBase64String(png);
            var total = (b64.Length + ChunkChars - 1) / ChunkChars;
            if (total <= 0)
            {
                total = 1;
            }

            for (var i = 0; i < total; i++)
            {
                var start = i * ChunkChars;
                var len = Math.Min(ChunkChars, b64.Length - start);
                var piece = b64.Substring(start, len);
                list.Add(ChunkPrefix + slot + "|" + version + "|" + i + "|" + total + "|" + piece);
            }

            return list;
        }

        public static bool TryDecodeChunk(string actionId, out char slot, out int version, out int index,
            out int total, out string b64Piece)
        {
            slot = 'A';
            version = 0;
            index = 0;
            total = 0;
            b64Piece = null;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(ChunkPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var raw = actionId.Substring(ChunkPrefix.Length);
            var parts = raw.Split(new[] { '|' }, 5);
            if (parts.Length < 5 || parts[0].Length == 0)
            {
                return false;
            }

            slot = parts[0][0];
            if (!int.TryParse(parts[1], out version) || !int.TryParse(parts[2], out index) ||
                !int.TryParse(parts[3], out total))
            {
                return false;
            }

            b64Piece = parts[4];
            return total > 0 && index >= 0 && index < total;
        }

        public static bool TryAssemble(Dictionary<int, string> parts, int total, out byte[] png)
        {
            png = null;
            if (parts == null || total <= 0 || parts.Count < total)
            {
                return false;
            }

            var sb = new StringBuilder(total * ChunkChars);
            for (var i = 0; i < total; i++)
            {
                if (!parts.TryGetValue(i, out var piece))
                {
                    return false;
                }

                sb.Append(piece);
            }

            try
            {
                png = Convert.FromBase64String(sb.ToString());
                return png != null && png.Length > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
