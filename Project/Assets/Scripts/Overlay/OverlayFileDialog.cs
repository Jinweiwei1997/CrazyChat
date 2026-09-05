using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace CrazyChat.Overlay
{
    public static class OverlayFileDialog
    {
        public static string OpenImage()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFilePanel("选择形象图", "", "png,jpg,jpeg");
#elif UNITY_STANDALONE_WIN
            var overlay = UnityEngine.Object.FindObjectOfType<TransparentOverlayWindow>();
            var owner = overlay != null ? overlay.WindowHandle : IntPtr.Zero;
            if (owner == IntPtr.Zero)
            {
                owner = GetActiveWindow();
            }

            if (overlay != null)
            {
                overlay.SuspendTopmostForDialog(true);
            }

            try
            {
                return ShowOpenFileDialog(owner);
            }
            finally
            {
                if (overlay != null)
                {
                    overlay.SuspendTopmostForDialog(false);
                }
            }
#else
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int OfnExplorer = 0x00080000;
        const int OfnFileMustExist = 0x00001000;
        const int OfnPathMustExist = 0x00000800;
        const int OfnNoChangeDir = 0x00000008;
        const int OfnHidereadOnly = 0x00000004;

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        static string ShowOpenFileDialog(IntPtr owner)
        {
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(typeof(OpenFileName));
            ofn.dlgOwner = owner;
            ofn.filter = "Images\0*.png;*.jpg;*.jpeg\0All\0*.*\0\0";
            ofn.file = new StringBuilder(1024);
            ofn.maxFile = ofn.file.Capacity;
            ofn.fileTitle = new StringBuilder(256);
            ofn.maxFileTitle = ofn.fileTitle.Capacity;
            ofn.title = "选择形象图";
            ofn.defExt = "png";
            ofn.flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnHidereadOnly;
            return GetOpenFileName(ofn) ? ofn.file.ToString() : null;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class OpenFileName
        {
            public int structSize;
            public IntPtr dlgOwner;
            public IntPtr instance;
            public string filter;
            public string customFilter;
            public int maxCustFilter;
            public int filterIndex;
            public StringBuilder file;
            public int maxFile;
            public StringBuilder fileTitle;
            public int maxFileTitle;
            public string initialDir;
            public string title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public string defExt;
            public IntPtr custData;
            public IntPtr hook;
            public string templateName;
            public IntPtr reservedPtr;
            public int reservedInt;
            public int flagsEx;
        }
#endif
    }
}
