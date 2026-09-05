using System.IO;
using CrazyChat.Overlay;
using UnityEditor;
using UnityEngine;

public static class OverlaySettingsMenuPrefabBuilder
{
    const string AssetPath = "Assets/Resources/Prefab/UI/SettingsMenu.prefab";

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
}
