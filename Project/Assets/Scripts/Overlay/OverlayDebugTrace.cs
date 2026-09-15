using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Diagnostic quit/UI-close trace. Writes to persistentDataPath/crazychat-debug.log with flush.
    /// Search tags: [DEBUG-quit]
    /// </summary>
    public static class OverlayDebugTrace
    {
        const string Tag = "[DEBUG-quit]";
        static readonly object Gate = new object();
        static string _path;
        static bool _hooked;
        static bool _lastClickThrough = true;
        static bool _hasLastClickThrough;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            EnsureHooked();
            Log("process-start product=" + Application.productName +
                " version=" + Application.version +
                " platform=" + Application.platform +
                " dataPath=" + Application.dataPath);
        }

        public static void Log(string message)
        {
            EnsureHooked();
            var line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + Tag + " " + message;
            try
            {
                Debug.Log(line);
            }
            catch (Exception)
            {
            }

            try
            {
                lock (Gate)
                {
                    File.AppendAllText(Path, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
            }
        }

        public static void LogClickThrough(bool clickThrough, bool overUi)
        {
            if (_hasLastClickThrough && _lastClickThrough == clickThrough)
            {
                return;
            }

            _hasLastClickThrough = true;
            _lastClickThrough = clickThrough;
            Log("clickThrough=" + clickThrough + " overUi=" + overUi);
        }

        static string Path
        {
            get
            {
                if (!string.IsNullOrEmpty(_path))
                {
                    return _path;
                }

                try
                {
                    _path = System.IO.Path.Combine(Application.persistentDataPath, "crazychat-debug.log");
                }
                catch (Exception)
                {
                    _path = "crazychat-debug.log";
                }

                return _path;
            }
        }

        static void EnsureHooked()
        {
            if (_hooked)
            {
                return;
            }

            _hooked = true;
            Application.logMessageReceivedThreaded += OnUnityLog;
            Application.quitting += () => Log("Application.quitting");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                Log("playMode=" + state);
            };
#endif
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Log("UnhandledException isTerminating=" + args.IsTerminating + " " + args.ExceptionObject);
            };
        }

        static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
            {
                return;
            }

            try
            {
                lock (Gate)
                {
                    var sb = new StringBuilder(256);
                    sb.Append(DateTime.Now.ToString("HH:mm:ss.fff"));
                    sb.Append(' ').Append(Tag).Append(" unity-").Append(type).Append(' ').Append(condition);
                    sb.AppendLine();
                    if (!string.IsNullOrEmpty(stackTrace))
                    {
                        sb.AppendLine(stackTrace);
                    }

                    File.AppendAllText(Path, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
