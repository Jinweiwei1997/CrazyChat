using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Windows 下把游戏做成桌面透明置顶层：空白处点击穿透，好友头像可交互。
    /// </summary>
    public sealed class TransparentOverlayWindow : MonoBehaviour
    {

        GraphicRaycasterHost _raycasterHost;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        bool _clickThrough = true;
        bool _wantClickThrough = true;
        float _wantClickThroughSince;
        float _lastForceStealAt = -999f;
        float _nextHeartbeatAt;
#endif
        bool _applied;
        bool _alwaysOnTop = true;
        bool _suspendTopmost;
        bool _appFocused = true;
        int _targetDisplayIndex;
        int _appliedDisplayIndex = -1;
        Coroutine _moveDisplayRoutine;

        const float ClickThroughCaptureDebounce = 0.08f;
        const float ForceStealMinInterval = 0.5f;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int GwlStyle = -16;
        const int GwlExStyle = -20;
        const uint WsPopup = 0x80000000;
        const uint WsVisible = 0x10000000;
        const uint WsExLayered = 0x00080000;
        const uint WsExTransparent = 0x00000020;
        const uint WsExTopmost = 0x00000008;
        const uint WsExToolWindow = 0x00000080;
        const uint SwpFrameChanged = 0x0020;
        const uint SwpShowWindow = 0x0040;
        const uint SwpNoActivate = 0x0010;
        const uint SwpNoMove = 0x0002;
        const uint SwpNoSize = 0x0001;

        static readonly IntPtr HwndTopmost = new IntPtr(-1);
        static readonly IntPtr HwndNoTopmost = new IntPtr(-2);

        IntPtr _hwnd;

        [StructLayout(LayoutKind.Sequential)]
        struct Margins
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hWnd);


        [StructLayout(LayoutKind.Sequential)]
        struct CursorPoint { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        struct ClientRect { public int left, top, right, bottom; }
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out CursorPoint point);
        [DllImport("user32.dll")]
        static extern bool ScreenToClient(IntPtr window, ref CursorPoint point);
        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr window, out ClientRect rect);
        [DllImport("Dwmapi.dll")]
        static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins pMarInset);

        static bool Is64BitProcess => IntPtr.Size == 8;

        static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return Is64BitProcess
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        static void SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value)
        {
            if (Is64BitProcess)
            {
                SetWindowLongPtr64(hWnd, nIndex, value);
            }
            else
            {
                SetWindowLong32(hWnd, nIndex, value.ToInt32());
            }
        }
#endif

        public bool TryGetPointerPosition(out Vector2 position)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            position = default;
            if (!EnsureWindowHandle() || !GetCursorPos(out var point) ||
                !ScreenToClient(_hwnd, ref point) || !GetClientRect(_hwnd, out var rect))
                return false;
            var width = rect.right - rect.left;
            var height = rect.bottom - rect.top;
            if (width <= 0 || height <= 0) return false;
            // ScreenToClient handles monitors left/above the primary screen and window offsets.
            // Preserve out-of-window coordinates; never clamp another monitor onto a UI edge.
            position = new Vector2(point.x * (float)Screen.width / width,
                (height - point.y) * (float)Screen.height / height);
            return true;
#else
            position = Input.mousePosition;
            return true;
#endif
        }
        public void BindRaycaster(GraphicRaycasterHost host)
        {
            _raycasterHost = host;
            if (host != null) host.BindWindow(this);
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            if (_alwaysOnTop == enabled) return;
            _alwaysOnTop = enabled;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_applied && !_suspendTopmost)
            {
                ApplyTopmost();
            }
#endif
        }

        public static int GetAvailableDisplayCount()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var layout = new List<DisplayInfo>();
            Screen.GetDisplayLayout(layout);
            return Mathf.Max(1, layout.Count);
#else
            return Mathf.Max(1, Display.displays.Length);
#endif
        }

        public void SetTargetDisplay(int index)
        {
            var target = Mathf.Max(0, index);
            if (target == _targetDisplayIndex &&
                (_moveDisplayRoutine != null || _appliedDisplayIndex == target))
            {
                return;
            }

            _targetDisplayIndex = target;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!_applied || _appliedDisplayIndex == _targetDisplayIndex)
            {
                return;
            }

            if (_moveDisplayRoutine != null)
            {
                StopCoroutine(_moveDisplayRoutine);
            }
            _moveDisplayRoutine = StartCoroutine(MoveToTargetDisplay());
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        public IntPtr WindowHandle => _hwnd;

        /// <summary>
        /// Temporarily drop TOPMOST so Win32 file dialogs are visible and usable.
        /// </summary>
        public void SuspendTopmostForDialog(bool suspend)
        {
            _suspendTopmost = suspend;
            if (!_applied || !EnsureWindowHandle())
            {
                return;
            }

            if (suspend)
            {
                SetWindowPos(_hwnd, HwndNoTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
            }
            else
            {
                ApplyTopmost();
            }
        }
#else
        public IntPtr WindowHandle => IntPtr.Zero;

        public void SuspendTopmostForDialog(bool suspend)
        {
        }
#endif

        public bool HasKeyboardFocus
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return EnsureWindowHandle() && GetForegroundWindow() == _hwnd;
#else
                return Application.isFocused;
#endif
            }
        }

        public void FocusForTextInput(bool forceSteal = false)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!_applied || !EnsureWindowHandle()) return;
            // Opening chat/settings must take hits immediately (bypass capture debounce).
            ApplyClickThrough(false, immediate: true);
            if (GetForegroundWindow() == _hwnd)
            {
                SetFocus(_hwnd);
                return;
            }

            if (forceSteal)
            {
                // One-shot steal for keyboard open (double-Ctrl+Enter) / Esc priority dialogs.
                // Rate-limited so we never AttachThreadInput-spam against other apps.
                var now = Time.unscaledTime;
                var allowAttach = now - _lastForceStealAt >= ForceStealMinInterval;
                if (allowAttach)
                {
                    _lastForceStealAt = now;
                    var foreground = GetForegroundWindow();
                    var windowThread = GetWindowThreadProcessId(_hwnd, IntPtr.Zero);
                    var foregroundThread = foreground != IntPtr.Zero
                        ? GetWindowThreadProcessId(foreground, IntPtr.Zero)
                        : 0;
                    var attached = windowThread != 0 && foregroundThread != 0 && foregroundThread != windowThread &&
                                   AttachThreadInput(windowThread, foregroundThread, true);
                    try
                    {
                        SetForegroundWindow(_hwnd);
                        SetFocus(_hwnd);
                    }
                    finally
                    {
                        if (attached)
                        {
                            AttachThreadInput(windowThread, foregroundThread, false);
                        }
                    }
                }
                else
                {
                    SetForegroundWindow(_hwnd);
                    SetFocus(_hwnd);
                }
            }
            else
            {
                // Soft request only; mouse-driven opens usually already own last input.
                SetForegroundWindow(_hwnd);
            }
#endif
        }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            yield return null;
            yield return null;
            ApplyChrome();
#else
            yield break;
#endif
        }

        void OnApplicationFocus(bool focused)
        {
            _appFocused = focused;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!_applied)
            {
                return;
            }

            // Other apps took foreground: release hit-test immediately, never fight for focus.
            if (!focused)
            {
                ApplyClickThrough(true, immediate: true);
            }
#endif
        }

        void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!_applied)
            {
                return;
            }

            if (!_appFocused)
            {
                ApplyClickThrough(true, immediate: true);
                LogHeartbeat();
                return;
            }

            var overUi = _raycasterHost != null && _raycasterHost.IsPointerOverInteractive();
            ApplyClickThrough(!overUi, immediate: false);
            LogHeartbeat();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        void ApplyChrome()
        {
            _hwnd = GetActiveWindow();
            if (_hwnd == IntPtr.Zero || !IsWindow(_hwnd))
            {
                _hwnd = FindWindow("UnityWndClass", Application.productName);
            }

            if (!EnsureWindowHandle())
            {
                Debug.LogWarning("[Overlay] 找不到游戏窗口，无法启用置顶透明。");
                return;
            }

            SetWindowLongPtr(_hwnd, GwlStyle, new IntPtr(unchecked((int)(WsPopup | WsVisible))));

            ApplyTopmost();

            var margins = new Margins
            {
                cxLeftWidth = -1,
                cxRightWidth = -1,
                cyTopHeight = -1,
                cyBottomHeight = -1
            };
            DwmExtendFrameIntoClientArea(_hwnd, ref margins);

            SetWindowPos(_hwnd, _alwaysOnTop ? HwndTopmost : HwndNoTopmost, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpFrameChanged | SwpShowWindow);

            _clickThrough = false;
            _wantClickThrough = true;
            ApplyClickThrough(true, immediate: true);
            _applied = true;
            SetTargetDisplay(_targetDisplayIndex);
        }

        IEnumerator MoveToTargetDisplay()
        {
            var layout = new List<DisplayInfo>();
            Screen.GetDisplayLayout(layout);
            if (layout.Count == 0)
            {
                _moveDisplayRoutine = null;
                yield break;
            }

            var index = Mathf.Clamp(_targetDisplayIndex, 0, layout.Count - 1);
            var display = layout[index];
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
            yield return null;

            var move = Screen.MoveMainWindowTo(display, Vector2Int.zero);
            if (move != null)
            {
                yield return move;
            }

            _hwnd = IntPtr.Zero;
            if (!EnsureWindowHandle())
            {
                _moveDisplayRoutine = null;
                yield break;
            }

            SetWindowPos(_hwnd, _alwaysOnTop ? HwndTopmost : HwndNoTopmost, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpFrameChanged | SwpShowWindow);
            _appliedDisplayIndex = index;
            _moveDisplayRoutine = null;
        }

        void ApplyClickThrough(bool clickThrough, bool immediate)
        {
            if (!EnsureWindowHandle())
            {
                return;
            }

            if (_wantClickThrough != clickThrough)
            {
                _wantClickThrough = clickThrough;
                _wantClickThroughSince = Time.unscaledTime;
            }

            // Releasing capture (through=true) applies immediately so other apps stay usable.
            // Taking capture (through=false) waits briefly to avoid SetWindowLong chatter at UI edges.
            if (!immediate && !clickThrough &&
                Time.unscaledTime - _wantClickThroughSince < ClickThroughCaptureDebounce)
            {
                return;
            }

            if (_clickThrough == clickThrough)
            {
                return;
            }

            _clickThrough = clickThrough;
            ApplyExStyle(clickThrough);
        }

        void LogHeartbeat()
        {
            if (Time.unscaledTime < _nextHeartbeatAt)
            {
                return;
            }

            _nextHeartbeatAt = Time.unscaledTime + 1f;
            var foreground = EnsureWindowHandle() && GetForegroundWindow() == _hwnd;
            Debug.Log("[OVERLAY-HB] focused=" + _appFocused +
                      " clickThrough=" + _clickThrough +
                      " wantThrough=" + _wantClickThrough +
                      " foreground=" + foreground);
        }

        void ApplyTopmost()
        {
            if (!EnsureWindowHandle())
            {
                return;
            }

            ApplyExStyle(_clickThrough);
            SetWindowPos(_hwnd, _alwaysOnTop ? HwndTopmost : HwndNoTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }

        void ApplyExStyle(bool clickThrough)
        {
            if (!EnsureWindowHandle())
            {
                return;
            }

            var original = (ulong)GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
            var ex = original;
            ex |= WsExLayered | WsExToolWindow;
            if (_alwaysOnTop && !_suspendTopmost)
            {
                ex |= WsExTopmost;
            }
            else
            {
                ex &= ~WsExTopmost;
            }

            if (clickThrough)
            {
                ex |= WsExTransparent;
            }
            else
            {
                ex &= ~WsExTransparent;
            }

            if (ex != original)
            {
                SetWindowLongPtr(_hwnd, GwlExStyle, new IntPtr((long)ex));
            }
        }

        bool EnsureWindowHandle()
        {
            if (_hwnd != IntPtr.Zero && IsWindow(_hwnd))
            {
                return true;
            }

            _hwnd = FindWindow("UnityWndClass", Application.productName);
            return _hwnd != IntPtr.Zero && IsWindow(_hwnd);
        }
#endif
    }

    public sealed class GraphicRaycasterHost : MonoBehaviour
    {
        static readonly List<RaycastResult> Results = new List<RaycastResult>(8);
        TransparentOverlayWindow _window;

        public void BindWindow(TransparentOverlayWindow window) { _window = window; }

        public bool TryGetPointerPosition(out Vector2 position)
        {
            if (_window != null) return _window.TryGetPointerPosition(out position);
            position = Input.mousePosition;
            return true;
        }

        public bool IsPointerOverInteractive()
        {
            return TryGetPointerPosition(out var position) && IsPointerOverInteractive(position);
        }

        public bool IsPointerOverInteractive(Vector2 position)
        {
            if (position.x < 0f || position.y < 0f ||
                position.x >= Screen.width || position.y >= Screen.height) return false;
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var data = new PointerEventData(eventSystem)
            {
                position = position
            };
            Results.Clear();
            eventSystem.RaycastAll(data, Results);
            for (var i = 0; i < Results.Count; i++)
            {
                var graphic = Results[i].gameObject.GetComponent<UnityEngine.UI.Graphic>();
                if (graphic != null && graphic.raycastTarget)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
