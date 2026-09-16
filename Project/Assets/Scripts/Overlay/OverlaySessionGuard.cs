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
        const int MaxLeaseBytes = 4096;
        const float LeaseRequestTimeoutSeconds = 15f;
        bool _requestPending;
        float _requestDeadline;
#if !DISABLESTEAMWORKS
        CallResult<RemoteStorageFileReadAsyncComplete_t> _readResult;
        CallResult<RemoteStorageFileWriteAsyncComplete_t> _writeResult;
#endif
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
// Editor Play sessions are not cross-device game sessions. Avoid launching
// cloud operations that may still be pending when Play mode tears Steam down.
#if DISABLESTEAMWORKS || UNITY_EDITOR
            return;
#else
            if (!SteamManager.Initialized || _leaseActive || _exiting) return;

            _sessionId = Guid.NewGuid().ToString("N");
            _startedUnix = NowUnix();
            _readResult = CallResult<RemoteStorageFileReadAsyncComplete_t>.Create(OnLeaseRead);
            _writeResult = CallResult<RemoteStorageFileWriteAsyncComplete_t>.Create(OnLeaseWritten);
            _leaseActive = true;
            // Claim the session on startup; later heartbeats yield to a fresh foreign lease.
            WriteLeaseAsync();
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
            if (_requestPending)
            {
                if (now >= _requestDeadline)
                    DisableLease("异步请求超时");
                return;
            }

            if (now >= _nextHeartbeatAt) BeginLeaseCycle();
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

        void OnDisable()
        {
            _leaseActive = false;
            _requestPending = false;
#if !DISABLESTEAMWORKS
            _readResult?.Dispose();
            _writeResult?.Dispose();
            _readResult = null;
            _writeResult = null;
#endif
            // Local mutex remains owned until Bootstrap's quit path.
        }

#if !DISABLESTEAMWORKS
        void BeginLeaseCycle()
        {
            if (!_leaseActive || _exiting || _requestPending) return;
            try
            {
                var size = SteamRemoteStorage.GetFileSize(OverlaySessionLease.FileName);
                if (size <= 0)
                {
                    WriteLeaseAsync();
                    return;
                }
                if (size > MaxLeaseBytes)
                {
                    DisableLease("租约文件过大");
                    return;
                }

                var call = SteamRemoteStorage.FileReadAsync(OverlaySessionLease.FileName, 0, (uint)size);
                if (call == SteamAPICall_t.Invalid)
                {
                    DisableLease("无法发起异步读取");
                    return;
                }
                _requestPending = true;
                _requestDeadline = Time.unscaledTime + LeaseRequestTimeoutSeconds;
                _readResult.Set(call);
            }
            catch (Exception e)
            {
                DisableLease(e.Message);
            }
        }

        void OnLeaseRead(RemoteStorageFileReadAsyncComplete_t result, bool ioFailure)
        {
            if (!_leaseActive || _exiting) return;
            _requestPending = false;
            try
            {
                if (ioFailure || result.m_eResult != EResult.k_EResultOK ||
                    result.m_cubRead == 0 || result.m_cubRead > MaxLeaseBytes)
                {
                    DisableLease("异步读取失败");
                    return;
                }

                var buffer = new byte[(int)result.m_cubRead];
                // Complete only copies already-read bytes and must run inside this callback.
                if (!SteamRemoteStorage.FileReadAsyncComplete(result.m_hFileReadAsync, buffer, result.m_cubRead))
                {
                    DisableLease("读取结果获取失败");
                    return;
                }

                var now = NowUnix();
                var local = new OverlaySessionLease.Payload
                {
                    sessionId = _sessionId, startedUnix = _startedUnix, heartbeatUnix = now
                };
                if (OverlaySessionLease.TryParse(Encoding.UTF8.GetString(buffer), out var remote) &&
                    OverlaySessionLease.ShouldYield(local, remote, now, OverlaySessionLease.StaleAfterSeconds))
                {
                    ScheduleExit("已在其他设备登录 CrazyChat");
                    return;
                }

                WriteLeaseAsync();
            }
            catch (Exception e)
            {
                DisableLease(e.Message);
            }
        }

        void WriteLeaseAsync()
        {
            if (!_leaseActive || _exiting || _requestPending) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(
                    OverlaySessionLease.ToJson(_sessionId, _startedUnix, NowUnix()));
                var call = SteamRemoteStorage.FileWriteAsync(OverlaySessionLease.FileName, bytes, (uint)bytes.Length);
                if (call == SteamAPICall_t.Invalid)
                {
                    DisableLease("无法发起异步写入");
                    return;
                }
                _requestPending = true;
                _requestDeadline = Time.unscaledTime + LeaseRequestTimeoutSeconds;
                _writeResult.Set(call);
            }
            catch (Exception e)
            {
                DisableLease(e.Message);
            }
        }

        void OnLeaseWritten(RemoteStorageFileWriteAsyncComplete_t result, bool ioFailure)
        {
            if (!_leaseActive || _exiting) return;
            _requestPending = false;
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                DisableLease("异步写入失败");
                return;
            }
            _nextHeartbeatAt = Time.unscaledTime + OverlaySessionLease.HeartbeatSeconds;
        }

        void DisableLease(string reason)
        {
            _leaseActive = false;
            _requestPending = false;
            _readResult?.Cancel();
            _writeResult?.Cancel();
            Debug.LogWarning("[Overlay] 跨设备会话检测已降级，本机单实例仍有效: " + reason);
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
