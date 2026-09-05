#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using System.IO;
using CrazyChat.Overlay.Interact;
using UnityEngine;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Local A/B presence + channel-2 sync for desktop chips.
    /// </summary>
    public sealed class OverlayAvatarPresence : MonoBehaviour
    {
        FriendOverlayView _view;
        OverlayInteractService _interact;
        OverlayInputWatcher _input;
        OverlayUserSettings _settings;

        bool _localActive;
        readonly Dictionary<ulong, int> _remoteVersion = new Dictionary<ulong, int>();
        readonly Dictionary<ulong, bool> _remoteActive = new Dictionary<ulong, bool>();
        readonly Dictionary<ulong, int> _pushedVersion = new Dictionary<ulong, int>();
        readonly HashSet<ulong> _requested = new HashSet<ulong>();
        readonly Dictionary<string, Dictionary<int, string>> _chunkBuf = new Dictionary<string, Dictionary<int, string>>();
        readonly Dictionary<string, int> _chunkTotal = new Dictionary<string, int>();

        Sprite _localA;
        Sprite _localB;

        public void Bind(FriendOverlayView view, OverlayInteractService interact, OverlayInputWatcher input,
            OverlayUserSettings settings)
        {
            _view = view;
            _interact = interact;
            _input = input;
            _settings = settings;
            if (_interact != null)
            {
                _interact.Received += OnReceived;
            }

            if (_input != null)
            {
                _input.PresenceChanged += OnLocalPresence;
            }

            ReloadLocalSprites();
            ApplyLocalChip();
        }

        public void Unbind()
        {
            if (_interact != null)
            {
                _interact.Received -= OnReceived;
            }

            if (_input != null)
            {
                _input.PresenceChanged -= OnLocalPresence;
            }
        }

        public bool LocalEnabled =>
            _settings != null &&
            OverlayAvatarRules.IsEnabled(_settings.AvatarVersion, OverlayAvatarCodec.LocalPathA,
                OverlayAvatarCodec.LocalPathB);

        public void ReloadLocalSprites()
        {
            DestroySprite(ref _localA);
            DestroySprite(ref _localB);
            if (!LocalEnabled)
            {
                return;
            }

            _localA = OverlayAvatarCodec.LoadSprite(OverlayAvatarCodec.LocalPathA);
            _localB = OverlayAvatarCodec.LoadSprite(OverlayAvatarCodec.LocalPathB);
        }

        public void OnLocalAvatarChanged()
        {
            ReloadLocalSprites();
            ApplyLocalChip();
            _pushedVersion.Clear();
            PushImagesToDesktopFriends();
        }

        public void OnDesktopFriendAdded(ulong steamId)
        {
            if (steamId == 0 || _interact == null)
            {
                return;
            }

            if (LocalEnabled)
            {
                int pushed;
                _pushedVersion.TryGetValue(steamId, out pushed);
                var ver = _settings.AvatarVersion;
                if (pushed != ver)
                {
                    _interact.Send(steamId, OverlayAvatarSync.EncodeVersion(ver));
                    PushImagesTo(steamId);
                    _pushedVersion[steamId] = ver;
                }

                _interact.Send(steamId, OverlayAvatarSync.EncodePresence(_localActive));
            }

            EnsureRemote(steamId);
        }

        void OnLocalPresence(bool active)
        {
            _localActive = active;
            ApplyLocalChip();
            if (!LocalEnabled || _interact == null)
            {
                return;
            }

            var payload = OverlayAvatarSync.EncodePresence(active);
            _view.VisitDesktopFriends(chip =>
            {
                if (chip != null && !chip.IsLocal)
                {
                    _interact.Send(chip.SteamId, payload);
                }
            });
        }

        void ApplyLocalChip()
        {
            var chip = _view != null ? _view.LocalChip : null;
            if (chip == null)
            {
                return;
            }

            if (LocalEnabled && _localA != null && _localB != null)
            {
                chip.SetPresenceSprites(_localA, _localB, false);
                chip.SetPresenceActive(_localActive);
            }
            else
            {
                chip.ClearPresenceSprites();
            }
        }

        public void RefreshChip(FriendAvatarChip chip)
        {
            if (chip == null || chip.IsLocal)
            {
                ApplyLocalChip();
                return;
            }

            EnsureRemote(chip.SteamId);
        }

        void EnsureRemote(ulong steamId)
        {
            if (_interact == null || steamId == 0)
            {
                return;
            }

            int known;
            _remoteVersion.TryGetValue(steamId, out known);
            var pathA = OverlayAvatarCodec.CachePath(steamId, true);
            var pathB = OverlayAvatarCodec.CachePath(steamId, false);
            if (known > 0 && OverlayAvatarRules.FilesReady(pathA, pathB))
            {
                ApplyRemoteChip(steamId);
                return;
            }

            if (_requested.Add(steamId))
            {
                _interact.Send(steamId, OverlayAvatarSync.EncodeRequest(0));
            }
        }

        void PushImagesToDesktopFriends()
        {
            if (!LocalEnabled || _interact == null)
            {
                return;
            }

            _view.VisitDesktopFriends(chip =>
            {
                if (chip != null && !chip.IsLocal)
                {
                    PushImagesTo(chip.SteamId);
                    _interact.Send(chip.SteamId, OverlayAvatarSync.EncodeVersion(_settings.AvatarVersion));
                }
            });
        }

        void PushImagesTo(ulong steamId)
        {
            if (!LocalEnabled || _interact == null)
            {
                return;
            }

            try
            {
                var bytesA = File.ReadAllBytes(OverlayAvatarCodec.LocalPathA);
                var bytesB = File.ReadAllBytes(OverlayAvatarCodec.LocalPathB);
                var ver = _settings.AvatarVersion;
                SendChunks(steamId, 'A', ver, bytesA);
                SendChunks(steamId, 'B', ver, bytesB);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 推送形象失败: " + e.Message);
            }
        }

        void SendChunks(ulong steamId, char slot, int version, byte[] png)
        {
            var list = OverlayAvatarSync.EncodeChunks(slot, version, png);
            for (var i = 0; i < list.Count; i++)
            {
                _interact.Send(steamId, list[i]);
            }
        }

        void OnReceived(ulong fromId, string actionId)
        {
            if (OverlayAvatarSync.TryDecodePresence(actionId, out var active))
            {
                _remoteActive[fromId] = active;
                ApplyRemoteChip(fromId);
                return;
            }

            if (OverlayAvatarSync.TryDecodeVersion(actionId, out var ver))
            {
                int have;
                _remoteVersion.TryGetValue(fromId, out have);
                if (ver > 0 && ver != have)
                {
                    _requested.Remove(fromId);
                    _interact.Send(fromId, OverlayAvatarSync.EncodeRequest(ver));
                }

                return;
            }

            if (OverlayAvatarSync.TryDecodeRequest(actionId, out _))
            {
                if (LocalEnabled)
                {
                    _interact.Send(fromId, OverlayAvatarSync.EncodeVersion(_settings.AvatarVersion));
                    PushImagesTo(fromId);
                    _interact.Send(fromId, OverlayAvatarSync.EncodePresence(_localActive));
                }

                return;
            }

            if (OverlayAvatarSync.TryDecodeChunk(actionId, out var slot, out var version, out var index, out var total,
                    out var piece))
            {
                var key = fromId + "|" + slot + "|" + version;
                if (!_chunkBuf.TryGetValue(key, out var map))
                {
                    map = new Dictionary<int, string>();
                    _chunkBuf[key] = map;
                }

                map[index] = piece;
                _chunkTotal[key] = total;
                if (map.Count < total)
                {
                    return;
                }

                if (!OverlayAvatarSync.TryAssemble(map, total, out var png))
                {
                    return;
                }

                _chunkBuf.Remove(key);
                _chunkTotal.Remove(key);
                var path = OverlayAvatarCodec.CachePath(fromId, slot == 'A' || slot == 'a');
                if (!OverlayAvatarCodec.TryWrite(path, png))
                {
                    return;
                }

                _remoteVersion[fromId] = version;
                ApplyRemoteChip(fromId);
            }
        }

        void ApplyRemoteChip(ulong steamId)
        {
            if (_view == null || !_view.TryGetChip(steamId, out var chip) || chip == null || chip.IsLocal)
            {
                return;
            }

            var pathA = OverlayAvatarCodec.CachePath(steamId, true);
            var pathB = OverlayAvatarCodec.CachePath(steamId, false);
            int ver;
            _remoteVersion.TryGetValue(steamId, out ver);
            if (!OverlayAvatarRules.IsEnabled(Mathf.Max(1, ver), pathA, pathB))
            {
                chip.ClearPresenceSprites();
                return;
            }

            var sa = OverlayAvatarCodec.LoadSprite(pathA);
            var sb = OverlayAvatarCodec.LoadSprite(pathB);
            chip.SetPresenceSprites(sa, sb, true);
            bool active;
            _remoteActive.TryGetValue(steamId, out active);
            chip.SetPresenceActive(active);
        }

        void OnDestroy()
        {
            Unbind();
            DestroySprite(ref _localA);
            DestroySprite(ref _localB);
        }

        static void DestroySprite(ref Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            if (sprite.texture != null)
            {
                Destroy(sprite.texture);
            }

            Destroy(sprite);
            sprite = null;
        }
    }
}
