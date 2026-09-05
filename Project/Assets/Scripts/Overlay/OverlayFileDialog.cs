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
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(typeof(OpenFileName));
            ofn.filter = "Images\0*.png;*.jpg;*.jpeg\0\0";
            ofn.file = new string(new char[512]);
            ofn.maxFile = ofn.file.Length;
            ofn.fileTitle = new string(new char[128]);
            ofn.maxFileTitle = ofn.fileTitle.Length;
            ofn.title = "选择形象图";
            ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;
            return GetOpenFileName(ofn) ? ofn.file : null;
#else
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class OpenFileName
        {
            public int structSize;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public string filter;
            public string customFilter;
            public int maxCustFilter;
            public int filterIndex;
            public string file;
            public int maxFile;
            public string fileTitle;
            public int maxFileTitle;
            public string initialDir;
            public string title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public string defExt;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string templateName;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt;
            public int flagsEx;
        }
#endif
    }
}
