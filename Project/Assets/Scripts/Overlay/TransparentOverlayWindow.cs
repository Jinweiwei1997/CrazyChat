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
        [SerializeField] float topmostRefreshSeconds = 2f;

        GraphicRaycasterHost _raycasterHost;
        bool _clickThrough = true;
        float _nextTopmostTime;
        bool _applied;
        bool _alwaysOnTop = true;

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
            if (_applied)
            {
                ApplyTopmost();
            }
#endif
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Screen.fullScreen = false;
            Screen.fullScreenMode = FullScreenMode.Windowed;

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

            var overUi = _raycasterHost != null && _raycasterHost.IsPointerOverInteractive();
            SetClickThrough(!overUi);

            if (_alwaysOnTop && Time.unscaledTime >= _nextTopmostTime)
            {
                ApplyTopmost();
                _nextTopmostTime = Time.unscaledTime + topmostRefreshSeconds;
            }
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        void ApplyChrome()
        {
            _hwnd = GetActiveWindow();
            if (_hwnd == IntPtr.Zero)
            {
                _hwnd = FindWindow("UnityWndClass", Application.productName);
            }

            if (_hwnd == IntPtr.Zero)
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

            var width = Display.main.systemWidth;
            var height = Display.main.systemHeight;
            if (width > 0 && height > 0 && (Screen.width != width || Screen.height != height))
            {
                Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
            }

            SetWindowPos(_hwnd, _alwaysOnTop ? HwndTopmost : HwndNoTopmost, 0, 0, width, height, SwpFrameChanged | SwpShowWindow);

            _clickThrough = false;
            SetClickThrough(true);
            _applied = true;
        }

        void SetClickThrough(bool clickThrough)
        {
            if (_hwnd == IntPtr.Zero || _clickThrough == clickThrough)
            {
                return;
            }

            _clickThrough = clickThrough;
            ApplyExStyle(clickThrough);
        }

        void ApplyTopmost()
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            ApplyExStyle(_clickThrough);
            SetWindowPos(_hwnd, _alwaysOnTop ? HwndTopmost : HwndNoTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }

        void ApplyExStyle(bool clickThrough)
        {
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
