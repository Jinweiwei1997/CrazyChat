using System.IO;
using CrazyChat.Overlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OverlayUiPrefabBuilder
{
    const string ChatPath = "Assets/Resources/Prefab/UI/ChatPanel.prefab";
    const string SquareRectPath = "Assets/Resources/Overlay/UI/square_rect.png";
    const string ControlRectPath = "Assets/Resources/Overlay/UI/control_rect.png";
    const string SendIconPath = "Assets/Resources/Overlay/UI/codicon_send.png";
    const string HistoryIconPath = "Assets/Resources/Overlay/UI/codicon_history.png";
    const string CloseIconPath = "Assets/Resources/Overlay/UI/codicon_close.png";

    [MenuItem("CrazyChat/Build Chat Panel Prefab")]
    public static void BuildChat()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/Prefab/UI"));
        var root = new GameObject("ChatPanel", typeof(RectTransform));
        Stretch((RectTransform)root.transform);
        var ui = root.AddComponent<OverlayChatUi>();
        ui.EditorPopulate();
        ApplyChatAssets(
            root,
            ConfigureSprite(SquareRectPath, 1f),
            ConfigureSprite(ControlRectPath, 1f),
            ConfigureIcon(SendIconPath),
            ConfigureIcon(HistoryIconPath),
            ConfigureIcon(CloseIconPath));
        ReplaceGeneratedFonts(root);
        Save(root, ChatPath);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Save(GameObject root, string assetPath)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (prefab == null)
        {
            Debug.LogError("[Overlay] 写入 Prefab 失败: " + assetPath);
            return;
        }

        Debug.Log("[Overlay] 已写入 " + assetPath);
    }

    static void ApplyChatAssets(
        GameObject root,
        Sprite square,
        Sprite control,
        Sprite sendIcon,
        Sprite historyIcon,
        Sprite closeIcon)
    {
        var images = root.GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            var image = images[i];
            var name = image.gameObject.name;
            if (name == "Backdrop")
            {
                image.sprite = null;
                image.color = Color.clear;
                continue;
            }

            if (name == "Icon")
            {
                var parentName = image.transform.parent.name;
                image.sprite = parentName == "Send"
                    ? sendIcon
                    : parentName == "History"
                        ? historyIcon
                        : closeIcon;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = OverlaySkin.SettingsThemeText(1);
                continue;
            }

            if (name == "Divider" || name == "ComposerDivider")
            {
                image.sprite = square;
                image.type = Image.Type.Sliced;
                image.color = OverlaySkin.ThemeDivider(1);
                continue;
            }

            image.sprite = name == "ChatCard" || name == "Header" || name == "Body"
                ? square
                : control;
            image.type = Image.Type.Sliced;
            image.color = name == "ChatCard"
                ? OverlaySkin.ThemeBackground(1)
                : name == "Header"
                    ? OverlaySkin.ThemeHeader(1)
                    : name == "Body"
                        ? OverlaySkin.ThemeSection(1)
                        : name == "Input"
                            ? OverlaySkin.ThemeInputBackground(1)
                            : name == "Send" || name == "History" || name == "Close"
                                ? Color.clear
                                : OverlaySkin.ThemeControl(1);
        }

        var card = root.transform.Find("ChatCard");
        if (card != null)
        {
            var effects = card.GetComponents<Shadow>();
            for (var i = 0; i < effects.Length; i++)
            {
                Object.DestroyImmediate(effects[i]);
            }
        }

        var buttons = root.GetComponentsInChildren<Button>(true);
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].transition = Selectable.Transition.None;
        }

        var input = root.GetComponentInChildren<InputField>(true);
        if (input != null)
        {
            input.transition = Selectable.Transition.None;
        }
    }

    static Sprite ConfigureSprite(string path, float border)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            throw new FileNotFoundException("缺少聊天界面图片", path);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite ConfigureIcon(string path)
    {
        var sprite = ConfigureSprite(path, 0f);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? sprite;
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
