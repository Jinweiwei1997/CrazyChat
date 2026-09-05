using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// 监听键盘和鼠标按下。Windows 下用系统按键状态，切到别的窗口也能算。
    /// </summary>
    public sealed class OverlayInputWatcher : MonoBehaviour
    {
        public event Action Tapped;
        public event Action<int> InputDown;
        public event Action<bool> PresenceChanged;
        public event Action DoubleControl;
        public event Action NavigateLeft;
        public event Action NavigateRight;
        public event Action Confirm;
        public event Action Cancel;

        readonly bool[] _down = new bool[256];
        float _lastCtrl;
        bool _anyDown;

        public bool IsAnyDown => _anyDown;

        void Update()
        {
            var taps = 0;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            taps = PollWindows();
#else
            if (Input.anyKeyDown)
            {
                taps++;
            }

            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                taps++;
            }

            PollUnityCommands();
            var held = Input.anyKey || Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
            SetAnyDown(held);
#endif
            for (var i = 0; i < taps; i++)
            {
                Tapped?.Invoke();
            }
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        int PollWindows()
        {
            var taps = 0;
            for (var vk = 1; vk < 256; vk++)
            {
                if (vk == 3 || vk == 7)
                {
                    continue;
                }

                var pressed = (GetAsyncKeyState(vk) & 0x8000) != 0;
                if (pressed && !_down[vk])
                {
                    taps++;
                    OnCommandDown(vk);
                    InputDown?.Invoke(vk);
                }

                _down[vk] = pressed;
            }

            var any = false;
            for (var vk = 1; vk < 256; vk++)
            {
                if (_down[vk])
                {
                    any = true;
                    break;
                }
            }

            SetAnyDown(any);
            return taps;
        }

        void SetAnyDown(bool any)
        {
            if (any == _anyDown)
            {
                return;
            }

            _anyDown = any;
            PresenceChanged?.Invoke(any);
        }

        void OnCommandDown(int vk)
        {
            if (vk == 0x11)
            {
                if (Time.unscaledTime - _lastCtrl <= 0.4f)
                {
                    DoubleControl?.Invoke();
                }

                _lastCtrl = Time.unscaledTime;
                return;
            }

            if (vk == 0x25)
            {
                NavigateLeft?.Invoke();
            }
            else if (vk == 0x27)
            {
                NavigateRight?.Invoke();
            }
            else if (vk == 0x0D)
            {
                Confirm?.Invoke();
            }
            else if (vk == 0x1B)
            {
                Cancel?.Invoke();
            }
        }
#else
        void PollUnityCommands()
        {
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
            {
                if (Time.unscaledTime - _lastCtrl <= 0.4f)
                {
                    DoubleControl?.Invoke();
                }

                _lastCtrl = Time.unscaledTime;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                NavigateLeft?.Invoke();
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                NavigateRight?.Invoke();
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Confirm?.Invoke();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel?.Invoke();
            }
        }
#endif
    }
}
