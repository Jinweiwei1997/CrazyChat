#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CrazyChat.Overlay
{
    /// <summary>
    /// 本应用聊天：记录只写本地；发送走 Steam P2P，对方当时没开本游戏就送不到。
    /// </summary>
    public sealed class OverlayChatService : MonoBehaviour
    {
        const int Channel = 1;
        // Existing input limit is 200 UTF-16 characters; allow up to 800 UTF-8 bytes plus CC1|.
        const int MaxPayloadBytes = 804;
        const string Prefix = "CC1|";

        public OverlayChatStore Store { get; private set; }

        readonly IntPtr[] _receiveBuffer = new IntPtr[16];
        int _nextMessageId = 1;

#if !DISABLESTEAMWORKS
        Callback<SteamNetworkingMessagesSessionRequest_t> _sessionCallback;
#endif

        public void Bind(OverlayChatStore store)
        {
            Store = store;
        }

        void OnEnable()
        {
            EnsureSteam();
        }

        void OnDisable()
        {
#if !DISABLESTEAMWORKS
            _sessionCallback?.Dispose();
            _sessionCallback = null;
#endif
        }

        void Update()
        {
#if !DISABLESTEAMWORKS
            EnsureSteam();
            if (!SteamManager.Initialized)
            {
                return;
            }

            ReceiveP2P();
#endif
        }

        public bool Send(ulong friendId, string text)
        {
            return Send(friendId, text, null, null);
        }

        public bool Send(ulong friendId, string text, string replyTo, string replyText)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0 || Store == null)
            {
                return false;
            }

            var localId = 0UL;
#if !DISABLESTEAMWORKS
            if (SteamManager.Initialized)
            {
                localId = SteamUser.GetSteamID().m_SteamID;
            }
#endif
            replyTo = SingleLine(replyTo);
            replyText = Clip(SingleLine(replyText), 36);
            var id = NextId(localId);
            var body = EncodeBody(id, replyTo, replyText, text);
            if (Encoding.UTF8.GetByteCount(body) > MaxPayloadBytes - Prefix.Length)
            {
                replyText = string.Empty;
                body = EncodeBody(id, replyTo, replyText, text);
                if (Encoding.UTF8.GetByteCount(body) > MaxPayloadBytes - Prefix.Length)
                {
                    return false;
                }
            }

            Store.Add(friendId, text, true, localId, id, replyTo, replyText);

#if !DISABLESTEAMWORKS
            if (SteamManager.Initialized && !PlayingFriendsService.IsTestFriend(friendId))
            {
                SendP2P(friendId, body);
            }
#endif
            return true;
        }

        string NextId(ulong steamId)
        {
            var seq = _nextMessageId++;
            if (_nextMessageId > 0xFFFF)
            {
                _nextMessageId = 1;
            }

            return ((ushort)steamId).ToString("x4") +
                   DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString("x") +
                   seq.ToString("x");
        }

        static string EncodeBody(string id, string replyTo, string replyText, string text)
        {
            return "m\n" + SingleLine(id) + "\n" + SingleLine(replyTo) + "\n" +
                   SingleLine(replyText) + "\n" + SingleLine(text);
        }

        static bool TryReadEnvelope(string body, out string id, out string replyTo, out string replyText, out string text)
        {
            id = null;
            replyTo = null;
            replyText = null;
            text = null;
            if (string.IsNullOrEmpty(body) || !body.StartsWith("m\n", StringComparison.Ordinal))
            {
                return false;
            }

            var parts = body.Substring(2).Split(new[] { '\n' }, 4);
            if (parts.Length < 4 || string.IsNullOrEmpty(parts[0]))
            {
                return false;
            }

            id = parts[0];
            replyTo = parts[1];
            replyText = parts[2];
            text = (parts[3] ?? string.Empty).Trim();
            return text.Length > 0;
        }

        static string SingleLine(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        static string Clip(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, max) + "…";
        }

#if !DISABLESTEAMWORKS
        void EnsureSteam()
        {
            if (_sessionCallback != null || !SteamManager.Initialized)
            {
                return;
            }

            SteamNetworkingUtils.InitRelayNetworkAccess();
            _sessionCallback = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
        }

        void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t ev)
        {
            var identity = ev.m_identityRemote;
            if (SteamFriends.GetFriendRelationship(new CSteamID(identity.GetSteamID64())) ==
                EFriendRelationship.k_EFriendRelationshipFriend)
            {
                SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
            }
        }

        void ReceiveP2P()
        {
            var count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, _receiveBuffer, _receiveBuffer.Length);
            for (var i = 0; i < count; i++)
            {
                var ptr = _receiveBuffer[i];
                if (ptr == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    var message = SteamNetworkingMessage_t.FromIntPtr(ptr);
                    var identity = message.m_identityPeer;
                    var friendId = identity.GetSteamID64();
                    if (SteamFriends.GetFriendRelationship(new CSteamID(friendId)) !=
                        EFriendRelationship.k_EFriendRelationshipFriend)
                    {
                        continue;
                    }
                    var body = DecodePayload(message.m_pData, message.m_cbSize);
                    if (Store == null || string.IsNullOrEmpty(body))
                    {
                        continue;
                    }

                    if (TryReadEnvelope(body, out var id, out var replyTo, out var replyText, out var text))
                    {
                        Store.Add(friendId, text, false, friendId, id, replyTo, replyText);
                    }
                    else
                    {
                        Store.Add(friendId, body, false, friendId);
                    }
                }
                finally
                {
                    SteamNetworkingMessage_t.Release(ptr);
                }
            }
        }

        static void SendP2P(ulong friendId, string text)
        {
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(friendId);
            var bytes = Encoding.UTF8.GetBytes(Prefix + text);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                SteamNetworkingMessages.SendMessageToUser(
                    ref identity,
                    handle.AddrOfPinnedObject(),
                    (uint)bytes.Length,
                    Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession,
                    Channel);
            }
            finally
            {
                handle.Free();
            }
        }

        static string DecodePayload(IntPtr data, int size)
        {
            if (data == IntPtr.Zero || size <= Prefix.Length || size > MaxPayloadBytes)
            {
                return null;
            }

            var bytes = new byte[size];
            Marshal.Copy(data, bytes, 0, size);
            var raw = Encoding.UTF8.GetString(bytes);
            return raw.StartsWith(Prefix, StringComparison.Ordinal) ? raw.Substring(Prefix.Length) : null;
        }
#endif
    }
}
