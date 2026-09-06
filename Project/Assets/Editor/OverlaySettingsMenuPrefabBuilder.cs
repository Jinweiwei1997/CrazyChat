using System.IO;
using CrazyChat.Overlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OverlaySettingsMenuPrefabBuilder
{
    const string AssetPath = "Assets/Resources/Prefab/UI/SettingsMenu.prefab";
    const string SquareRectPath = "Assets/Resources/Overlay/UI/square_rect.png";
    const string JournalRectPath = "Assets/Resources/Overlay/UI/journal_rect.png";
    static readonly Color FlatBackground = new Color(0.094f, 0.094f, 0.094f, 1f);
    static readonly Color FlatControl = new Color(0.176f, 0.176f, 0.188f, 1f);
    static readonly Color FlatAccent = new Color(0f, 0.478f, 0.8f, 1f);
    static readonly Color FlatDanger = new Color(0.65f, 0.16f, 0.16f, 1f);
    static readonly Color FlatText = new Color(0.8f, 0.8f, 0.8f, 1f);
    static readonly Color FlatMuted = new Color(0.59f, 0.59f, 0.59f, 1f);

    [MenuItem("CrazyChat/Build Settings Menu Prefab")]
    public static void Build()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/Prefab/UI"));

        var root = new GameObject("SettingsMenu", typeof(RectTransform));
        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var ui = root.AddComponent<OverlaySettingsUi>();
        ui.EditorPopulate();
        ReplaceGeneratedSprites(root, ConfigureSprite(SquareRectPath, 1f));
        ConfigureSprite(JournalRectPath, 4f);
        ReplaceGeneratedFonts(root);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, AssetPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (prefab == null)
        {
            Debug.LogError("[Overlay] 写入设置菜单 Prefab 失败: " + AssetPath);
            return;
        }

        Debug.Log("[Overlay] 已写入 " + AssetPath);
    }

    [MenuItem("CrazyChat/Apply Settings Themes Layout")]
    public static void ApplySettingsThemesLayout()
    {
        var square = ConfigureSprite(SquareRectPath, 1f);
        ConfigureSprite(JournalRectPath, 4f);
        var root = PrefabUtility.LoadPrefabContents(AssetPath);
        try
        {
            var ui = root.GetComponent<OverlaySettingsUi>();
            var background = root.transform.Find("SettingsPanel/Background");
            if (background == null)
            {
                Debug.LogError("[Overlay] 设置 Prefab 中找不到 Background。");
                return;
            }

            var skinRow = background.Find("Pages/GamePage/SkinStyleRow");
            if (skinRow != null)
            {
                Object.DestroyImmediate(skinRow.gameObject);
            }

            var themeRow = ui != null ? ui.EditorEnsureThemeRow() : null;
            if (themeRow == null)
            {
                Debug.LogError("[Overlay] 设置 Prefab 中无法加入 ThemeRow。");
                return;
            }

            ((RectTransform)background).localScale = new Vector3(2f / 3f, 2f / 3f, 1f);
            var images = background.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                var image = images[i];
                image.sprite = square;
                image.type = Image.Type.Sliced;
                image.color = FlatControl;

                if (image.gameObject.name == "Background")
                {
                    image.color = FlatBackground;
                }
                else if (image.gameObject.name == "GameTab")
                {
                    image.color = FlatAccent;
                }
                else if (image.gameObject.name == "QuitGameRow")
                {
                    image.color = FlatDanger;
                }
            }

            var labels = background.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var name = labels[i].gameObject.name;
                labels[i].color = name == "Muted" || name == "Status" ? FlatMuted : FlatText;
            }

            var backgroundImage = background.GetComponent<Image>();
            var outline = background.GetComponent<Outline>();
            if (outline == null)
            {
                outline = background.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.235f, 0.235f, 0.235f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
            backgroundImage.raycastTarget = true;

            ReplaceGeneratedFonts(root);
            PrefabUtility.SaveAsPrefabAsset(root, AssetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Overlay] 已应用双主题设置布局。");
    }

    static Sprite ConfigureSprite(string assetPath, float border)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            throw new FileNotFoundException("缺少设置皮肤图片", assetPath);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    static void ReplaceGeneratedSprites(GameObject root, Sprite sprite)
    {
        var images = root.GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            if (images[i].sprite != null && !EditorUtility.IsPersistent(images[i].sprite))
            {
                images[i].sprite = sprite;
            }
        }
    }

    static void ReplaceGeneratedFonts(GameObject root)
    {
        var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var labels = root.GetComponentsInChildren<Text>(true);
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i].font == null || !EditorUtility.IsPersistent(labels[i].font))
            {
                labels[i].font = fallback;
            }
        }
    }
}
