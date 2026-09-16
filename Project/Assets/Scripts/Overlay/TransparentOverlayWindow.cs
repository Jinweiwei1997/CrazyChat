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
        const float TopmostRefreshSeconds = 2f;
        const float FocusMinIntervalSeconds = 0.75f;
        const float HeartbeatSeconds = 1f;

        GraphicRaycasterHost _raycasterHost;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        bool _clickThrough = true;
        bool _pointerCaptureLock;
        float _lastFocusAt = -999f;
#endif
        float _nextTopmostTime;
        float _nextHeartbeatAt;
        bool _applied;
        bool _alwaysOnTop = true;
        bool _suspendTopmost;
        int _targetDisplayIndex;
        int _appliedDisplayIndex = -1;
        Coroutine _moveDisplayRoutine;

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

        public void BindRaycaster(GraphicRaycasterHost host)
        {
            _raycasterHost = host;
        }

        public void SetAlwaysOnTop(bool enabled)
        {
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

        public void SetPointerCaptureLock(bool locked)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _pointerCaptureLock = locked;
            if (locked)
            {
                SetClickThrough(false);
            }
#endif
        }

        /// <param name="forceSteal">
        /// true：键盘开聊天等无鼠标手势时，短时 AttachThreadInput 抢前台；false：仅软抢（鼠标点开可用）。
        /// </param>
        public void FocusForTextInput(bool forceSteal = false)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!_applied || !EnsureWindowHandle())
            {
                OverlayDebugTrace.Log("FocusForTextInput abort");
                return;
            }

            SetClickThrough(false);

            var now = Time.unscaledTime;
            if (!forceSteal && now - _lastFocusAt < FocusMinIntervalSeconds)
            {
                return;
            }

            _lastFocusAt = now;
            if (GetForegroundWindow() == _hwnd)
            {
                SetFocus(_hwnd);
                OverlayDebugTrace.Log("FocusForTextInput already-foreground");
                return;
            }

            SetWindowPos(_hwnd, _alwaysOnTop ? HwndTopmost : HwndNoTopmost, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpShowWindow);

            if (forceSteal)
            {
                OverlayDebugTrace.Log("FocusForTextInput force");
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
                OverlayDebugTrace.Log("FocusForTextInput soft");
                SetForegroundWindow(_hwnd);
                SetFocus(_hwnd);
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

        void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!_applied)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now >= _nextHeartbeatAt)
            {
                _nextHeartbeatAt = now + HeartbeatSeconds;
                OverlayDebugTrace.LogVerbose(
                    "heartbeat clickThrough=" + _clickThrough +
                    " fgSelf=" + (GetForegroundWindow() == _hwnd));
            }

            var overUi = _raycasterHost != null && _raycasterHost.IsPointerOverInteractive();
            if (_pointerCaptureLock)
            {
                SetClickThrough(false);
            }
            else
            {
                SetClickThrough(!overUi);
            }

            OverlayDebugTrace.LogClickThrough(_clickThrough, overUi);

            if (_alwaysOnTop && !_suspendTopmost && now >= _nextTopmostTime)
            {
                ApplyTopmost();
                _nextTopmostTime = now + TopmostRefreshSeconds;
            }
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
            SetClickThrough(true);
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

        void SetClickThrough(bool clickThrough)
        {
            if (_clickThrough == clickThrough || !EnsureWindowHandle())
            {
                return;
            }

            _clickThrough = clickThrough;
            ApplyExStyle(clickThrough);
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

            var ex = (ulong)GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
            ex |= WsExLayered | WsExToolWindow;
            if (_alwaysOnTop)
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

            SetWindowLongPtr(_hwnd, GwlExStyle, new IntPtr((long)ex));
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

        public bool IsPointerOverInteractive()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var data = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition
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
