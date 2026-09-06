using System.IO;
using CrazyChat.Overlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OverlaySettingsMenuPrefabBuilder
{
    const string AssetPath = "Assets/Resources/Prefab/UI/SettingsMenu.prefab";
    const string RoundedRectPath = "Assets/Resources/Overlay/UI/rounded_rect.png";
    static readonly string[] StylePaths =
    {
        RoundedRectPath,
        "Assets/Resources/Overlay/UI/rounded_rect_style2.png",
        "Assets/Resources/Overlay/UI/rounded_rect_style3.png",
        "Assets/Resources/Overlay/UI/rounded_rect_style4.png"
    };

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
        var styles = EnsureStyleSprites();
        ReplaceGeneratedSprites(root, styles[0]);
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

    [MenuItem("CrazyChat/Add Settings Skin Style Row")]
    public static void AddSkinStyleRow()
    {
        var styles = EnsureStyleSprites();
        var root = PrefabUtility.LoadPrefabContents(AssetPath);
        try
        {
            var ui = root.GetComponent<OverlaySettingsUi>();
            var row = ui != null ? ui.EditorEnsureSkinStyleRow() : null;
            if (row == null)
            {
                Debug.LogError("[Overlay] 设置 Prefab 中找不到 SkinStyleRow 挂载位置。");
                return;
            }

            var inputIcons = row.parent.Find("InputIconsRow");
            if (inputIcons != null)
            {
                row.SetSiblingIndex(inputIcons.GetSiblingIndex() + 1);
            }

            var images = row.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i].sprite == null || !EditorUtility.IsPersistent(images[i].sprite))
                {
                    images[i].sprite = styles[0];
                }
            }

            ReplaceGeneratedFonts(row.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, AssetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Overlay] 已增量加入设置皮肤风格按钮。");
    }

    static Sprite[] EnsureStyleSprites()
    {
        var sprites = new Sprite[StylePaths.Length];
        for (var i = 0; i < StylePaths.Length; i++)
        {
            sprites[i] = ConfigureSprite(StylePaths[i]);
        }

        return sprites;
    }

    static Sprite ConfigureSprite(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            throw new FileNotFoundException("缺少设置皮肤图片", assetPath);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(10f, 10f, 10f, 10f);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    static void ReplaceGeneratedSprites(GameObject root, Sprite roundedRect)
    {
        var images = root.GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            if (images[i].sprite != null && !EditorUtility.IsPersistent(images[i].sprite))
            {
                images[i].sprite = roundedRect;
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
