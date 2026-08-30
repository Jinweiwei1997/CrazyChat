#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CrazyChat.Overlay
{
    /// <summary>
    /// 本应用聊天：记录只写本地；发送走 Steam P2P，双方都开着本游戏才能送到。
    /// </summary>
    public sealed class OverlayChatService : MonoBehaviour
    {
        const int Channel = 1;
        const string Prefix = "CC1|";

        public OverlayChatStore Store { get; private set; }

        readonly List<PendingSend> _pending = new List<PendingSend>();
        readonly IntPtr[] _receiveBuffer = new IntPtr[16];
        float _nextRetry;

#if !DISABLESTEAMWORKS
        Callback<SteamNetworkingMessagesSessionRequest_t> _sessionCallback;
#endif

        public void Bind(OverlayChatStore store)
        {
            Store = store;
        }

        void OnEnable()
        {
#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                return;
            }

            SteamNetworkingUtils.InitRelayNetworkAccess();
            _sessionCallback = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
#endif
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
            if (!SteamManager.Initialized)
            {
                return;
            }

            ReceiveP2P();
            if (Time.unscaledTime >= _nextRetry)
            {
                _nextRetry = Time.unscaledTime + 5f;
                FlushPending();
            }
#endif
        }

        public void Send(ulong friendId, string text)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0 || Store == null)
            {
                return;
            }

            var localId = 0UL;
#if !DISABLESTEAMWORKS
            if (SteamManager.Initialized)
            {
                localId = SteamUser.GetSteamID().m_SteamID;
            }
#endif
            Store.Add(friendId, text, true, localId);

#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized || SendP2P(friendId, text))
            {
                return;
            }

            _pending.Add(new PendingSend { friendId = friendId, text = text });
#endif
        }

#if !DISABLESTEAMWORKS
        void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t ev)
        {
            var identity = ev.m_identityRemote;
            SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
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

                var message = SteamNetworkingMessage_t.FromIntPtr(ptr);
                var identity = message.m_identityPeer;
                var friendId = identity.GetSteamID64();
                var text = DecodePayload(message.m_pData, message.m_cbSize);
                SteamNetworkingMessage_t.Release(ptr);
                if (Store != null && !string.IsNullOrEmpty(text))
                {
                    Store.Add(friendId, text, false, friendId);
                }
            }
        }

        static bool SendP2P(ulong friendId, string text)
        {
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(friendId);
            var bytes = Encoding.UTF8.GetBytes(Prefix + text);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var result = SteamNetworkingMessages.SendMessageToUser(
                ref identity,
                handle.AddrOfPinnedObject(),
                (uint)bytes.Length,
                Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession,
                Channel);
            handle.Free();
            return result == EResult.k_EResultOK;
        }

        static string DecodePayload(IntPtr data, int size)
        {
            if (data == IntPtr.Zero || size <= 0)
            {
                return null;
            }

            var bytes = new byte[size];
            Marshal.Copy(data, bytes, 0, size);
            var raw = Encoding.UTF8.GetString(bytes);
            return raw.StartsWith(Prefix, StringComparison.Ordinal) ? raw.Substring(Prefix.Length) : raw;
        }

        void FlushPending()
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (SendP2P(_pending[i].friendId, _pending[i].text))
                {
                    _pending.RemoveAt(i);
                }
            }
        }
#endif

        struct PendingSend
        {
            public ulong friendId;
            public string text;
        }
    }
}
