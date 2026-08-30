using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class CopySteamAppIdOnBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows64)
        {
            return;
        }

        var source = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "steam_appid.txt");
        if (!File.Exists(source))
        {
            source = Path.Combine(Application.dataPath, "..", "steam_appid.txt");
        }

        if (!File.Exists(source))
        {
            Debug.LogWarning("没找到 steam_appid.txt，联机测试前请把它放到 exe 旁边。");
            return;
        }

        var dest = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "steam_appid.txt");
        File.Copy(source, dest, true);
    }
}
