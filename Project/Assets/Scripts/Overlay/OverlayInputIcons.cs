using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CrazyChat.Overlay
{
    public static class OverlayInputIcons
    {
        const string EditorKeyboard = "Assets/Art/Package/GameInputControllerIconsFree/keyboard/keyboard-outlined";
        const string EditorMouse = "Assets/Art/Package/GameInputControllerIconsFree/mouse/mouse-outlined";

        static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            { 0x01, "mouse-left" },
            { 0x02, "mouse-right" },
            { 0x04, "mouse-middle" },
            { 0x05, "mouse-g1" },
            { 0x06, "mouse-g2" },
            { 0x08, "backspace" },
            { 0x09, "tab" },
            { 0x0D, "enter" },
            { 0x10, "shift" },
            { 0x11, "ctrl" },
            { 0x12, "alt" },
            { 0x13, "pause" },
            { 0x14, "caps" },
            { 0x1B, "esc" },
            { 0x20, "space" },
            { 0x21, "pgup" },
            { 0x22, "pgdn" },
            { 0x23, "end" },
            { 0x24, "home" },
            { 0x25, "arrow-left" },
            { 0x26, "arrow-up" },
            { 0x27, "arrow-right" },
            { 0x28, "arrow-down" },
            { 0x2C, "prtsc" },
            { 0x2D, "ins" },
            { 0x2E, "del" },
            { 0x5B, "windows" },
            { 0x5C, "windows" },
            { 0x5D, "context" },
            { 0x90, "numlk" },
            { 0x91, "scrlk" },
            { 0xA0, "shift" },
            { 0xA1, "shift" },
            { 0xA2, "ctrl" },
            { 0xA3, "ctrl" },
            { 0xA4, "alt" },
            { 0xA5, "alt" },
            { 0xBA, "semi-colon" },
            { 0xBB, "equals" },
            { 0xBC, "comma" },
            { 0xBD, "hyphen" },
            { 0xBE, "dot" },
            { 0xBF, "forward-slash" },
            { 0xC0, "tilde" },
            { 0xDB, "bracket-open" },
            { 0xDC, "backward-slash" },
            { 0xDD, "bracket-close" },
            { 0xDE, "quote" }
        };

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(int vk)
        {
            var name = NameFor(vk);
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            Sprite sprite;
            if (Cache.TryGetValue(name, out sprite))
            {
                return sprite;
            }

            sprite = Load(name);
            Cache[name] = sprite;
            return sprite;
        }

        static string NameFor(int vk)
        {
            string name;
            if (Names.TryGetValue(vk, out name))
            {
                return name;
            }

            if (vk >= 0x30 && vk <= 0x39)
            {
                return ((char)vk).ToString();
            }

            if (vk >= 0x41 && vk <= 0x5A)
            {
                return ((char)(vk + 32)).ToString();
            }

            if (vk >= 0x70 && vk <= 0x7B)
            {
                return "f" + (vk - 0x6F);
            }

            return null;
        }

        static Sprite Load(string name)
        {
#if UNITY_EDITOR
            var editor = LoadEditor(name);
            if (editor != null)
            {
                return editor;
            }
#endif
            return LoadFile(name);
        }

#if UNITY_EDITOR
        static Sprite LoadEditor(string name)
        {
            var keyboard = AssetDatabase.LoadAssetAtPath<Sprite>(EditorKeyboard + "/" + name + ".png");
            if (keyboard != null)
            {
                return keyboard;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(EditorMouse + "/" + name + ".png");
        }
#endif

        static Sprite LoadFile(string name)
        {
            var paths = new[]
            {
                Path.Combine(Application.dataPath, "Art/Package/GameInputControllerIconsFree/keyboard/keyboard-outlined", name + ".png"),
                Path.Combine(Application.dataPath, "Art/Package/GameInputControllerIconsFree/mouse/mouse-outlined", name + ".png"),
                Path.Combine(Application.streamingAssetsPath, "InputIcons", name + ".png"),
                Path.Combine(Directory.GetParent(Application.dataPath).FullName, "InputIcons", name + ".png")
            };

            for (var i = 0; i < paths.Length; i++)
            {
                if (!File.Exists(paths[i]))
                {
                    continue;
                }

                var bytes = File.ReadAllBytes(paths[i]);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = name
                };
                if (!tex.LoadImage(bytes))
                {
                    Object.Destroy(tex);
                    continue;
                }

                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }

            return null;
        }
    }
}
