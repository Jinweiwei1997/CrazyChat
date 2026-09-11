using System.IO;
using CrazyChat.Overlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OverlaySettingsMenuPrefabBuilder
{
    const string AssetPath = "Assets/Resources/Prefab/UI/SettingsMenu.prefab";
    const string SquareRectPath = "Assets/Resources/Overlay/UI/square_rect.png";
    const string ControlRectPath = "Assets/Resources/Overlay/UI/control_rect.png";
    const string SettingsIconPath = "Assets/Resources/Overlay/UI/codicon_settings.png";
    const string CloseIconPath = "Assets/Resources/Overlay/UI/codicon_close.png";

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
        ConfigureSprite(ControlRectPath, 1f);
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

        ApplySettingsThemesLayout();
        Debug.Log("[Overlay] 已写入 " + AssetPath);
    }

    [MenuItem("CrazyChat/Apply Settings Themes Layout")]
    public static void ApplySettingsThemesLayout()
    {
        var square = ConfigureSprite(SquareRectPath, 1f);
        var control = ConfigureSprite(ControlRectPath, 1f);
        var settingsIcon = ConfigureIconSprite(SettingsIconPath);
        var closeIcon = ConfigureIconSprite(CloseIconPath);
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
                Debug.LogError("[Overlay] 设置 Prefab 中找不到 ThemeRow。");
                return;
            }
            ui.EditorEnsureColorRows();
            if (ui.EditorEnsureBackdrop() == null)
            {
                Debug.LogError("[Overlay] 设置 Prefab 中无法加入 Backdrop。");
                return;
            }

            ((RectTransform)background).localScale = Vector3.one;
            var header = background.Find("Header");
            var tabBar = background.Find("TabBar");
            EnsureSectionImage(header, square, OverlaySkin.ThemeHeader(1));
            EnsureSectionImage(tabBar, square, OverlaySkin.ThemeSection(1));
            ApplyVsCodeLayout(background, header, tabBar);
            ApplyFunctionalIcons(root.transform, header, tabBar, control, settingsIcon, closeIcon);
            EnsureDivider(header, square);
            EnsureDivider(tabBar, square);

            var images = background.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                var image = images[i];
                var name = image.gameObject.name;
                if (name == "Icon")
                {
                    image.color = OverlaySkin.SettingsThemeText(1);
                    continue;
                }
                if (name == "Preview")
                {
                    image.color = Color.white;
                    continue;
                }
                if (name == "Frame" || name == "Mask")
                {
                    continue;
                }
                if (name == "Fill")
                {
                    image.color = OverlaySkin.ThemeAccent(1);
                    continue;
                }
                if (name == "Handle")
                {
                    image.color = OverlaySkin.SettingsThemeText(1);
                    continue;
                }
                if (name == "TipBg")
                {
                    image.color = new Color(0f, 0f, 0f, 0.65f);
                    continue;
                }
                if (name == "Divider")
                {
                    image.sprite = square;
                    image.type = Image.Type.Sliced;
                    image.color = OverlaySkin.ThemeDivider(1);
                    continue;
                }

                image.sprite = name == "Background" || name == "Header" || name == "TabBar"
                    ? square
                    : control;
                image.type = Image.Type.Sliced;
                image.color = OverlaySkin.ThemeControl(1);

                if (name == "Background")
                {
                    image.color = OverlaySkin.ThemeBackground(1);
                }
                else if (name == "Header")
                {
                    image.color = OverlaySkin.ThemeHeader(1);
                }
                else if (name == "TabBar")
                {
                    image.color = OverlaySkin.ThemeSection(1);
                }
                else if (name == "Template")
                {
                    image.color = OverlaySkin.ThemeBackground(1);
                }
                else if (name == "DisplayDropdown")
                {
                    image.color = OverlaySkin.ThemeInputBackground(1);
                }
                else if (name == "Item")
                {
                    image.color = Color.clear;
                }
                else if (name == "Close")
                {
                    image.color = Color.clear;
                }
                else if (name == "GameTab")
                {
                    var selected = OverlaySkin.ThemeAccent(1);
                    selected.a = 0.3f;
                    image.color = selected;
                }
                else if (name == "DisplayTab" || name == "DynamicTab" || name == "SystemTab" ||
                         name == "Toggle" || name == "Minus" || name == "Plus" ||
                         name == "Reset" || name == "Confirm" || name == "Cancel" ||
                         name == "Shrink" || name == "Grow" || name == "Clear" ||
                         name.EndsWith("Row") && image.GetComponent<Button>() != null)
                {
                    image.color = Color.clear;
                }

                if (name == "Toggle" || name == "Minus" || name == "Plus")
                {
                    var element = image.GetComponent<LayoutElement>();
                    if (element != null)
                    {
                        element.minHeight = 24f;
                        element.preferredHeight = 24f;
                    }
                }
                else if (name.EndsWith("Row") && image.GetComponent<Button>() != null)
                {
                    var element = image.GetComponent<LayoutElement>();
                    if (element != null)
                    {
                        element.minHeight = 28f;
                        element.preferredHeight = 28f;
                    }
                }
            }

            var labels = background.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var name = labels[i].gameObject.name;
                labels[i].color = name == "Tip"
                    ? Color.white
                    : labels[i].transform.parent.name == "QuitGameRow"
                    ? OverlaySkin.ThemeDanger(1)
                    : name == "Muted" || name == "Status" || name == "Hint"
                        ? OverlaySkin.ThemeMuted(1)
                        : OverlaySkin.SettingsThemeText(1);
                labels[i].fontSize = labels[i].transform.parent.name == "Header"
                    ? 14
                    : labels[i].transform.parent.name == "Close"
                        ? 14
                        : name == "Muted" || name == "Status" || name == "Hint"
                            ? 12
                            : 13;
            }

            var backgroundImage = background.GetComponent<Image>();
            var outline = background.GetComponent<Outline>();
            if (outline != null)
            {
                Object.DestroyImmediate(outline);
            }
            backgroundImage.raycastTarget = true;

            var selectables = root.GetComponentsInChildren<Selectable>(true);
            for (var i = 0; i < selectables.Length; i++)
            {
                selectables[i].transition = Selectable.Transition.None;
            }

            ui?.EditorFitContent();
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

    static void ApplyFunctionalIcons(
        Transform root,
        Transform header,
        Transform tabBar,
        Sprite control,
        Sprite settingsIcon,
        Sprite closeIcon)
    {
        var settingsButton = root.Find("SettingsButton");
        if (settingsButton != null)
        {
            var image = settingsButton.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = control;
                image.type = Image.Type.Sliced;
                image.preserveAspect = false;
                image.color = Color.clear;
            }
            EnsureIcon(settingsButton, settingsIcon, 24f);
        }

        EnsureIcon(header != null ? header.Find("Close") : null, closeIcon, 16f);
        RestoreTextTab(tabBar != null ? tabBar.Find("GameTab") : null);
        RestoreTextTab(tabBar != null ? tabBar.Find("DisplayTab") : null);
        RestoreTextTab(tabBar != null ? tabBar.Find("DynamicTab") : null);
        RestoreTextTab(tabBar != null ? tabBar.Find("SystemTab") : null);
    }

    static void RestoreTextTab(Transform tab)
    {
        if (tab == null)
        {
            return;
        }

        var icon = tab.Find("Icon");
        if (icon != null)
        {
            Object.DestroyImmediate(icon.gameObject);
        }

        var label = tab.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.gameObject.SetActive(true);
        }
    }

    static void EnsureIcon(Transform parent, Sprite sprite, float size)
    {
        if (parent == null)
        {
            return;
        }

        var oldLabel = parent.GetComponentInChildren<Text>(true);
        if (oldLabel != null)
        {
            oldLabel.gameObject.SetActive(false);
        }

        var icon = parent.Find("Icon");
        if (icon == null)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            icon = go.transform;
            icon.SetParent(parent, false);
        }

        var rt = (RectTransform)icon;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        var image = icon.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = OverlaySkin.SettingsThemeText(1);
    }

    static void EnsureSectionImage(Transform target, Sprite sprite, Color color)
    {
        if (target == null)
        {
            return;
        }

        var image = target.GetComponent<Image>();
        if (image == null)
        {
            image = target.gameObject.AddComponent<Image>();
        }

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
    }

    static void EnsureDivider(Transform parent, Sprite sprite)
    {
        if (parent == null)
        {
            return;
        }

        var divider = parent.Find("Divider");
        if (divider == null)
        {
            divider = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).transform;
            divider.SetParent(parent, false);
        }

        var rt = (RectTransform)divider;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 1f);
        divider.SetAsLastSibling();

        var image = divider.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = OverlaySkin.ThemeDivider(1);
        image.raycastTarget = false;
    }

    static void ApplyVsCodeLayout(Transform background, Transform header, Transform tabBar)
    {
        if (header != null)
        {
            var headerRt = (RectTransform)header;
            headerRt.sizeDelta = new Vector2(0f, 36f);
            var title = header.Find("Title") as RectTransform;
            if (title != null)
            {
                title.anchorMin = title.anchorMax = new Vector2(0f, 0.5f);
                title.pivot = new Vector2(0f, 0.5f);
                title.anchoredPosition = new Vector2(12f, 0f);
                var text = title.GetComponent<Text>();
                if (text != null)
                {
                    text.alignment = TextAnchor.MiddleLeft;
                }
            }

            var close = header.Find("Close") as RectTransform;
            if (close != null)
            {
                close.anchoredPosition = new Vector2(-4f, 0f);
                close.sizeDelta = new Vector2(28f, 28f);
            }
        }

        if (tabBar != null)
        {
            var tabRt = (RectTransform)tabBar;
            tabRt.anchoredPosition = new Vector2(0f, -36f);
            tabRt.sizeDelta = new Vector2(0f, 32f);
        }

        var tabsLayout = tabBar != null ? tabBar.GetComponent<HorizontalLayoutGroup>() : null;
        if (tabsLayout != null)
        {
            tabsLayout.padding = new RectOffset(12, 12, 2, 2);
            tabsLayout.spacing = 4f;
        }

        var pages = background.Find("Pages");
        if (pages == null)
        {
            return;
        }

        var pagesRt = (RectTransform)pages;
        pagesRt.offsetMin = new Vector2(0f, 8f);
        pagesRt.offsetMax = new Vector2(0f, -68f);

        for (var i = 0; i < pages.childCount; i++)
        {
            var layout = pages.GetChild(i).GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(12, 12, 4, 8);
                layout.spacing = 4f;
            }

            for (var rowIndex = 0; rowIndex < pages.GetChild(i).childCount; rowIndex++)
            {
                var row = pages.GetChild(i).GetChild(rowIndex);
                var element = row.GetComponent<LayoutElement>();
                if (element == null || !row.name.EndsWith("Row") || row.GetComponent<Image>() != null)
                {
                    continue;
                }

                var height = row.name == "AvatarStatusRow" ? 22f : 26f;
                element.minHeight = height;
                element.preferredHeight = height;
            }
        }
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

    static Sprite ConfigureIconSprite(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            throw new FileNotFoundException("缺少 Codicon 图片", assetPath);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = Vector4.zero;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
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
