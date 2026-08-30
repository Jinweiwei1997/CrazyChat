#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CrazyChat.Overlay
{
    public sealed class PlayingFriend
    {
        public ulong SteamId;
        public string Name;
        public Texture2D Avatar;
        public bool IsLocal;
    }

    /// <summary>
    /// 不建房间：按 OverlayConfig 轮询 Steam 好友并显示。
    /// </summary>
    public sealed class PlayingFriendsService : MonoBehaviour
    {
        public event Action Changed;

        public IReadOnlyList<PlayingFriend> Friends => _visible;

        public OverlayConfig Config { get; private set; }

        public bool RequireSameGame => _requireSameGameOverride ?? (Config != null && Config.requireSameGame);

        bool? _requireSameGameOverride;

        readonly List<PlayingFriend> _visible = new List<PlayingFriend>(8);
        readonly Dictionary<ulong, Texture2D> _avatars = new Dictionary<ulong, Texture2D>();
        float _nextPoll;

#if !DISABLESTEAMWORKS
        Callback<PersonaStateChange_t> _personaCallback;
        Callback<AvatarImageLoaded_t> _avatarCallback;
#endif

        void OnEnable()
        {
#if !DISABLESTEAMWORKS
            if (SteamManager.Initialized)
            {
                _personaCallback = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);
                _avatarCallback = Callback<AvatarImageLoaded_t>.Create(OnAvatarLoaded);
                SteamFriends.SetRichPresence("status", "桌面挂件");
            }
#endif
            _nextPoll = 0f;
        }

        void OnDisable()
        {
#if !DISABLESTEAMWORKS
            _personaCallback?.Dispose();
            _avatarCallback?.Dispose();
            _personaCallback = null;
            _avatarCallback = null;
#endif
        }

        void OnDestroy()
        {
            foreach (var pair in _avatars)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }

            _avatars.Clear();
        }

        void Update()
        {
            if (Time.unscaledTime < _nextPoll)
            {
                return;
            }

            _nextPoll = Time.unscaledTime + (Config != null ? Config.pollSeconds : 3f);
            Refresh();
        }

        public void BindConfig(OverlayConfig config)
        {
            Config = config != null ? config : OverlayConfig.LoadOrDefault();
        }

        public void ToggleEditorRequireSameGame()
        {
            if (!Application.isEditor)
            {
                return;
            }

            _requireSameGameOverride = !RequireSameGame;
            _nextPoll = 0f;
            Refresh();
        }

        public void OpenProfile(ulong steamId)
        {
#if !DISABLESTEAMWORKS
            if (!SteamManager.Initialized)
            {
                return;
            }

            SteamFriends.ActivateGameOverlayToUser("steamid", new CSteamID(steamId));
#endif
        }

        public string GetName(ulong steamId)
        {
            for (var i = 0; i < _visible.Count; i++)
            {
                if (_visible[i].SteamId == steamId && !string.IsNullOrEmpty(_visible[i].Name))
                {
                    return _visible[i].Name;
                }
            }

#if !DISABLESTEAMWORKS
            if (SteamManager.Initialized)
            {
                var name = SteamFriends.GetFriendPersonaName(new CSteamID(steamId));
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
#endif
            return "好友";
        }

        public void Refresh()
        {
            var before = Snapshot();
            _visible.Clear();

#if !DISABLESTEAMWORKS
            if (SteamManager.Initialized)
            {
                CollectSteamFriends();
            }
            else
#endif
            {
                CollectEditorPlaceholders();
            }

            if (before != Snapshot())
            {
                Changed?.Invoke();
            }
        }

#if !DISABLESTEAMWORKS
        void CollectSteamFriends()
        {
            var localId = SteamUser.GetSteamID();
            if (Config == null || Config.includeLocalPlayer)
            {
                AddFriend(localId, SteamFriends.GetPersonaName(), true);
            }

            var myApp = SteamUtils.GetAppID();
            var maxCollect = Config != null ? Config.maxCollectFriends : 64;
            var onlineOnly = Config == null || Config.onlineOnly;
            var count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (var i = 0; i < count && _visible.Count < maxCollect; i++)
            {
                var id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (id == localId)
                {
                    continue;
                }

                var state = SteamFriends.GetFriendPersonaState(id);
                if (onlineOnly && (state == EPersonaState.k_EPersonaStateOffline || state == EPersonaState.k_EPersonaStateInvisible))
                {
                    continue;
                }

                if (RequireSameGame)
                {
                    var inThisGame = SteamFriends.GetFriendGamePlayed(id, out var info) && info.m_gameID.AppID() == myApp;
                    if (!inThisGame)
                    {
                        continue;
                    }
                }

                SteamFriends.RequestUserInformation(id, false);
                AddFriend(id, SteamFriends.GetFriendPersonaName(id), false);
            }
        }

        void AddFriend(CSteamID id, string name, bool isLocal)
        {
            var key = id.m_SteamID;
            _visible.Add(new PlayingFriend
            {
                SteamId = key,
                Name = string.IsNullOrEmpty(name) ? "好友" : name,
                Avatar = GetOrLoadAvatar(id),
                IsLocal = isLocal
            });
        }

        Texture2D GetOrLoadAvatar(CSteamID id)
        {
            var key = id.m_SteamID;
            if (_avatars.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var imageId = SteamFriends.GetLargeFriendAvatar(id);
            if (imageId <= 0)
            {
                return null;
            }

            if (!SteamUtils.GetImageSize(imageId, out var width, out var height) || width == 0 || height == 0)
            {
                return null;
            }

            var buffer = new byte[width * height * 4];
            if (!SteamUtils.GetImageRGBA(imageId, buffer, buffer.Length))
            {
                return null;
            }

            FlipVertical(buffer, (int)width, (int)height);
            var tex = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "SteamAvatar_" + key
            };
            tex.LoadRawTextureData(buffer);
            tex.Apply(false, false);

            if (_avatars.TryGetValue(key, out var old) && old != null)
            {
                Destroy(old);
            }

            _avatars[key] = tex;
            return tex;
        }

        static void FlipVertical(byte[] rgba, int width, int height)
        {
            var stride = width * 4;
            var row = new byte[stride];
            for (var y = 0; y < height / 2; y++)
            {
                var top = y * stride;
                var bottom = (height - 1 - y) * stride;
                Buffer.BlockCopy(rgba, top, row, 0, stride);
                Buffer.BlockCopy(rgba, bottom, rgba, top, stride);
                Buffer.BlockCopy(row, 0, rgba, bottom, stride);
            }
        }

        void OnPersonaStateChange(PersonaStateChange_t ev)
        {
            if ((ev.m_nChangeFlags & (EPersonaChange.k_EPersonaChangeGamePlayed |
                                      EPersonaChange.k_EPersonaChangeComeOnline |
                                      EPersonaChange.k_EPersonaChangeGoneOffline |
                                      EPersonaChange.k_EPersonaChangeName |
                                      EPersonaChange.k_EPersonaChangeAvatar)) != 0)
            {
                _nextPoll = 0f;
            }
        }

        void OnAvatarLoaded(AvatarImageLoaded_t ev)
        {
            _avatars.Remove(ev.m_steamID.m_SteamID);
            _nextPoll = 0f;
        }
#endif

        void CollectEditorPlaceholders()
        {
            if (!Application.isEditor)
            {
                return;
            }

            _visible.Add(new PlayingFriend
            {
                SteamId = 1,
                Name = "你",
                Avatar = Placeholder(1, new Color(0.35f, 0.62f, 0.95f)),
                IsLocal = true
            });
            _visible.Add(new PlayingFriend
            {
                SteamId = 2,
                Name = "示例好友",
                Avatar = Placeholder(2, new Color(0.95f, 0.55f, 0.35f)),
                IsLocal = false
            });
            _visible.Add(new PlayingFriend
            {
                SteamId = 3,
                Name = "示例好友2",
                Avatar = Placeholder(3, new Color(0.45f, 0.78f, 0.55f)),
                IsLocal = false
            });
        }

        Texture2D Placeholder(ulong id, Color color)
        {
            if (_avatars.TryGetValue(id, out var cached) && cached != null)
            {
                return cached;
            }

            var tex = OverlaySprites.CreateSolidAvatar(color, id.ToString());
            _avatars[id] = tex;
            return tex;
        }

        string Snapshot()
        {
            var parts = new string[_visible.Count];
            for (var i = 0; i < _visible.Count; i++)
            {
                var f = _visible[i];
                parts[i] = f.SteamId + ":" + f.Name + ":" + (f.Avatar != null);
            }

            return string.Join("|", parts);
        }
    }
}
