using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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
        const float DefaultThemeHue = 0.95f;
        const float DefaultThemeIntensity = 0.24f;

        public float Scale { get; private set; } = 1f;
        public float FireworkScale { get; private set; } = 1f;
        public bool AlwaysOnTop { get; private set; } = true;
        public bool DisableDrag { get; private set; }
        public bool FlipHorizontal { get; private set; }
        public bool AutoStart { get; private set; }
        public bool ShowInputIcons { get; private set; }
        public bool TestMode { get; private set; }
        public bool ShowCheckin { get; private set; } = true;
        public int SettingsTheme { get; private set; } = MinSettingsTheme;
        public float ThemeHue { get; private set; } = DefaultThemeHue;
        public float ThemeIntensity { get; private set; } = DefaultThemeIntensity;
        public bool ReduceTransparency { get; private set; } = true;
        public int TargetDisplayIndex { get; private set; }
        public int AvatarVersion { get; private set; }
        public List<TodoItem> Todos { get; private set; } = new List<TodoItem>();
        public event Action TodosChanged;
        public const string CheckinId = "checkin";
        public const string DefaultCheckinText = "嘀嘀嘀嘀，打开上班~";
        public bool HasTodos => Todos.Exists(item => item.IsCheckin || !string.IsNullOrWhiteSpace(item.text));
        public TodoItem Checkin => Todos.Find(item => item.IsCheckin);

        public void EnsureCheckin()
        {
            var item = Checkin;
            if (item == null) Todos.Insert(0, new TodoItem { id = CheckinId, frequency = TodoFrequency.Daily });
            else item.frequency = TodoFrequency.Daily;
        }

        public enum TodoFrequency { Once, Daily, Weekly }

        [Serializable]
        public sealed class TodoItem
        {
            public string id;
            public string text = "";
            public TodoFrequency frequency;
            public string completedPeriod = "";
            public bool IsCheckin => id == CheckinId;
            public string DisplayText => IsCheckin && string.IsNullOrWhiteSpace(text) ? DefaultCheckinText : text;

            public bool IsComplete(DateTime date) => completedPeriod == Period(date);

            public void ToggleCompletion(DateTime date)
            {
                if (IsCheckin && IsComplete(date)) return;
                completedPeriod = IsComplete(date) ? "" : Period(date);
            }

            internal string Period(DateTime date)
            {
                if (frequency == TodoFrequency.Once) return "once";
                var day = frequency == TodoFrequency.Daily ? date.AddHours(-6).Date : date.Date;
                if (frequency == TodoFrequency.Weekly)
                    day = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
                return day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        public TodoItem AddTodo()
        {
            var item = new TodoItem { id = Guid.NewGuid().ToString("N") };
            Todos.Add(item);
            SaveTodos();
            return item;
        }

        public void EditTodo(TodoItem item, string text, TodoFrequency frequency)
        {
            if (item == null || !Todos.Contains(item)) return;
            if (item.IsCheckin) frequency = TodoFrequency.Daily;
            if (item.frequency != frequency) item.completedPeriod = "";
            item.text = (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (item.text.Length > 100) item.text = item.text.Substring(0, 100);
            item.frequency = frequency;
            SaveTodos();
        }

        public void DeleteTodo(TodoItem item)
        {
            if (item != null && item.IsCheckin) return;
            if (Todos.Remove(item)) SaveTodos();
        }

        public void CompleteTodo(TodoItem item)
        {
            if (item == null || !Todos.Contains(item) || (!item.IsCheckin && string.IsNullOrWhiteSpace(item.text)) ||
                item.IsComplete(DateTime.Now)) return;
            item.completedPeriod = item.Period(DateTime.Now);
            SaveTodos();
        }

        public void ToggleTodo(TodoItem item)
        {
            if (item != null && item.IsCheckin) { CompleteTodo(item); return; }
            if (item == null || !Todos.Contains(item) || string.IsNullOrWhiteSpace(item.text)) return;
            item.ToggleCompletion(DateTime.Now);
            SaveTodos();
        }

        void SaveTodos()
        {
            Save();
            TodosChanged?.Invoke();
        }

        public void Load()
        {
            EnsureCheckin();
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
                FireworkScale = !HasPrefsKey(json, "fireworkScale")
                    ? 1f
                    : Mathf.Clamp(data.fireworkScale, MinScale, MaxScale);
                AlwaysOnTop = data.alwaysOnTop;
                DisableDrag = data.disableDrag;
                FlipHorizontal = data.flipHorizontal;
                AutoStart = data.autoStart;
                ShowInputIcons = data.showInputIcons;
                TestMode = data.testMode;
                // Missing field in old prefs → default true (JsonUtility bool defaults false).
                ShowCheckin = !HasPrefsKey(json, "showCheckin") || data.showCheckin;
                SettingsTheme = data.settingsTheme == MaxPresetTheme
                    ? MaxPresetTheme
                    : MinSettingsTheme;
                ThemeHue = Mathf.Clamp01(data.themeHue);
                ThemeIntensity = Mathf.Clamp01(data.themeIntensity);
                ReduceTransparency = data.reduceTransparency;
                TargetDisplayIndex = Mathf.Max(0, data.targetDisplayIndex);
                AvatarVersion = data.avatarVersion;
                Todos = data.todos ?? new List<TodoItem>();
                Todos.RemoveAll(item => item == null);
                EnsureCheckin();
                foreach (var item in Todos)
                {
                    if (string.IsNullOrEmpty(item.id)) item.id = Guid.NewGuid().ToString("N");
                    if (!Enum.IsDefined(typeof(TodoFrequency), item.frequency))
                        item.frequency = TodoFrequency.Once;
                }
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
                fireworkScale = FireworkScale,
                alwaysOnTop = AlwaysOnTop,
                disableDrag = DisableDrag,
                flipHorizontal = FlipHorizontal,
                autoStart = AutoStart,
                showInputIcons = ShowInputIcons,
                testMode = TestMode,
                showCheckin = ShowCheckin,
                settingsTheme = SettingsTheme,
                themeHue = ThemeHue,
                themeIntensity = ThemeIntensity,
                reduceTransparency = ReduceTransparency,
                targetDisplayIndex = TargetDisplayIndex,
                avatarVersion = AvatarVersion,
                todos = Todos
            }, true);
            WriteLocal(json);
        }

        public void AddScale(float delta)
        {
            Scale = Mathf.Clamp(Mathf.Round((Scale + delta) * 10f) / 10f, MinScale, MaxScale);
        }

        public void AddFireworkScale(float delta)
        {
            FireworkScale = Mathf.Clamp(Mathf.Round((FireworkScale + delta) * 10f) / 10f, MinScale, MaxScale);
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
        public void SetShowCheckin(bool value) => ShowCheckin = value;

        public void SetTargetDisplayIndex(int value) => TargetDisplayIndex = Mathf.Max(0, value);

        public void CycleSettingsTheme()
        {
            var next = SettingsTheme == MinSettingsTheme ? MaxPresetTheme : MinSettingsTheme;
            SetSettingsTheme(next);
        }

        public void SetSettingsTheme(int theme)
        {
            SettingsTheme = theme == MaxPresetTheme ? MaxPresetTheme : MinSettingsTheme;
        }

        public void SetThemeHue(float hue)
        {
            ThemeHue = Mathf.Clamp01(hue);
        }

        public void SetThemeIntensity(float intensity) => ThemeIntensity = Mathf.Clamp01(intensity);

        public void SetReduceTransparency(bool value) => ReduceTransparency = value;

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

        static bool HasPrefsKey(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return false;
            return json.IndexOf("\"" + key + "\"", System.StringComparison.Ordinal) >= 0;
        }

        [Serializable]
        class PrefsFile
        {
            public float scale = 1f;
            public float fireworkScale = 1f;
            public bool alwaysOnTop = true;
            public bool disableDrag;
            public bool flipHorizontal;
            public bool autoStart;
            public bool showInputIcons;
            public bool testMode;
            public bool showCheckin = true;
            public int settingsTheme = MinSettingsTheme;
            public float themeHue = DefaultThemeHue;
            public float themeIntensity = DefaultThemeIntensity;
            public bool reduceTransparency = true;
            public int targetDisplayIndex;
            public int avatarVersion;
            public List<TodoItem> todos = new List<TodoItem>();
        }
    }

    public static class OverlayAutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "CrazyChat";

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        static readonly IntPtr CurrentUser = new IntPtr(unchecked((int)0x80000001));

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegSetKeyValueW(IntPtr key, string subKey, string valueName,
            uint type, byte[] data, uint dataSize);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegDeleteKeyValueW(IntPtr key, string subKey, string valueName);
#endif

        public static void Apply(bool enable)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                int result;
                if (enable)
                {
                    var exe = Path.Combine(Directory.GetParent(Application.dataPath).FullName, Application.productName + ".exe");
                    // REG_SZ requires UTF-16 data including the terminating null character.
                    var data = Encoding.Unicode.GetBytes("\"" + exe + "\"\0");
                    result = RegSetKeyValueW(CurrentUser, RunKey, ValueName, 1, data, (uint)data.Length);
                }
                else
                {
                    result = RegDeleteKeyValueW(CurrentUser, RunKey, ValueName);
                    if (result == 2) return; // Already absent (ERROR_FILE_NOT_FOUND).
                }
                if (result != 0)
                    Debug.LogWarning("[Overlay] 开机自启设置失败，Windows 错误码: " + result);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 开机自启设置失败: " + e.Message);
            }
#endif
        }
    }
}
