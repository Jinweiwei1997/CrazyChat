using System;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Pure session-lease helpers (no Unity / Steam). Used by OverlaySessionGuard and console tests.
    /// </summary>
    public static class OverlaySessionLease
    {
        public const string FileName = "crazychat_session_lease.json";
        public const float HeartbeatSeconds = 5f;
        public const float StaleAfterSeconds = 45f;
        public const float ExitNoticeSeconds = 2.5f;

        [Serializable]
        public sealed class Payload
        {
            public string sessionId;
            public long startedUnix;
            public long heartbeatUnix;
        }

        public static string ToJson(string sessionId, long startedUnix, long heartbeatUnix)
        {
            // Minimal JSON — avoid UnityEngine.JsonUtility so console tests can link this file.
            return "{\"sessionId\":\"" + Escape(sessionId) + "\",\"startedUnix\":" + startedUnix +
                   ",\"heartbeatUnix\":" + heartbeatUnix + "}";
        }

        public static bool TryParse(string json, out Payload payload)
        {
            payload = null;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            var sessionId = ReadString(json, "sessionId");
            if (string.IsNullOrEmpty(sessionId))
            {
                return false;
            }

            if (!TryReadLong(json, "startedUnix", out var started))
            {
                return false;
            }

            if (!TryReadLong(json, "heartbeatUnix", out var heartbeat))
            {
                return false;
            }

            payload = new Payload
            {
                sessionId = sessionId,
                startedUnix = started,
                heartbeatUnix = heartbeat
            };
            return true;
        }

        /// <summary>
        /// Yield when another session owns a fresh lease (takeover). Stale foreign leases are ignored.
        /// </summary>
        public static bool ShouldYield(Payload local, Payload remote, long nowUnix, float staleAfterSeconds)
        {
            if (local == null || remote == null || string.IsNullOrEmpty(local.sessionId) ||
                string.IsNullOrEmpty(remote.sessionId))
            {
                return false;
            }

            if (string.Equals(local.sessionId, remote.sessionId, StringComparison.Ordinal))
            {
                return false;
            }

            var age = nowUnix - remote.heartbeatUnix;
            if (age < 0)
            {
                age = 0;
            }

            if (age > staleAfterSeconds)
            {
                return false;
            }

            return true;
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static string ReadString(string json, string key)
        {
            var token = "\"" + key + "\"";
            var keyIndex = json.IndexOf(token, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return null;
            }

            var colon = json.IndexOf(':', keyIndex + token.Length);
            if (colon < 0)
            {
                return null;
            }

            var firstQuote = json.IndexOf('"', colon + 1);
            if (firstQuote < 0)
            {
                return null;
            }

            var secondQuote = json.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0)
            {
                return null;
            }

            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        static bool TryReadLong(string json, string key, out long value)
        {
            value = 0;
            var token = "\"" + key + "\"";
            var keyIndex = json.IndexOf(token, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return false;
            }

            var colon = json.IndexOf(':', keyIndex + token.Length);
            if (colon < 0)
            {
                return false;
            }

            var i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i]))
            {
                i++;
            }

            var start = i;
            if (i < json.Length && json[i] == '-')
            {
                i++;
            }

            while (i < json.Length && char.IsDigit(json[i]))
            {
                i++;
            }

            if (i == start || (i == start + 1 && json[start] == '-'))
            {
                return false;
            }

            return long.TryParse(json.Substring(start, i - start), out value);
        }
    }
}
