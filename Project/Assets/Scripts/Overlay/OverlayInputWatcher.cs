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
        readonly bool[] _eligible = new bool[256];
        readonly float[] _heldSince = new float[256];
        readonly bool[] _reportedHeld = new bool[256];
        float _nextHeldTrace;
        OverlayConfig _config;
        float _lastCtrl;
        bool _anyDown;
        bool _pollInitialized;

        public bool IsAnyDown => _anyDown;
        void Awake()
        {
            _config = OverlayConfig.LoadOrDefault();
        }

        /// <summary>当前是否按住鼠标右键（Windows 下切焦点仍有效）。</summary>
        public bool IsRightButtonHeld
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return _pollInitialized && _down[0x02];
#else
                return Input.GetMouseButton(1);
#endif
            }
        }

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

            if (Input.GetMouseButtonDown(0)) InputDown?.Invoke(0x01);
            PollUnityCommands();
            SetAnyDown(Input.anyKey || Input.GetMouseButton(0) || Input.GetMouseButton(1) ||
                       Input.GetMouseButton(2));
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
            var any = false;
            for (var vk = 1; vk < 256; vk++)
            {
                if (!IsPhysicalInput(vk))
                {
                    _down[vk] = false;
                    continue;
                }

                var pressed = (GetAsyncKeyState(vk) & 0x8000) != 0;
                if (!_pollInitialized)
                {
                    _down[vk] = pressed;
                    _eligible[vk] = !pressed;
                    continue;
                }

                if (!pressed)
                {
                    _eligible[vk] = true;
                }

                if (_eligible[vk] && pressed && !_down[vk])
                {
                    taps++;
                    OnCommandDown(vk);
                    InputDown?.Invoke(vk);
                }

                _down[vk] = pressed;
                any |= _eligible[vk] && pressed;
            }

            TraceHeldInputs();
            _pollInitialized = true;
            SetAnyDown(any);
            return taps;
        }

        void TraceHeldInputs()
        {
            var now = Time.unscaledTime;
            var report = now >= _nextHeldTrace;
            System.Text.StringBuilder held = null;
            for (var vk = 1; vk < _down.Length; vk++)
            {
                if (!_down[vk] || !_eligible[vk])
                {
                    if (_reportedHeld[vk])
                    {
                        Debug.Log("input-held released vk=0x" + vk.ToString("X2") +
                            " seconds=" + (now - _heldSince[vk]).ToString("F1"));
                    }
                    _heldSince[vk] = 0f;
                    _reportedHeld[vk] = false;
                    continue;
                }

                if (_heldSince[vk] == 0f) _heldSince[vk] = now;
                if (!report || now - _heldSince[vk] < 10f) continue;
                if (held == null) held = new System.Text.StringBuilder();
                held.Append(" vk=0x").Append(vk.ToString("X2"))
                    .Append(" seconds=").Append((now - _heldSince[vk]).ToString("F1"));
                _reportedHeld[vk] = true;
            }

            if (held != null)
            {
                _nextHeldTrace = now + 30f;
                // Include sided modifier states to diagnose an aggregate Ctrl/Alt/Shift stuck bit.
                Debug.Log("input-held focused=" + Application.isFocused + held +
                    " modifiers(LShift,RShift,LCtrl,RCtrl,LAlt,RAlt)=" +
                    ((GetAsyncKeyState(0xA0) & 0x8000) != 0) + "," +
                    ((GetAsyncKeyState(0xA1) & 0x8000) != 0) + "," +
                    ((GetAsyncKeyState(0xA2) & 0x8000) != 0) + "," +
                    ((GetAsyncKeyState(0xA3) & 0x8000) != 0) + "," +
                    ((GetAsyncKeyState(0xA4) & 0x8000) != 0) + "," +
                    ((GetAsyncKeyState(0xA5) & 0x8000) != 0));
            }
        }
        // Only poll real keyboard and mouse buttons. Undefined / IME virtual keys can report as stuck.
        static bool IsPhysicalInput(int vk)
        {
            if (vk == 0x01 || vk == 0x02 || (vk >= 0x04 && vk <= 0x06))
            {
                return true;
            }

            if (vk == 0x08 || vk == 0x09 || vk == 0x0D || (vk >= 0x10 && vk <= 0x14) || vk == 0x1B)
            {
                return true;
            }

            if ((vk >= 0x20 && vk <= 0x2F) || (vk >= 0x30 && vk <= 0x39) ||
                (vk >= 0x41 && vk <= 0x5D))
            {
                return true;
            }

            if ((vk >= 0x60 && vk <= 0x87) || vk == 0x90 || vk == 0x91)
            {
                return true;
            }

            if ((vk >= 0xA6 && vk <= 0xB7) || (vk >= 0xBA && vk <= 0xC0) ||
                (vk >= 0xDB && vk <= 0xDF))
            {
                return true;
            }

            return vk == 0xE2;
        }

        void SetAnyDown(bool any)
        {
            if (_anyDown == any)
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
                var interval = _config != null ? Mathf.Max(0.1f, _config.doubleControlSeconds) : 0.4f;
                if (Time.unscaledTime - _lastCtrl <= interval)
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
                var interval = _config != null ? Mathf.Max(0.1f, _config.doubleControlSeconds) : 0.4f;
                if (Time.unscaledTime - _lastCtrl <= interval)
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
