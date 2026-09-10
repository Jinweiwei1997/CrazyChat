using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Windows Standalone 通知区托盘图标：悬停 CrazyChat，右键退出，左键无操作。
    /// </summary>
    public sealed class OverlayTrayIcon : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN
        const uint NimAdd = 0x00000000;
        const uint NimDelete = 0x00000002;
        const uint NifMessage = 0x00000001;
        const uint NifIcon = 0x00000002;
        const uint NifTip = 0x00000004;
        const uint WmApp = 0x8000;
        const uint TrayCallback = WmApp + 1;
        const uint WmRButtonUp = 0x0205;
        const uint WmContextMenu = 0x007B;
        const uint WmCommand = 0x0111;
        const uint WmDestroy = 0x0002;
        const uint WmNull = 0x0000;
        const uint MfString = 0x00000000;
        const uint TpmLeftAlign = 0x0000;
        const uint TpmRightButton = 0x0002;
        const uint TpmReturnCmd = 0x0100;
        const uint PmRemove = 0x0001;
        const int IdiApplication = 32512;
        const int MenuIdQuit = 1;
        const int TrayId = 1;

        static readonly IntPtr HwndMessage = new IntPtr(-3);

        WndProc _wndProc;
        IntPtr _wndProcPtr;
        IntPtr _hwnd;
        IntPtr _hIcon;
        bool _iconOwned;
        bool _added;
        bool _classRegistered;
        string _className;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NotifyIconData
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Msg
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public Point pt;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WndClassEx
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool PeekMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref Msg lpMsg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref Msg lpMsg);

        [DllImport("user32.dll")]
        static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        void OnEnable()
        {
            if (Application.isEditor)
            {
                enabled = false;
                return;
            }

            TryAdd();
        }

        void OnDisable()
        {
            Remove();
        }

        void OnDestroy()
        {
            Remove();
        }

        void Update()
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            while (PeekMessage(out var msg, _hwnd, 0, 0, PmRemove))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        void TryAdd()
        {
            if (_added)
            {
                return;
            }

            try
            {
                var module = GetModuleHandle(null);
                _className = "CrazyChat.OverlayTray." + GetInstanceID();
                _wndProc = WindowProc;
                _wndProcPtr = Marshal.GetFunctionPointerForDelegate(_wndProc);

                var wc = new WndClassEx
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(WndClassEx)),
                    lpfnWndProc = _wndProcPtr,
                    hInstance = module,
                    lpszClassName = _className
                };

                if (RegisterClassEx(ref wc) == 0)
                {
                    Debug.LogWarning("[CrazyChat] RegisterClassEx for tray failed.");
                    return;
                }

                _classRegistered = true;
                _hwnd = CreateWindowEx(
                    0,
                    _className,
                    "CrazyChatTray",
                    0,
                    0,
                    0,
                    0,
                    0,
                    HwndMessage,
                    IntPtr.Zero,
                    module,
                    IntPtr.Zero);

                if (_hwnd == IntPtr.Zero)
                {
                    Debug.LogWarning("[CrazyChat] CreateWindowEx for tray failed.");
                    CleanupNative(keepIcon: false);
                    return;
                }

                _hIcon = LoadAppIcon(module, out _iconOwned);
                var data = BuildNotifyData();
                if (!Shell_NotifyIcon(NimAdd, ref data))
                {
                    Debug.LogWarning("[CrazyChat] Shell_NotifyIcon NIM_ADD failed.");
                    CleanupNative(keepIcon: false);
                    return;
                }

                _added = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CrazyChat] Tray icon setup failed: " + ex.Message);
                CleanupNative(keepIcon: false);
            }
        }

        void Remove()
        {
            if (!_added && _hwnd == IntPtr.Zero && !_classRegistered)
            {
                return;
            }

            try
            {
                if (_added && _hwnd != IntPtr.Zero)
                {
                    var data = BuildNotifyData();
                    Shell_NotifyIcon(NimDelete, ref data);
                    _added = false;
                }
            }
            catch (Exception)
            {
            }

            CleanupNative(keepIcon: false);
        }

        NotifyIconData BuildNotifyData()
        {
            return new NotifyIconData
            {
                cbSize = Marshal.SizeOf(typeof(NotifyIconData)),
                hWnd = _hwnd,
                uID = TrayId,
                uFlags = NifMessage | NifIcon | NifTip,
                uCallbackMessage = TrayCallback,
                hIcon = _hIcon,
                szTip = "CrazyChat"
            };
        }

        static IntPtr LoadAppIcon(IntPtr module, out bool owned)
        {
            owned = false;
            try
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exe))
                {
                    var extracted = ExtractIcon(module, exe, 0);
                    if (extracted != IntPtr.Zero && extracted != new IntPtr(1))
                    {
                        owned = true;
                        return extracted;
                    }
                }
            }
            catch (Exception)
            {
            }

            return LoadIcon(IntPtr.Zero, new IntPtr(IdiApplication));
        }

        IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == TrayCallback)
            {
                var mouse = (uint)lParam.ToInt64();
                if (mouse == WmRButtonUp || mouse == WmContextMenu)
                {
                    ShowQuitMenu();
                }

                return IntPtr.Zero;
            }

            if (msg == WmCommand && wParam.ToInt32() == MenuIdQuit)
            {
                QuitGame();
                return IntPtr.Zero;
            }

            if (msg == WmDestroy)
            {
                return IntPtr.Zero;
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        void ShowQuitMenu()
        {
            var menu = CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                return;
            }

            try
            {
                AppendMenu(menu, MfString, new UIntPtr(MenuIdQuit), "退出");
                GetCursorPos(out var pt);
                SetForegroundWindow(_hwnd);
                var cmd = TrackPopupMenu(
                    menu,
                    TpmLeftAlign | TpmRightButton | TpmReturnCmd,
                    pt.X,
                    pt.Y,
                    0,
                    _hwnd,
                    IntPtr.Zero);
                PostMessage(_hwnd, WmNull, IntPtr.Zero, IntPtr.Zero);
                if (cmd == MenuIdQuit)
                {
                    QuitGame();
                }
            }
            finally
            {
                DestroyMenu(menu);
            }
        }

        static void QuitGame()
        {
            Application.Quit();
        }

        void CleanupNative(bool keepIcon)
        {
            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }

            if (_classRegistered && !string.IsNullOrEmpty(_className))
            {
                UnregisterClass(_className, GetModuleHandle(null));
                _classRegistered = false;
                _className = null;
            }

            if (!keepIcon && _hIcon != IntPtr.Zero)
            {
                // ExtractIcon returns a copy that must be destroyed; LoadIcon shared icons must not.
                if (_iconOwned)
                {
                    DestroyIcon(_hIcon);
                }

                _hIcon = IntPtr.Zero;
                _iconOwned = false;
            }

            _wndProc = null;
            _wndProcPtr = IntPtr.Zero;
            _added = false;
        }
#else
        void Awake()
        {
            enabled = false;
        }
#endif
    }
}
