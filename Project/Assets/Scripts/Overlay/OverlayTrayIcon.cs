using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Windows Standalone 通知区托盘图标：右键可取消隐藏或退出。
    /// </summary>
    public sealed class OverlayTrayIcon : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN
        const uint NimAdd = 0;
        const uint NimDelete = 2;
        const uint NifMessage = 1;
        const uint NifIcon = 2;
        const uint NifTip = 4;
        const uint TrayCallback = 0x8001;
        const uint WmRButtonUp = 0x0205;
        const uint WmContextMenu = 0x007B;
        const uint WmDestroy = 0x0002;
        const uint WmNull = 0;
        const uint MfString = 0;
        const uint TpmLeftAlign = 0;
        const uint TpmRightButton = 2;
        const uint TpmReturnCmd = 0x0100;
        const uint PmRemove = 1;
        const int IdiApplication = 32512;
        const int MenuIdQuit = 1;
        const int MenuIdReveal = 2;
        const int TrayId = 1;

        static readonly IntPtr HwndMessage = new IntPtr(-3);

        WndProc _wndProc;
        IntPtr _hwnd;
        IntPtr _hIcon;
        bool _iconOwned;
        bool _added;
        bool _classRegistered;
        bool _pendingAdd;
        string _className;
        Func<bool> _isHidden;
        Action _onReveal;

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
        static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr ExtractIcon(IntPtr instance, string path, int index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern ushort RegisterClassEx(ref WndClassEx windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool UnregisterClass(string className, IntPtr instance);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateWindowEx(
            uint exStyle, string className, string windowName, uint style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu,
            IntPtr instance, IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyWindow(IntPtr window);

        [DllImport("user32.dll")]
        static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool PeekMessage(out Msg message, IntPtr window, uint min, uint max, uint remove);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref Msg message);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref Msg message);

        [DllImport("user32.dll")]
        static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string text);

        [DllImport("user32.dll")]
        static extern bool DestroyMenu(IntPtr menu);

        [DllImport("user32.dll")]
        static extern uint TrackPopupMenu(
            IntPtr menu, uint flags, int x, int y, int reserved, IntPtr window, IntPtr rect);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr icon);

        [DllImport("user32.dll")]
        static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string moduleName);

        public void BindStealth(Func<bool> isHidden, Action onReveal)
        {
            _isHidden = isHidden;
            _onReveal = onReveal;
        }

        void OnEnable()
        {
            if (Application.isEditor)
            {
                enabled = false;
                return;
            }

            // 不在 AddComponent/OnEnable 调用栈里创建原生窗口。
            _pendingAdd = true;
        }

        void Update()
        {
            if (_pendingAdd)
            {
                _pendingAdd = false;
                TryAdd();
            }

            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            while (PeekMessage(out var message, _hwnd, 0, 0, PmRemove))
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }

        void OnDisable()
        {
            _pendingAdd = false;
            Remove();
        }

        void OnDestroy()
        {
            _pendingAdd = false;
            Remove();
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
                var windowClass = new WndClassEx
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(WndClassEx)),
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                    hInstance = module,
                    lpszClassName = _className
                };

                if (RegisterClassEx(ref windowClass) == 0)
                {
                    Debug.LogWarning("[CrazyChat] RegisterClassEx for tray failed.");
                    return;
                }

                _classRegistered = true;
                _hwnd = CreateWindowEx(
                    0, _className, "CrazyChatTray", 0,
                    0, 0, 0, 0, HwndMessage, IntPtr.Zero, module, IntPtr.Zero);
                if (_hwnd == IntPtr.Zero)
                {
                    Debug.LogWarning("[CrazyChat] CreateWindowEx for tray failed.");
                    CleanupNative();
                    return;
                }

                _hIcon = LoadAppIcon(module, out _iconOwned);
                var data = BuildNotifyData();
                if (!Shell_NotifyIcon(NimAdd, ref data))
                {
                    Debug.LogWarning("[CrazyChat] Shell_NotifyIcon NIM_ADD failed.");
                    CleanupNative();
                    return;
                }

                _added = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CrazyChat] Tray icon setup failed: " + ex.Message);
                CleanupNative();
            }
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
                var path = ResolveExePath();
                if (!string.IsNullOrEmpty(path))
                {
                    var icon = ExtractIcon(module, path, 0);
                    if (icon != IntPtr.Zero && icon != new IntPtr(1))
                    {
                        owned = true;
                        return icon;
                    }
                }
            }
            catch (Exception)
            {
            }

            return LoadIcon(IntPtr.Zero, new IntPtr(IdiApplication));
        }

        static string ResolveExePath()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args != null && args.Length > 0 && File.Exists(args[0]))
                {
                    return Path.GetFullPath(args[0]);
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        IntPtr WindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == TrayCallback)
            {
                var mouseMessage = (uint)lParam.ToInt64();
                if (mouseMessage == WmRButtonUp || mouseMessage == WmContextMenu)
                {
                    ShowMenu();
                }

                return IntPtr.Zero;
            }

            if (message == WmDestroy)
            {
                return IntPtr.Zero;
            }

            return DefWindowProc(window, message, wParam, lParam);
        }

        void ShowMenu()
        {
            var menu = CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                return;
            }

            try
            {
                if (_isHidden != null && _isHidden())
                {
                    AppendMenu(menu, MfString, new UIntPtr(MenuIdReveal), "取消隐藏");
                }

                AppendMenu(menu, MfString, new UIntPtr(MenuIdQuit), "退出");
                GetCursorPos(out var point);
                SetForegroundWindow(_hwnd);
                var command = TrackPopupMenu(
                    menu, TpmLeftAlign | TpmRightButton | TpmReturnCmd,
                    point.X, point.Y, 0, _hwnd, IntPtr.Zero);
                PostMessage(_hwnd, WmNull, IntPtr.Zero, IntPtr.Zero);
                if (command == MenuIdReveal)
                {
                    _onReveal?.Invoke();
                }
                else if (command == MenuIdQuit)
                {
                    Application.Quit();
                }
            }
            finally
            {
                DestroyMenu(menu);
            }
        }

        void Remove()
        {
            if (_added && _hwnd != IntPtr.Zero)
            {
                try
                {
                    var data = BuildNotifyData();
                    Shell_NotifyIcon(NimDelete, ref data);
                }
                catch (Exception)
                {
                }
            }

            _added = false;
            CleanupNative();
        }

        void CleanupNative()
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

            if (_iconOwned && _hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
            }

            _hIcon = IntPtr.Zero;
            _iconOwned = false;
            _wndProc = null;
        }
#else
        public void BindStealth(Func<bool> isHidden, Action onReveal)
        {
        }
#endif
    }
}
