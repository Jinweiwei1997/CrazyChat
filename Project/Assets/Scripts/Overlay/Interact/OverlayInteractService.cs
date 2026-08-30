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

namespace CrazyChat.Overlay.Interact
{
    /// <summary>
    /// 互动走独立 P2P 通道，不进聊天记录。对方没开本游戏就收不到。
    /// </summary>
    public sealed class OverlayInteractService : MonoBehaviour
    {
        const int Channel = 2;
        const string Prefix = "IX1|";

        public event Action<ulong, string> Received;

        readonly IntPtr[] _receiveBuffer = new IntPtr[8];

#if !DISABLESTEAMWORKS
        Callback<SteamNetworkingMessagesSessionRequest_t> _sessionCallback;
#endif

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
#endif
        }

        public void Send(ulong friendId, string actionId)
        {
            if (friendId == 0 || string.IsNullOrEmpty(actionId))
            {
                return;
            }

#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                return;
            }

            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(friendId);
            var bytes = Encoding.UTF8.GetBytes(Prefix + actionId);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            SteamNetworkingMessages.SendMessageToUser(
                ref identity,
                handle.AddrOfPinnedObject(),
                (uint)bytes.Length,
                Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession,
                Channel);
            handle.Free();
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
                var actionId = Decode(message.m_pData, message.m_cbSize);
                SteamNetworkingMessage_t.Release(ptr);
                if (!string.IsNullOrEmpty(actionId))
                {
                    Received?.Invoke(friendId, actionId);
                }
            }
        }

        static string Decode(IntPtr data, int size)
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
