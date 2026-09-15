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
        // Includes the 12,000-character A/B image chunks and their protocol headers.
        const int MaxPayloadBytes = 16384;
        const string Prefix = "IX1|";

        public event Action<ulong, string> Received;

        readonly IntPtr[] _receiveBuffer = new IntPtr[8];

#if !DISABLESTEAMWORKS
        Callback<SteamNetworkingMessagesSessionRequest_t> _sessionCallback;
#endif

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

        public void Send(ulong friendId, string actionId)
        {
            if (friendId == 0 || PlayingFriendsService.IsTestFriend(friendId) || string.IsNullOrEmpty(actionId))
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
            if (bytes.Length > MaxPayloadBytes) return;
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
                    var actionId = Decode(message.m_pData, message.m_cbSize);

                    if (!string.IsNullOrEmpty(actionId))
                    {
                        Received?.Invoke(friendId, actionId);
                    }
                }
                finally
                {
                    SteamNetworkingMessage_t.Release(ptr);
                }
            }
        }

        static string Decode(IntPtr data, int size)
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
