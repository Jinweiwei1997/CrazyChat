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
        const string ResIndividual = "Overlay/Skins/individual/";
        const string ArtIndividual = "Assets/Art/Package/Individual/";

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
        static Sprite _indPanel;
        static Sprite _indButton;
        static Sprite _indButtonOn;
        static Sprite _indButtonDanger;
        static Sprite _indClose;
        static Sprite _indGear;
        static Sprite _indTab;
        static Sprite _indToggle;

        public static string Id => _id;

        public static bool IsBasic => _id == Basic;

        public static string Label => IsBasic ? "Basic" : "极简";

        public static Color Text => IsBasic ? BasicText : Color.white;

        public static Color TextMuted => IsBasic ? BasicMuted : OverlaySprites.TextMuted;

        public static Color SettingsText => Color.white;

        public static Color SettingsMuted => new Color(1f, 1f, 1f, 0.72f);

        public static void ApplySettingsPanel(Image image)
        {
            // if (image == null)
            // {
            //     return;
            // }
            //
            // ClearFx(image);
            // ApplySliced(image, IndividualPanel, Color.white, 3.5f);
        }

        public static void ApplySettingsButton(Image image, bool on = false, bool danger = false)
        {
            // if (image == null)
            // {
            //     return;
            // }
            //
            // ClearFx(image);
            // if (danger)
            // {
            //     ApplySliced(image, IndividualDanger, Color.white, 5f);
            //     return;
            // }
            //
            // ApplySliced(image, on ? IndividualButtonOn : IndividualButton, Color.white, 4.5f);
        }

        public static void ApplySettingsTab(Image image, bool on)
        {
            // if (image == null)
            // {
            //     return;
            // }
            //
            // ClearFx(image);
            // ApplySliced(image, on ? IndividualButtonOn : IndividualTab, Color.white, 4.5f);
        }

        public static void ApplySettingsToggle(Image image, bool on)
        {
            // if (image == null)
            // {
            //     return;
            // }
            //
            // ClearFx(image);
            // ApplySliced(image, on ? IndividualButtonOn : IndividualToggle, Color.white, 4.5f);
        }

        public static void ApplySettingsClose(Image image)
        {
            // ApplySimple(image, IndividualClose);
        }

        public static void ApplySettingsGear(Image image)
        {
            // ApplySimple(image, IndividualGear);
        }

        static Sprite IndividualPanel => Ind(ref _indPanel, "panel", "UI_Mobile_InterfaceButtons_rect70.png");
        static Sprite IndividualButton => Ind(ref _indButton, "button", "UI_Mobile_InterfaceButtons_g210.png");
        static Sprite IndividualButtonOn => Ind(ref _indButtonOn, "button_on", "UI_Mobile_InterfaceButtons_g203.png");
        static Sprite IndividualDanger => Ind(ref _indButtonDanger, "button_danger", "UI_Mobile_InterfaceButtons_g193.png");
        static Sprite IndividualClose => Ind(ref _indClose, "close", "UI_Mobile_InterfaceButtons_g206.png");
        static Sprite IndividualGear => Ind(ref _indGear, "gear", "UI_Mobile_InterfaceButtons_g190.png");
        static Sprite IndividualTab => Ind(ref _indTab, "tab", "UI_Mobile_InterfaceButtons_rect95.png");
        static Sprite IndividualToggle => Ind(ref _indToggle, "toggle", "UI_Mobile_InterfaceButtons_g196.png");

        static Sprite Ind(ref Sprite cache, string resourceName, string artFile)
        {
            if (cache == null)
            {
                cache = LoadSprite(ResIndividual + resourceName, ArtIndividual + artFile);
            }

            return cache;
        }

        static void ApplySimple(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            ClearFx(image);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
            }
            else
            {
                image.sprite = OverlaySprites.RoundedRect;
                image.preserveAspect = false;
            }

            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.pixelsPerUnitMultiplier = 1f;
        }

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
            image.preserveAspect = false;
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
