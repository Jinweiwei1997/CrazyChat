using System.IO;
using CrazyChat.Overlay;
using UnityEditor;
using UnityEngine;

public static class OverlayUiPrefabBuilder
{
    const string ChatPath = "Assets/Resources/Prefab/UI/ChatPanel.prefab";

    [MenuItem("CrazyChat/Build Chat Panel Prefab")]
    public static void BuildChat()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/Prefab/UI"));
        var root = new GameObject("ChatPanel", typeof(RectTransform));
        Stretch((RectTransform)root.transform);
        var ui = root.AddComponent<OverlayChatUi>();
        ui.EditorPopulate();
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
}
