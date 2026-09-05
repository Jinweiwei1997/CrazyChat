using System.IO;

namespace CrazyChat.Overlay
{
    public static class OverlayAvatarRules
    {
        public static bool FilesReady(string pathA, string pathB)
        {
            return File.Exists(pathA) && File.Exists(pathB)
                   && new FileInfo(pathA).Length > 0
                   && new FileInfo(pathB).Length > 0;
        }

        public static bool IsEnabled(int version, string pathA, string pathB)
        {
            return version > 0 && FilesReady(pathA, pathB);
        }
    }
}
