using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public static class OverlaySkin
    {
        public const string Minimal = "minimal";
        public const string Basic = "basic";

        const string ResVertical = "Overlay/Skins/basic/panel_vertical";
        const string ResHorizontal = "Overlay/Skins/basic/panel_horizontal";
        const string ArtVertical = "Assets/Art/Package/Basic/Textures/panel_vertical.png";
        const string ArtHorizontal = "Assets/Art/Package/Basic/Textures/panel_horizontal.png";

        static readonly Color BasicText = new Color(0.18f, 0.20f, 0.24f, 1f);
        static readonly Color BasicMuted = new Color(0.40f, 0.42f, 0.48f, 1f);
        static readonly Color BasicAccent = new Color(0.76f, 0.86f, 1f, 1f);
        static readonly Color BasicDanger = new Color(1f, 0.78f, 0.78f, 1f);
        static readonly Color BasicWell = new Color(0.93f, 0.94f, 0.96f, 1f);
        static readonly Color BasicMine = new Color(0.72f, 0.84f, 1f, 1f);

        static string _id = Minimal;
        static Sprite _panelVertical;
        static Sprite _panelHorizontal;
        static bool _triedLoad;

        public static string Id => _id;

        public static bool IsBasic => _id == Basic;

        public static string Label => IsBasic ? "Basic" : "极简";

        public static Color Text => IsBasic ? BasicText : Color.white;

        public static Color TextMuted => IsBasic ? BasicMuted : OverlaySprites.TextMuted;

        public static void Set(string id)
        {
            _id = id == Basic ? Basic : Minimal;
        }

        public static void ApplyPanel(Image image)
        {
            if (image == null)
            {
                return;
            }

            if (IsBasic)
            {
                ClearFx(image);
                ApplySliced(image, PanelVertical, Color.white, 5f);
                return;
            }

            OverlaySprites.StylePanel(image);
        }

        public static void ApplyButton(Image image, bool accent = false, bool danger = false, bool well = false)
        {
            if (image == null)
            {
                return;
            }

            if (IsBasic)
            {
                ClearFx(image);
                var tint = Color.white;
                if (danger)
                {
                    tint = BasicDanger;
                }
                else if (accent)
                {
                    tint = BasicAccent;
                }
                else if (well)
                {
                    tint = BasicWell;
                }

                ApplySliced(image, PanelHorizontal, tint, 6f);
                return;
            }

            var color = OverlaySprites.Button;
            if (danger)
            {
                color = OverlaySprites.Danger;
            }
            else if (accent)
            {
                color = OverlaySprites.Accent;
            }
            else if (well)
            {
                color = OverlaySprites.Well;
            }

            OverlaySprites.StyleFill(image, color);
        }

        public static void ApplyBubble(Image image, bool mine)
        {
            if (image == null)
            {
                return;
            }

            if (IsBasic)
            {
                ClearFx(image);
                ApplySliced(image, PanelHorizontal, mine ? BasicMine : Color.white, 5.5f);
                return;
            }

            OverlaySprites.StyleFill(image, mine ? OverlaySprites.Accent : OverlaySprites.Button);
        }

        static Sprite PanelVertical
        {
            get
            {
                EnsureLoaded();
                return _panelVertical;
            }
        }

        static Sprite PanelHorizontal
        {
            get
            {
                EnsureLoaded();
                return _panelHorizontal;
            }
        }

        static void EnsureLoaded()
        {
            if (_triedLoad)
            {
                return;
            }

            _triedLoad = true;
            _panelVertical = LoadSprite(ResVertical, ArtVertical);
            _panelHorizontal = LoadSprite(ResHorizontal, ArtHorizontal);
        }

        static Sprite LoadSprite(string resourcesPath, string artPath)
        {
            var sprite = Resources.Load<Sprite>(resourcesPath);
            if (sprite != null)
            {
                return sprite;
            }

#if UNITY_EDITOR
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
#endif
            return sprite;
        }

        static void ApplySliced(Image image, Sprite sprite, Color color, float ppu)
        {
            if (sprite != null)
            {
                image.sprite = sprite;
                image.pixelsPerUnitMultiplier = ppu;
            }
            else
            {
                image.sprite = OverlaySprites.RoundedRect;
                image.pixelsPerUnitMultiplier = 1f;
            }

            image.type = Image.Type.Sliced;
            image.color = color;
        }

        static void ClearFx(Image image)
        {
            var shadow = image.GetComponent<Shadow>();
            if (shadow != null)
            {
                shadow.enabled = false;
            }

            var outline = image.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
        }
    }
}
