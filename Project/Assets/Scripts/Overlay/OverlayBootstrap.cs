using UnityEngine;

namespace CrazyChat.Overlay
{
    public sealed class OverlayBootstrap : MonoBehaviour
    {
        static OverlayBootstrap _instance;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (_instance != null)
            {
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
    }
}
