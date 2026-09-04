#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CrazyChat.Overlay
{
    public sealed class OverlayBootstrap : MonoBehaviour
    {
        static OverlayBootstrap _instance;
        static bool _steamSessionOpen;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
        }
#endif

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void WatchEditorPlayMode()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        }

        static void OnEditorPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                EndSteamSession();
                OverlaySessionGuard.ReleaseLocalMutex();
            }
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (_instance != null)
            {
                return;
            }

            if (!OverlaySessionGuard.TryAcquireLocalMutex())
            {
                OverlaySessionGuard.BeginQuit("CrazyChat 已在本机运行");
                return;
            }

            var go = new GameObject("CrazyChatOverlay");
            go.AddComponent<OverlayBootstrap>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            ConfigureSceneCamera();
            EnsureSteamManager();
            _steamSessionOpen = SteamManager.Initialized;

            var session = gameObject.AddComponent<OverlaySessionGuard>();
            session.StartLeaseIfPossible();

            var friends = gameObject.AddComponent<PlayingFriendsService>();
            friends.BindConfig(OverlayConfig.LoadOrDefault());
            var view = gameObject.AddComponent<FriendOverlayView>();
            var window = gameObject.AddComponent<TransparentOverlayWindow>();
            var raycaster = view.Build(friends);
            window.BindRaycaster(raycaster);
            friends.Refresh();
        }

        static void EnsureSteamManager()
        {
            if (FindObjectOfType<SteamManager>() != null)
            {
                return;
            }

            var steam = new GameObject("SteamManager");
            steam.AddComponent<SteamManager>();
        }

        static void ConfigureSceneCamera()
        {
            RenderSettings.skybox = null;

            var cameras = FindObjectsOfType<Camera>();
            for (var i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.allowHDR = false;
                cam.allowMSAA = false;
            }

            var light = FindObjectOfType<Light>();
            if (light != null)
            {
                light.enabled = false;
            }
        }

        void OnApplicationQuit()
        {
            EndSteamSession();
            OverlaySessionGuard.ReleaseLocalMutex();
        }

        static void EndSteamSession()
        {
            if (!_steamSessionOpen)
            {
                return;
            }

            _steamSessionOpen = false;
#if !DISABLESTEAMWORKS
            var steam = FindObjectOfType<SteamManager>();
            if (steam != null)
            {
                try
                {
                    SteamFriends.ClearRichPresence();
                }
                catch (System.Exception)
                {
                }

                DestroyImmediate(steam.gameObject);
                return;
            }

            try
            {
                SteamAPI.Shutdown();
            }
            catch (System.Exception)
            {
            }
#endif
        }
    }
}
