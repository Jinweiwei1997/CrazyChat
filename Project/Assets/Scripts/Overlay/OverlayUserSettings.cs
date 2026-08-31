#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.IO;
using System.Text;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CrazyChat.Overlay
{
    public sealed class OverlayUserSettings
    {
        const string FileName = "overlay_prefs.json";
        const float MinScale = 0.5f;
        const float MaxScale = 2.5f;

        public float Scale { get; private set; } = 1f;
        public bool AlwaysOnTop { get; private set; } = true;
        public bool DisableDrag { get; private set; }
        public bool FlipHorizontal { get; private set; }
        public bool AutoStart { get; private set; }
        public OverlayClickEffect ClickEffect { get; private set; } = OverlayClickEffect.Elastic;
        public bool ShowInputIcons { get; private set; }

        public void Load()
        {
            var json = ReadSteamCloud();
            if (string.IsNullOrEmpty(json))
            {
                json = ReadLocal();
            }

            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<PrefsFile>(json);
                if (data == null)
                {
                    return;
                }

                Scale = Mathf.Clamp(data.scale, MinScale, MaxScale);
                AlwaysOnTop = data.alwaysOnTop;
                DisableDrag = data.disableDrag;
                FlipHorizontal = data.flipHorizontal;
                AutoStart = data.autoStart;
                ClickEffect = data.clickEffect == (int)OverlayClickEffect.Flip
                    ? OverlayClickEffect.Flip
                    : OverlayClickEffect.Elastic;
                ShowInputIcons = data.showInputIcons;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 读取基础设置失败: " + e.Message);
            }
        }

        public void Save()
        {
            var json = JsonUtility.ToJson(new PrefsFile
            {
                scale = Scale,
                alwaysOnTop = AlwaysOnTop,
                disableDrag = DisableDrag,
                flipHorizontal = FlipHorizontal,
                autoStart = AutoStart,
                clickEffect = (int)ClickEffect,
                showInputIcons = ShowInputIcons
            }, true);
            WriteLocal(json);
            WriteSteamCloud(json);
        }

        public void AddScale(float delta)
        {
            Scale = Mathf.Clamp(Mathf.Round((Scale + delta) * 10f) / 10f, MinScale, MaxScale);
        }

        public void ResetScale()
        {
            Scale = 1f;
        }

        public void SetAlwaysOnTop(bool value) => AlwaysOnTop = value;

        public void SetDisableDrag(bool value) => DisableDrag = value;

        public void SetFlipHorizontal(bool value) => FlipHorizontal = value;

        public void CycleClickEffect()
        {
            ClickEffect = ClickEffect == OverlayClickEffect.Elastic
                ? OverlayClickEffect.Flip
                : OverlayClickEffect.Elastic;
        }

        public void SetShowInputIcons(bool value) => ShowInputIcons = value;

        public void SetAutoStart(bool value)
        {
            AutoStart = value;
            OverlayAutoStart.Apply(value);
        }

        static string ReadLocal()
        {
            try
            {
                var path = Path.Combine(Application.persistentDataPath, FileName);
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            }
            catch
            {
                return null;
            }
        }

        static void WriteLocal(string json)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(Path.Combine(Application.persistentDataPath, FileName), json, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 写入基础设置失败: " + e.Message);
            }
        }

        static string ReadSteamCloud()
        {
#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized || !SteamRemoteStorage.FileExists(FileName))
            {
                return null;
            }

            var size = SteamRemoteStorage.GetFileSize(FileName);
            if (size <= 0)
            {
                return null;
            }

            var buffer = new byte[size];
            var read = SteamRemoteStorage.FileRead(FileName, buffer, size);
            return read > 0 ? Encoding.UTF8.GetString(buffer, 0, read) : null;
#else
            return null;
#endif
        }

        static void WriteSteamCloud(string json)
        {
#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(json);
            SteamRemoteStorage.FileWrite(FileName, bytes, bytes.Length);
#endif
        }

        [Serializable]
        class PrefsFile
        {
            public float scale = 1f;
            public bool alwaysOnTop = true;
            public bool disableDrag;
            public bool flipHorizontal;
            public bool autoStart;
            public int clickEffect;
            public bool showInputIcons;
        }
    }

    public enum OverlayClickEffect
    {
        Elastic = 0,
        Flip = 1
    }

    public static class OverlayAutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "CrazyChat";

        public static void Apply(bool enable)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null)
                    {
                        return;
                    }

                    if (enable)
                    {
                        var exe = Path.Combine(Directory.GetParent(Application.dataPath).FullName, Application.productName + ".exe");
                        key.SetValue(ValueName, "\"" + exe + "\"");
                    }
                    else if (key.GetValue(ValueName) != null)
                    {
                        key.DeleteValue(ValueName);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 开机自启设置失败: " + e.Message);
            }
#endif
        }
    }
}
