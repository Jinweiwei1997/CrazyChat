#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.IO;
using System.Text;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Keeps a local persistent file and the same Steam Cloud name in step.
    /// Cloud failure leaves the local file alone.
    /// </summary>
    public static class OverlayCloudFiles
    {
        const int MaxFileBytes = 8 * 1024 * 1024;

        public static string ReadText(string fileName)
        {
            var path = LocalPath(fileName);
            SyncFromCloud(fileName, path);
            try
            {
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            }
            catch
            {
                return null;
            }
        }

        public static void WriteText(string fileName, string json)
        {
            var path = LocalPath(fileName);
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(path, json ?? string.Empty, Encoding.UTF8);
            Push(fileName, Encoding.UTF8.GetBytes(json ?? string.Empty));
        }

        public static void SyncFromCloud(string fileName, string localPath)
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(localPath))
            {
                return;
            }

            var cloud = TryReadCloud(fileName, out var cloudUnix);
            var hasLocal = File.Exists(localPath);
            var localUnix = hasLocal ? UnixTime(File.GetLastWriteTimeUtc(localPath)) : 0L;
            try
            {
                if (cloud != null && (!hasLocal || cloudUnix > localUnix))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath) ?? Application.persistentDataPath);
                    File.WriteAllBytes(localPath, cloud);
                    if (cloudUnix > 0)
                    {
                        File.SetLastWriteTimeUtc(localPath, DateTimeOffset.FromUnixTimeSeconds(cloudUnix).UtcDateTime);
                    }

                    return;
                }

                if (hasLocal && (cloud == null || localUnix > cloudUnix))
                {
                    Push(fileName, File.ReadAllBytes(localPath));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 云存档同步失败 " + fileName + ": " + e.Message);
            }
        }

        public static void Push(string fileName, byte[] data)
        {
            if (string.IsNullOrEmpty(fileName) || data == null || data.Length == 0 || data.Length > MaxFileBytes)
            {
                return;
            }

#if UNITY_EDITOR
            return;
#elif !DISABLESTEAMWORKS
            try
            {
                if (!SteamManager.Initialized)
                {
                    return;
                }

                if (!SteamRemoteStorage.FileWrite(fileName, data, data.Length))
                {
                    Debug.LogWarning("[Overlay] 云存档写入失败: " + fileName);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 云存档写入失败 " + fileName + ": " + e.Message);
            }
#endif
        }

        public static void Delete(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

#if UNITY_EDITOR
            return;
#elif !DISABLESTEAMWORKS
            try
            {
                if (!SteamManager.Initialized || !SteamRemoteStorage.FileExists(fileName))
                {
                    return;
                }

                SteamRemoteStorage.FileDelete(fileName);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 云存档删除失败 " + fileName + ": " + e.Message);
            }
#endif
        }

        static string LocalPath(string fileName)
        {
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        static long UnixTime(DateTime utc)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        }

        static byte[] TryReadCloud(string fileName, out long timestamp)
        {
            timestamp = 0;
#if UNITY_EDITOR
            return null;
#elif !DISABLESTEAMWORKS
            try
            {
                if (!SteamManager.Initialized || !SteamRemoteStorage.FileExists(fileName))
                {
                    return null;
                }

                var size = SteamRemoteStorage.GetFileSize(fileName);
                if (size <= 0 || size > MaxFileBytes)
                {
                    Debug.LogWarning("[Overlay] 云存档文件跳过 " + fileName + " size=" + size);
                    return null;
                }

                var buffer = new byte[size];
                if (SteamRemoteStorage.FileRead(fileName, buffer, size) != size)
                {
                    return null;
                }

                timestamp = SteamRemoteStorage.GetFileTimestamp(fileName);
                return buffer;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 云存档读取失败 " + fileName + ": " + e.Message);
                return null;
            }
#else
            return null;
#endif
        }
    }
}
