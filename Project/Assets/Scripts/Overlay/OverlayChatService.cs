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
        const string Prefix = "CC1|";

        public OverlayChatStore Store { get; private set; }

        readonly IntPtr[] _receiveBuffer = new IntPtr[16];

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
            if (SteamManager.Initialized)
            {
                SendP2P(friendId, text);
            }
#endif
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

        static void SendP2P(ulong friendId, string text)
        {
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(friendId);
            var bytes = Encoding.UTF8.GetBytes(Prefix + text);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            SteamNetworkingMessages.SendMessageToUser(
                ref identity,
                handle.AddrOfPinnedObject(),
                (uint)bytes.Length,
                Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession,
                Channel);
            handle.Free();
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
#endif
    }
}
