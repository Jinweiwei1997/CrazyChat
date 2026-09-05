using System;
using System.Runtime.InteropServices;
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
            if (overlay != null)
            {
                overlay.SuspendTopmostForDialog(true);
            }

            try
            {
                // Layered overlay HWND as dlgOwner can AV in comdlg32; keep topmost suspended instead.
                return ShowOpenFileDialog(IntPtr.Zero);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 打开选图对话框失败: " + e.Message);
                return null;
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
        const int FileChars = 1024;
        const int TitleChars = 256;

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool GetOpenFileName(ref OpenFileName ofn);

        static string ShowOpenFileDialog(IntPtr owner)
        {
            var filterPtr = AllocDoubleNullString("Images\0*.png;*.jpg;*.jpeg\0All\0*.*\0");
            var filePtr = Marshal.AllocHGlobal(FileChars * 2);
            var fileTitlePtr = Marshal.AllocHGlobal(TitleChars * 2);
            var titlePtr = Marshal.StringToHGlobalUni("选择形象图");
            var defExtPtr = Marshal.StringToHGlobalUni("png");
            ZeroMemory(filePtr, FileChars * 2);
            ZeroMemory(fileTitlePtr, TitleChars * 2);

            try
            {
                var ofn = new OpenFileName();
                ofn.structSize = Marshal.SizeOf(typeof(OpenFileName));
                ofn.dlgOwner = owner;
                ofn.filter = filterPtr;
                ofn.file = filePtr;
                ofn.maxFile = FileChars;
                ofn.fileTitle = fileTitlePtr;
                ofn.maxFileTitle = TitleChars;
                ofn.title = titlePtr;
                ofn.defExt = defExtPtr;
                ofn.flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnHidereadOnly;
                if (!GetOpenFileName(ref ofn))
                {
                    return null;
                }

                return Marshal.PtrToStringUni(filePtr);
            }
            finally
            {
                Marshal.FreeHGlobal(filterPtr);
                Marshal.FreeHGlobal(filePtr);
                Marshal.FreeHGlobal(fileTitlePtr);
                Marshal.FreeHGlobal(titlePtr);
                Marshal.FreeHGlobal(defExtPtr);
            }
        }

        static IntPtr AllocDoubleNullString(string value)
        {
            // value should already end with a single \0 via C# literal; append one more for double-null.
            var chars = (value ?? string.Empty).ToCharArray();
            var bytes = new byte[(chars.Length + 2) * 2];
            Buffer.BlockCopy(chars, 0, bytes, 0, chars.Length * 2);
            var ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }

        static void ZeroMemory(IntPtr ptr, int bytes)
        {
            for (var i = 0; i < bytes; i++)
            {
                Marshal.WriteByte(ptr, i, 0);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFileName
        {
            public int structSize;
            public IntPtr dlgOwner;
            public IntPtr instance;
            public IntPtr filter;
            public IntPtr customFilter;
            public int maxCustFilter;
            public int filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public IntPtr initialDir;
            public IntPtr title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public IntPtr defExt;
            public IntPtr custData;
            public IntPtr hook;
            public IntPtr templateName;
            public IntPtr reservedPtr;
            public int reservedInt;
            public int flagsEx;
        }
#endif
    }
}
