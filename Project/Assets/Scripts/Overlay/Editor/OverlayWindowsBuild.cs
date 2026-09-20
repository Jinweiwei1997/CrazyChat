using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrazyChat.Overlay
{
    // DwmExtendFrameIntoClientArea requires the D3D11 BitBlt swapchain.
    sealed class OverlayWindowsBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64) return;
            PlayerSettings.useFlipModelSwapchain = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11 });
            Debug.Log("[Overlay] Windows build: D3D11 BitBlt enabled for transparent desktop rendering.");
        }
    }
}
