using System.Collections.Generic;
using UnityEngine;

namespace CrazyChat.Overlay.Fishing
{
    /// <summary>Loads fishing sprites from Resources/Art/Generated/Fishing (sinigangs + Zro water).</summary>
    public static class OverlayFishingArt
    {
        const string Root = "Art/Generated/Fishing/";
        const int WaterFrameCount = 24;
        static readonly Dictionary<string, Sprite> RuntimeSprites = new Dictionary<string, Sprite>();
        static Sprite[] _waterFrames;

        public enum RodTier
        {
            Classic = 0,
            Intermediate = 1,
            Expert = 2
        }

        /// <summary>Default gameplay rod (Intermediate). Multi-tier skins via <see cref="Rod(RodTier)"/>.</summary>
        public static Sprite Rod() => Rod(RodTier.Intermediate);

        public static Sprite Rod(RodTier tier)
        {
            switch (tier)
            {
                case RodTier.Classic:
                    return Load("rod_classic") ?? Load("SinigangsRod") ?? FallbackRod();
                case RodTier.Expert:
                    return Load("rod_expert") ?? Load("SinigangsRod") ?? FallbackRod();
                default:
                    return Load("rod_intermediate") ?? Load("SinigangsRod") ?? FallbackRod();
            }
        }

        /// <summary>Zro Water Tileset 3 cartoon solid+bubbles loop (blue #01). Empty if missing.</summary>
        public static Sprite[] WaterFrames()
        {
            if (_waterFrames != null) return _waterFrames;
            var list = new List<Sprite>(WaterFrameCount);
            for (var i = 0; i < WaterFrameCount; i++)
            {
                var s = Load("water_zro_" + i.ToString("00"));
                if (s != null) list.Add(s);
            }

            _waterFrames = list.Count > 0 ? list.ToArray() : System.Array.Empty<Sprite>();
            return _waterFrames;
        }

        public static Sprite Water()
        {
            var frames = WaterFrames();
            return frames.Length > 0 ? frames[0] : OverlaySprites.Circle;
        }

        public static Sprite QteBar() => OverlaySprites.RoundedRect;

        public static Sprite QteZone() => OverlaySprites.RoundedRect;

        public static Sprite QteKnob() => OverlaySprites.RoundedRect;

        public static Sprite FishOrFallback(string resource)
        {
            if (!string.IsNullOrEmpty(resource))
            {
                var direct = Resources.Load<Sprite>(resource);
                if (direct != null) return direct;
                var fromTex = SpriteFromTexture(resource);
                if (fromTex != null) return fromTex;
            }

            return Load("fish_tuna")
                   ?? Load("fish_seabass")
                   ?? Load("fish_anchovy")
                   ?? Load("fish_lo_blue")
                   ?? OverlaySprites.Circle;
        }

        static Sprite FallbackRod() =>
            Load("BambooFishingRod") ?? Load("CartoonFishingRod");

        static Sprite Load(string name)
        {
            var path = Root + name;
            var s = Resources.Load<Sprite>(path);
            if (s != null) return s;
            s = SpriteFromTexture(path);
            if (s != null) return s;
#if UNITY_EDITOR
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Resources/" + path + ".png");
            if (s != null) return s;
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/Generated/Fishing/" + name + ".png");
#endif
            return s;
        }

        static Sprite SpriteFromTexture(string resourcePath)
        {
            if (RuntimeSprites.TryGetValue(resourcePath, out var cached) && cached != null)
                return cached;

            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) return null;

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = resourcePath;
            RuntimeSprites[resourcePath] = sprite;
            return sprite;
        }
    }
}
