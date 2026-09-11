using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CrazyChat.Overlay
{
    public sealed class OverlayUserSettings
    {
        const string FileName = "overlay_prefs.json";
        const float MinScale = 0.5f;
        const float MaxScale = 2.5f;
        const int MinSettingsTheme = 1;
        const int MaxPresetTheme = 2;
        public const int CustomTheme = 3;

        public float Scale { get; private set; } = 1f;
        public bool AlwaysOnTop { get; private set; } = true;
        public bool DisableDrag { get; private set; }
        public bool FlipHorizontal { get; private set; }
        public bool AutoStart { get; private set; }
        public bool ShowInputIcons { get; private set; }
        public bool TestMode { get; private set; }
        public int SettingsTheme { get; private set; } = MinSettingsTheme;
        public Color ThemeBackgroundColor { get; private set; } = OverlaySkin.PresetBackground(1);
        public Color ThemeAccentColor { get; private set; } = OverlaySkin.PresetAccent(1);
        public int TargetDisplayIndex { get; private set; }
        public int AvatarVersion { get; private set; }

        public void Load()
        {
            var json = ReadLocal();

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
                ShowInputIcons = data.showInputIcons;
                TestMode = data.testMode;
                SettingsTheme = Mathf.Clamp(data.settingsTheme, MinSettingsTheme, CustomTheme);
                var fallbackTheme = SettingsTheme == MaxPresetTheme ? MaxPresetTheme : MinSettingsTheme;
                ThemeBackgroundColor = ParseColor(data.themeBackgroundColor,
                    OverlaySkin.PresetBackground(fallbackTheme));
                ThemeAccentColor = ParseColor(data.themeAccentColor,
                    OverlaySkin.PresetAccent(fallbackTheme));
                if (SettingsTheme <= MaxPresetTheme)
                {
                    ApplyPresetColors(SettingsTheme);
                }
                TargetDisplayIndex = Mathf.Max(0, data.targetDisplayIndex);
                AvatarVersion = data.avatarVersion;
                if (!OverlayAvatarRules.IsEnabled(AvatarVersion, OverlayAvatarCodec.LocalPathA,
                        OverlayAvatarCodec.LocalPathB))
                {
                    AvatarVersion = 0;
                }
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
                showInputIcons = ShowInputIcons,
                testMode = TestMode,
                settingsTheme = SettingsTheme,
                themeBackgroundColor = "#" + ColorUtility.ToHtmlStringRGB(ThemeBackgroundColor),
                themeAccentColor = "#" + ColorUtility.ToHtmlStringRGB(ThemeAccentColor),
                targetDisplayIndex = TargetDisplayIndex,
                avatarVersion = AvatarVersion
            }, true);
            WriteLocal(json);
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

        public void SetShowInputIcons(bool value) => ShowInputIcons = value;

        public void SetTestMode(bool value) => TestMode = value;

        public void SetTargetDisplayIndex(int value) => TargetDisplayIndex = Mathf.Max(0, value);

        public void CycleSettingsTheme()
        {
            var next = SettingsTheme == MinSettingsTheme ? MaxPresetTheme : MinSettingsTheme;
            SettingsTheme = next;
            ApplyPresetColors(next);
        }

        public void SetCustomThemeColor(bool background, Color color)
        {
            color.a = 1f;
            if (background)
            {
                ThemeBackgroundColor = color;
            }
            else
            {
                ThemeAccentColor = color;
            }

            SettingsTheme = CustomTheme;
        }

        public bool AvatarEnabled =>
            OverlayAvatarRules.IsEnabled(AvatarVersion, OverlayAvatarCodec.LocalPathA, OverlayAvatarCodec.LocalPathB);

        public bool TrySetAvatarSlot(bool slotA, byte[] sourceImage)
        {
            var png = OverlayAvatarCodec.ProcessToPng(sourceImage);
            if (png == null)
            {
                return false;
            }

            var path = slotA ? OverlayAvatarCodec.LocalPathA : OverlayAvatarCodec.LocalPathB;
            if (!OverlayAvatarCodec.TryWrite(path, png))
            {
                return false;
            }

            if (OverlayAvatarRules.FilesReady(OverlayAvatarCodec.LocalPathA, OverlayAvatarCodec.LocalPathB))
            {
                AvatarVersion = Mathf.Max(1, AvatarVersion + 1);
            }
            else
            {
                AvatarVersion = 0;
            }

            return true;
        }

        public void ClearAvatarPresence()
        {
            OverlayAvatarCodec.DeleteQuiet(OverlayAvatarCodec.LocalPathA);
            OverlayAvatarCodec.DeleteQuiet(OverlayAvatarCodec.LocalPathB);
            AvatarVersion = 0;
        }

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

        void ApplyPresetColors(int theme)
        {
            ThemeBackgroundColor = OverlaySkin.PresetBackground(theme);
            ThemeAccentColor = OverlaySkin.PresetAccent(theme);
        }

        static Color ParseColor(string value, Color fallback)
        {
            return !string.IsNullOrEmpty(value) && ColorUtility.TryParseHtmlString(value, out var color)
                ? new Color(color.r, color.g, color.b, 1f)
                : fallback;
        }

        [Serializable]
        class PrefsFile
        {
            public float scale = 1f;
            public bool alwaysOnTop = true;
            public bool disableDrag;
            public bool flipHorizontal;
            public bool autoStart;
            public bool showInputIcons;
            public bool testMode;
            public int settingsTheme = MinSettingsTheme;
            public string themeBackgroundColor;
            public string themeAccentColor;
            public int targetDisplayIndex;
            public int avatarVersion;
        }
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
