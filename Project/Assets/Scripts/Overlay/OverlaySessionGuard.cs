#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Text;
using System.Threading;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Local single-instance mutex + optional Steam Remote Storage session lease (cross-device takeover).
    /// </summary>
    public sealed class OverlaySessionGuard : MonoBehaviour
    {
        const string MutexName = "Local\\CrazyChat.Overlay.SingleInstance";

        static Mutex _mutex;
        static bool _mutexOwned;

        string _sessionId;
        long _startedUnix;
        float _nextHeartbeatAt;
        float _nextPollAt;
        float _exitAt = -1f;
        string _exitMessage;
        bool _leaseActive;
        bool _exiting;

        public static bool TryAcquireLocalMutex()
        {
            if (_mutexOwned)
            {
                return true;
            }

            try
            {
                _mutex = new Mutex(false, MutexName);
                _mutexOwned = _mutex.WaitOne(0);
                if (!_mutexOwned)
                {
                    _mutex.Dispose();
                    _mutex = null;
                }

                return _mutexOwned;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 本机互斥获取异常，降级放行: " + e.Message);
                ReleaseLocalMutex();
                return true;
            }
        }

        public static void ReleaseLocalMutex()
        {
            if (_mutex == null)
            {
                _mutexOwned = false;
                return;
            }

            try
            {
                if (_mutexOwned)
                {
                    _mutex.ReleaseMutex();
                }
            }
            catch (Exception)
            {
            }

            try
            {
                _mutex.Dispose();
            }
            catch (Exception)
            {
            }

            _mutex = null;
            _mutexOwned = false;
        }

        public static void BeginQuit(string message)
        {
            var go = new GameObject("CrazyChatSessionQuit");
            DontDestroyOnLoad(go);
            var guard = go.AddComponent<OverlaySessionGuard>();
            guard.ScheduleExit(message);
        }

        public void StartLeaseIfPossible()
        {
#if DISABLESTEAMWORKS
            return;
#else
            if (!SteamManager.Initialized)
            {
                return;
            }

            _sessionId = Guid.NewGuid().ToString("N");
            _startedUnix = NowUnix();
            if (!WriteLease(_startedUnix))
            {
                Debug.LogWarning("[Overlay] 会话租约写入失败，跨设备顶号降级。");
                _leaseActive = false;
                return;
            }

            _leaseActive = true;
            _nextHeartbeatAt = Time.unscaledTime + OverlaySessionLease.HeartbeatSeconds;
            _nextPollAt = Time.unscaledTime + OverlaySessionLease.HeartbeatSeconds;
#endif
        }

        void Update()
        {
            if (_exiting)
            {
                if (_exitAt > 0f && Time.unscaledTime >= _exitAt)
                {
                    QuitNow();
                }

                return;
            }

            if (!_leaseActive)
            {
                return;
            }

#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                _leaseActive = false;
                return;
            }

            var now = Time.unscaledTime;
            if (now >= _nextPollAt)
            {
                _nextPollAt = now + OverlaySessionLease.HeartbeatSeconds;
                PollLease();
            }

            if (!_exiting && now >= _nextHeartbeatAt)
            {
                _nextHeartbeatAt = now + OverlaySessionLease.HeartbeatSeconds;
                Heartbeat();
            }
#endif
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(_exitMessage))
            {
                return;
            }

            const int width = 420;
            const int height = 64;
            var x = (Screen.width - width) / 2;
            var y = (Screen.height - height) / 2;
            GUI.Box(new Rect(x, y, width, height), _exitMessage);
        }

        void OnDestroy()
        {
            if (_exiting)
            {
                return;
            }

            // Mutex released from Bootstrap EndSteamSession / quit path.
        }

#if !DISABLESTEAMWORKS
        void Heartbeat()
        {
            if (!TryReadRemote(out var remote) || remote == null)
            {
                WriteLease(NowUnix());
                return;
            }

            var local = CurrentPayload(NowUnix());
            if (OverlaySessionLease.ShouldYield(local, remote, NowUnix(), OverlaySessionLease.StaleAfterSeconds))
            {
                ScheduleExit("已在其他设备登录 CrazyChat");
                return;
            }

            WriteLease(NowUnix());
        }

        void PollLease()
        {
            if (!TryReadRemote(out var remote) || remote == null)
            {
                return;
            }

            var local = CurrentPayload(NowUnix());
            if (OverlaySessionLease.ShouldYield(local, remote, NowUnix(), OverlaySessionLease.StaleAfterSeconds))
            {
                ScheduleExit("已在其他设备登录 CrazyChat");
            }
        }

        OverlaySessionLease.Payload CurrentPayload(long heartbeatUnix)
        {
            return new OverlaySessionLease.Payload
            {
                sessionId = _sessionId,
                startedUnix = _startedUnix,
                heartbeatUnix = heartbeatUnix
            };
        }

        bool WriteLease(long heartbeatUnix)
        {
            try
            {
                var json = OverlaySessionLease.ToJson(_sessionId, _startedUnix, heartbeatUnix);
                var bytes = Encoding.UTF8.GetBytes(json);
                return SteamRemoteStorage.FileWrite(OverlaySessionLease.FileName, bytes, bytes.Length);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 写会话租约失败: " + e.Message);
                return false;
            }
        }

        static bool TryReadRemote(out OverlaySessionLease.Payload payload)
        {
            payload = null;
            try
            {
                if (!SteamRemoteStorage.FileExists(OverlaySessionLease.FileName))
                {
                    return false;
                }

                var size = SteamRemoteStorage.GetFileSize(OverlaySessionLease.FileName);
                if (size <= 0)
                {
                    return false;
                }

                var buffer = new byte[size];
                var read = SteamRemoteStorage.FileRead(OverlaySessionLease.FileName, buffer, size);
                if (read <= 0)
                {
                    return false;
                }

                return OverlaySessionLease.TryParse(Encoding.UTF8.GetString(buffer, 0, read), out payload);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 读会话租约失败: " + e.Message);
                return false;
            }
        }
#endif

        void ScheduleExit(string message)
        {
            if (_exiting)
            {
                return;
            }

            _exiting = true;
            _leaseActive = false;
            _exitMessage = message;
            _exitAt = Time.unscaledTime + OverlaySessionLease.ExitNoticeSeconds;
            Debug.LogWarning("[Overlay] " + message);
        }

        static void QuitNow()
        {
            ReleaseLocalMutex();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static long NowUnix()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
