using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CrazyChat.Overlay.Interact;

namespace CrazyChat.Overlay
{
    public sealed class FriendOverlayView : MonoBehaviour
    {
        PlayingFriendsService _service;
        OverlayLayoutStore _store;
        OverlayTapStats _stats;
        OverlayUserSettings _settings;
        OverlayInputWatcher _input;
        OverlayChatStore _chatStore;
        OverlayChatService _chat;
        OverlayChatUi _chatUi;
        OverlayBagUi _bag;
        OverlaySettingsUi _settingsUi;
        OverlayInteractService _interact;
        OverlayInteractUi _interactUi;
        OverlayInteractFx _interactFx;
        OverlayInputPopFx _inputPop;
        RectTransform _layer;
        RectTransform _chromeLayer;
        RectTransform _windowLayer;
        RectTransform _modalLayer;
        Text _hint;
        float _nextStatsSave;
        float _nextTapSend;
        int _pendingVk;
        readonly Dictionary<ulong, FriendAvatarChip> _chips = new Dictionary<ulong, FriendAvatarChip>();
        readonly List<PlayingFriend> _bagged = new List<PlayingFriend>();
        readonly List<ulong> _targets = new List<ulong>();
        bool _targeting;
        ulong _targetId;

        public FriendAvatarChip LocalChip { get; private set; }

        public OverlayUserSettings Settings => _settings;

        public OverlayConfig Config => _service != null && _service.Config != null
            ? _service.Config
            : OverlayConfig.LoadOrDefault();

        int MaxDesktopFriends => Config != null ? Config.maxDesktopFriends : 30;

        float ChipSize => Config != null ? Config.chipSize : 128f;

        public Transform OverlayLayer => _layer;

        public GraphicRaycasterHost Build(PlayingFriendsService service)
        {
            _service = service;
            _store = new OverlayLayoutStore();
            _store.Load();

            var canvasGo = new GameObject("OverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GraphicRaycasterHost));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = 5;

            var overlayCanvas = canvasGo.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 1000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventGo.transform.SetParent(transform, false);

            var canvas = canvasGo.transform;
            _bag = OverlayBagUi.Create(MakeLayer(canvas, "BagLayer"), this);

            _layer = MakeLayer(canvas, "FriendLayer");

            var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            hintGo.transform.SetParent(canvas, false);
            _hint = hintGo.GetComponent<Text>();
            _hint.font = OverlaySprites.UiFont;
            _hint.fontSize = 13;
            _hint.alignment = TextAnchor.MiddleLeft;
            _hint.color = new Color(1f, 1f, 1f, 0.45f);
            _hint.raycastTarget = false;
            var hintRt = (RectTransform)hintGo.transform;
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(0f, 0f);
            hintRt.pivot = new Vector2(0f, 0f);
            hintRt.anchoredPosition = new Vector2(16f, 8f);
            hintRt.sizeDelta = new Vector2(280f, 22f);

            var fxLayer = MakeLayer(canvas, "FxLayer");
            _chromeLayer = MakeLayer(canvas, "ChromeLayer");
            _windowLayer = MakeLayer(canvas, "WindowLayer");
            _modalLayer = MakeLayer(canvas, "ModalLayer");

            _settings = new OverlayUserSettings();
            _settings.Load();
            _settingsUi = OverlaySettingsUi.Create(_chromeLayer, _modalLayer, this);

            _chatStore = new OverlayChatStore();
            _chatStore.SetMaxPerFriend(Config.maxMessagesPerFriend);
            _chatStore.Load();
            _chatStore.Changed += RefreshChatPreviews;
            _chat = gameObject.AddComponent<OverlayChatService>();
            _chat.Bind(_chatStore);
            _chatUi = OverlayChatUi.Create(_windowLayer, this, _chat);
            _interact = gameObject.AddComponent<OverlayInteractService>();
            _interact.Received += OnInteractReceived;
            _interactFx = OverlayInteractFx.Create(fxLayer);
            _interactUi = OverlayInteractUi.Create(_chromeLayer, _windowLayer, this, _interact, _interactFx);
            _inputPop = OverlayInputPopFx.Create(_chromeLayer);

            _stats = new OverlayTapStats();
            _stats.Load();
            _input = gameObject.AddComponent<OverlayInputWatcher>();
            _input.Tapped += OnTapped;
            _input.InputDown += OnInputDown;
            _input.DoubleControl += OnDoubleControl;
            _input.NavigateLeft += () => StepTarget(-1);
            _input.NavigateRight += () => StepTarget(1);
            _input.Confirm += OnConfirmTarget;
            _input.Cancel += StopTargeting;

            _service.Changed += Rebuild;
            Rebuild();
            ApplyUserSettings();
            return canvasGo.GetComponent<GraphicRaycasterHost>();
        }

        public void ApplyUserSettings()
        {
            if (_settings == null)
            {
                return;
            }

            _settings.Save();
            var window = GetComponent<TransparentOverlayWindow>();
            if (window != null)
            {
                window.SetAlwaysOnTop(_settings.AlwaysOnTop);
            }
        }

        public void NotifyMoved(FriendAvatarChip chip)
        {
            if (chip == null || chip.IsLocal)
            {
                if (chip != null)
                {
                    _store.SetPixel(chip.SteamId, chip.LayoutPosition);
                    _store.Save();
                }

                return;
            }

            _store.SetPixel(chip.SteamId, chip.LayoutPosition);
            _store.Save();
        }

        public bool IsOverBag(Vector2 screen)
        {
            return _bag != null && _bag.ContainsScreenPoint(screen);
        }

        public void SetBagHover(Vector2 screen)
        {
            if (_bag == null)
            {
                return;
            }

            _bag.SetDropHighlight(screen != Vector2.zero && _bag.ContainsScreenPoint(screen));
        }

        public bool TryPutInBag(FriendAvatarChip chip)
        {
            SetBagHover(Vector2.zero);
            if (chip == null || chip.IsLocal || _store == null)
            {
                return false;
            }

            if (!IsOverBag(chip.LayoutPosition) && !IsOverBag(Input.mousePosition))
            {
                return false;
            }

            PutInBag(chip.SteamId);
            return true;
        }

        public void PutInBag(ulong friendId)
        {
            if (friendId == 0 || _store == null)
            {
                return;
            }

            if (_chatUi != null && _chatUi.IsOpen && _chatUi.OpenFriendId == friendId)
            {
                _chatUi.Hide();
            }

            _store.Remove(friendId);
            _store.Save();
            Rebuild();
        }

        public void TakeOut(ulong friendId, Vector2 pixel)
        {
            if (friendId == 0 || _store == null)
            {
                return;
            }

            EnsureDesktopSlot();
            _store.SetPixel(friendId, Clamp(pixel));
            _store.Save();
            Rebuild();
        }

        void EnsureDesktopSlot()
        {
            var desktop = 0;
            ulong victim = 0;
            foreach (var pair in _chips)
            {
                if (pair.Value == null || pair.Value.IsLocal)
                {
                    continue;
                }

                desktop++;
                if (victim == 0)
                {
                    victim = pair.Key;
                }
            }

            if (desktop < MaxDesktopFriends || victim == 0)
            {
                return;
            }

            _store.Remove(victim);
        }

        public bool TryGetFollowPosition(ulong friendId, out Vector2 pos)
        {
            if (TryGetChip(friendId, out var chip) && chip != null)
            {
                pos = chip.FollowPosition;
                return true;
            }

            if (_bag != null && _bag.TryGetItemPosition(friendId, out pos))
            {
                return true;
            }

            pos = default;
            return false;
        }

        void OnDestroy()
        {
            if (_service != null)
            {
                _service.Changed -= Rebuild;
            }

            if (_input != null)
            {
                _input.Tapped -= OnTapped;
                _input.InputDown -= OnInputDown;
                _input.DoubleControl -= OnDoubleControl;
                _input.Cancel -= StopTargeting;
            }

            if (_chatStore != null)
            {
                _chatStore.Changed -= RefreshChatPreviews;
            }

            if (_interact != null)
            {
                _interact.Received -= OnInteractReceived;
            }

            _stats?.SaveIfDirty();
        }

        public void VisitDesktopFriends(Action<FriendAvatarChip> visit)
        {
            if (visit == null)
            {
                return;
            }

            foreach (var pair in _chips)
            {
                if (pair.Value != null && !pair.Value.IsLocal)
                {
                    visit(pair.Value);
                }
            }
        }

        void OnInteractReceived(ulong fromId, string actionId)
        {
            if (!TryGetChip(fromId, out var fromChip) || fromChip == null)
            {
                return;
            }

            if (OverlayTapSync.TryDecode(actionId, out var effect, out var vk))
            {
                fromChip.PlayReaction(effect);
                PlayInputIcon(fromChip, vk);
                return;
            }

            if (LocalChip == null || !OverlayInteractCatalog.TryGet(actionId, out var action))
            {
                return;
            }

            action.Play(_interactFx, fromChip.FollowPosition, LocalChip.FollowPosition);
        }

        public void OpenChat(ulong friendId)
        {
            if (!IsPresent(friendId))
            {
                return;
            }

            StopTargeting();
            HideSettings();
            _bag?.ExpandFor(friendId);
            _chatUi?.Toggle(friendId);
        }

        public void HideSettings()
        {
            _settingsUi?.Hide();
        }

        public void HideInteractMenu()
        {
            _interactUi?.HideMenu();
        }

        public string GetFriendName(ulong friendId)
        {
            return _service != null ? _service.GetName(friendId) : "好友";
        }

        public bool TryGetChip(ulong friendId, out FriendAvatarChip chip)
        {
            return _chips.TryGetValue(friendId, out chip);
        }

        public bool IsPresent(ulong friendId)
        {
            if (friendId == 0)
            {
                return false;
            }

            if (TryGetChip(friendId, out var chip) && chip != null && !chip.IsLocal)
            {
                return true;
            }

            for (var i = 0; i < _bagged.Count; i++)
            {
                if (_bagged[i] != null && _bagged[i].SteamId == friendId)
                {
                    return true;
                }
            }

            return false;
        }

        public void RefreshChatSelection()
        {
            var selected = _targeting
                ? _targetId
                : (_chatUi != null && _chatUi.IsOpen ? _chatUi.OpenFriendId : 0UL);
            foreach (var pair in _chips)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetSelected(pair.Key == selected);
                }
            }

            _bag?.SetSelected(selected);
        }

        void RefreshChatPreviews()
        {
            if (_chatStore == null)
            {
                return;
            }

            foreach (var pair in _chips)
            {
                if (pair.Value == null || pair.Value.IsLocal)
                {
                    continue;
                }

                var latest = _chatStore.GetLatest(pair.Key);
                pair.Value.SetChatPreview(latest != null ? latest.text : null, _chatStore.GetUnread(pair.Key));
            }

            RefreshBag();
        }

        void RefreshBag()
        {
            var hasDesktopFriends = false;
            foreach (var pair in _chips)
            {
                if (pair.Value != null && !pair.Value.IsLocal)
                {
                    hasDesktopFriends = true;
                    break;
                }
            }

            _bag?.Refresh(_bagged, _chatStore, hasDesktopFriends || _bagged.Count > 0);
        }

        void OnTapped()
        {
            _stats.Add(1);
            if (LocalChip != null)
            {
                LocalChip.SetTapCount(_stats.Count);
                LocalChip.PlayReaction();
            }

            var vk = _pendingVk;
            _pendingVk = 0;
            BroadcastTap(vk);
        }

        void OnInputDown(int vk)
        {
            _pendingVk = vk;
            if (_settings != null && _settings.ShowInputIcons)
            {
                PlayInputIcon(LocalChip, vk);
            }
        }

        void PlayInputIcon(FriendAvatarChip chip, int vk)
        {
            if (vk == 0 || chip == null || _inputPop == null)
            {
                return;
            }

            var icon = OverlayInputIcons.Get(vk);
            if (icon == null)
            {
                return;
            }

            var scale = _settings != null ? _settings.Scale : 1f;
            var head = chip.FollowPosition + new Vector2(0f, ChipSize * 0.5f * scale + 6f);
            _inputPop.Play(head, icon);
        }

        void BroadcastTap(int vk)
        {
            if (_interact == null || _settings == null || _service == null)
            {
                return;
            }

            var cooldown = Config != null ? Mathf.Max(0.05f, Config.interactCooldown) : 0.1f;
            if (Time.unscaledTime < _nextTapSend)
            {
                return;
            }

            var sendVk = _settings.ShowInputIcons ? vk : 0;
            var payload = OverlayTapSync.Encode(_settings.ClickEffect, sendVk);
            var sent = false;
            VisitDesktopFriends(chip =>
            {
                _interact.Send(chip.SteamId, payload);
                sent = true;
            });
            if (sent)
            {
                _nextTapSend = Time.unscaledTime + cooldown;
            }
        }

        bool IsTyping()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.GetComponent<InputField>() != null;
        }

        void OnDoubleControl()
        {
            if (IsTyping())
            {
                return;
            }

            RebuildTargetList();
            if (_targets.Count == 0)
            {
                return;
            }

            _targeting = true;
            _targetId = _targets[0];
            RefreshChatSelection();
        }

        void StepTarget(int delta)
        {
            if (!_targeting || IsTyping() || _targets.Count == 0)
            {
                return;
            }

            var index = _targets.IndexOf(_targetId);
            if (index < 0)
            {
                index = 0;
            }

            index = (index + delta + _targets.Count) % _targets.Count;
            _targetId = _targets[index];
            RefreshChatSelection();
        }

        void OnConfirmTarget()
        {
            if (!_targeting || _targetId == 0 || IsTyping())
            {
                return;
            }

            var id = _targetId;
            StopTargeting();
            HideSettings();
            _bag?.ExpandFor(id);
            _chatUi?.Open(id);
        }

        void StopTargeting()
        {
            if (!_targeting)
            {
                return;
            }

            _targeting = false;
            _targetId = 0;
            RefreshChatSelection();
        }

        void RebuildTargetList()
        {
            _targets.Clear();
            foreach (var pair in _chips)
            {
                if (pair.Value != null && !pair.Value.IsLocal)
                {
                    _targets.Add(pair.Key);
                }
            }

            _targets.Sort(CompareTargetPriority);
        }

        int CompareTargetPriority(ulong a, ulong b)
        {
            var ta = _chatStore != null ? LatestTime(a) : 0;
            var tb = _chatStore != null ? LatestTime(b) : 0;
            if (ta != tb)
            {
                return tb.CompareTo(ta);
            }

            return a.CompareTo(b);
        }

        long LatestTime(ulong friendId)
        {
            var latest = _chatStore.GetLatest(friendId);
            return latest != null ? latest.time : 0;
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextStatsSave)
            {
                _nextStatsSave = Time.unscaledTime + 2f;
                _stats?.SaveIfDirty();
            }

            if (Input.GetKeyDown(KeyCode.F1))
            {
                ResetVisibleToDefault();
            }

            if (Application.isEditor && Input.GetKeyDown(KeyCode.F2))
            {
                _service.ToggleEditorRequireSameGame();
            }
        }

        void Rebuild()
        {
            var friends = _service.Friends;
            LocalChip = null;
            _bagged.Clear();
            var seenDesktop = new HashSet<ulong>();
            var playingFriends = 0;
            var desktopFriends = 0;
            var layoutDirty = false;

            for (var i = 0; i < friends.Count; i++)
            {
                var friend = friends[i];
                if (friend.IsLocal)
                {
                    seenDesktop.Add(friend.SteamId);
                    var chip = EnsureChip(friend, i);
                    LocalChip = chip;
                    chip.SetTapCount(_stats.Count);
                    if (!_store.TryGetPixel(friend.SteamId, out var localPos))
                    {
                        localPos = OverlayLayoutStore.LocalDefaultPixel(ChipSize);
                        _store.SetPixel(friend.SteamId, localPos);
                        layoutDirty = true;
                    }

                    chip.SetLayoutPosition(Clamp(localPos));
                    continue;
                }

                playingFriends++;
                var onDesk = _store.Has(friend.SteamId) && desktopFriends < MaxDesktopFriends;
                if (onDesk)
                {
                    desktopFriends++;
                    seenDesktop.Add(friend.SteamId);
                    var chip = EnsureChip(friend, i);
                    if (_store.TryGetPixel(friend.SteamId, out var pos))
                    {
                        chip.SetLayoutPosition(Clamp(pos));
                    }
                }
                else
                {
                    if (_store.Has(friend.SteamId))
                    {
                        _store.Remove(friend.SteamId);
                        layoutDirty = true;
                    }

                    _bagged.Add(friend);
                }
            }

            var stale = new List<ulong>();
            foreach (var pair in _chips)
            {
                if (!seenDesktop.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                if (_chips.TryGetValue(stale[i], out var chip) && chip != null)
                {
                    Destroy(chip.gameObject);
                }

                _chips.Remove(stale[i]);
            }

            if (layoutDirty)
            {
                _store.Save();
            }

            if (_targeting)
            {
                RebuildTargetList();
                if (_targets.Count == 0)
                {
                    _targeting = false;
                    _targetId = 0;
                }
                else if (!_targets.Contains(_targetId))
                {
                    _targetId = _targets[0];
                }
            }

            RefreshChatPreviews();
            RefreshChatSelection();
            _interactUi?.Sync();
            if (_chatUi != null && _chatUi.IsOpen && !IsPresent(_chatUi.OpenFriendId))
            {
                _chatUi.Hide();
            }

            if (friends.Count == 0)
            {
                _hint.text = SteamManager.Initialized ? "暂无在线好友" : "Steam 未连接";
            }
            else if (_service.RequireSameGame)
            {
                _hint.text = playingFriends + " 位好友在玩 · 麻袋 " + _bagged.Count;
            }
            else
            {
                _hint.text = playingFriends + " 位在线 · 麻袋 " + _bagged.Count;
            }
        }

        FriendAvatarChip EnsureChip(PlayingFriend friend, int index)
        {
            if (!_chips.TryGetValue(friend.SteamId, out var chip) || chip == null)
            {
                chip = FriendAvatarChip.Create(_layer);
                _chips[friend.SteamId] = chip;
            }

            chip.Bind(_service, this, friend, index);
            return chip;
        }

        public void ResetVisibleToDefault()
        {
            var index = 0;
            foreach (var pair in _chips)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                var pos = pair.Value.IsLocal
                    ? OverlayLayoutStore.LocalDefaultPixel(ChipSize)
                    : OverlayLayoutStore.DefaultPixel(index++, ChipSize);
                pair.Value.SetLayoutPosition(pos);
                _store.SetPixel(pair.Key, pos);
            }

            _store.Save();
        }

        public void ResetScale()
        {
            if (_settings == null)
            {
                return;
            }

            _settings.ResetScale();
            ApplyUserSettings();
        }

        public Vector2 Clamp(Vector2 pixel)
        {
            return Clamp(pixel, Mathf.Max(36f, ChipSize * 0.5f));
        }

        public static Vector2 Clamp(Vector2 pixel, float pad)
        {
            pixel.x = Mathf.Clamp(pixel.x, pad, Screen.width - pad);
            pixel.y = Mathf.Clamp(pixel.y, pad, Screen.height - pad);
            return pixel;
        }

        static RectTransform MakeLayer(Transform canvas, string name)
        {
            var layer = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(canvas, false);
            Stretch(layer);
            return layer;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
