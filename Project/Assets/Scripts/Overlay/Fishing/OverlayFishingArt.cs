using UnityEngine;

namespace CrazyChat.Overlay.Fishing
{
    /// <summary>Loads cartoon fishing sprites from Resources/Art/Generated/Fishing.</summary>
    public static class OverlayFishingArt
    {
        const string Root = "Art/Generated/Fishing/";

        public static Sprite Rod() =>
            Load("CartoonFishingRod") ?? Load("BambooFishingRod");

        public static Sprite Water() =>
            Load("WaterRipple") ?? Load("bubble_water") ?? OverlaySprites.Circle;

        public static Sprite QteBar() => Load("qte_bar") ?? OverlaySprites.RoundedRect;

        public static Sprite QteZone() => Load("qte_zone") ?? OverlaySprites.RoundedRect;

        public static Sprite QteKnob() => Load("qte_knob") ?? OverlaySprites.RoundedRect;

        public static Sprite FishOrFallback(string resource)
        {
            if (!string.IsNullOrEmpty(resource))
            {
                var s = Resources.Load<Sprite>(resource);
                if (s != null) return s;
            }

            return Load("fish_lo_blue") ?? OverlaySprites.Circle;
        }

        static Sprite Load(string name)
        {
            var s = Resources.Load<Sprite>(Root + name);
            if (s != null) return s;
#if UNITY_EDITOR
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Resources/" + Root + name + ".png");
            if (s != null) return s;
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/Generated/Fishing/" + name + ".png");
#endif
            return s;
        }
    }
}
