using System;
using System.Collections;
using System.Collections.Generic;
using CrazyChat.Overlay.Interact;
using UnityEngine;

namespace CrazyChat.Overlay.Fishing
{
    /// <summary>
    /// Local fishing state: enter/exit, bite schedule, catch flows, and channel-2 sync.
    /// </summary>
    public sealed class OverlayFishingController : MonoBehaviour
    {
        public const string MsgEnter = "fish|e";
        public const string MsgCatch = "fish|c";
        public const string MsgExit = "fish|x";

        FriendOverlayView _view;
        OverlayInteractService _service;
        OverlayFishingVisuals _visuals;
        OverlayFishingQte _qte;

        bool _fishing;
        float _nextBiteAt = -1f;
        bool _bitePending;
        bool _highTierBite;
        OverlayFishDef _pendingFish;
        float _bubbleDeadline = -1f;
        Coroutine _lowCatchRoutine;
        Coroutine _rewardRoutine;
        readonly HashSet<ulong> _remoteFishing = new HashSet<ulong>();

        public bool IsFishing => _fishing;

        public static OverlayFishingController Create(
            FriendOverlayView view,
            OverlayInteractService service,
            Transform chrome,
            Transform windowLayer,
            Transform fxLayer)
        {
            var go = new GameObject("FishingController");
            go.transform.SetParent(view.transform, false);
            var ctrl = go.AddComponent<OverlayFishingController>();
            ctrl._view = view;
            ctrl._service = service;
            ctrl._visuals = OverlayFishingVisuals.Create(chrome, fxLayer, view);
            ctrl._qte = OverlayFishingQte.Create(windowLayer, ctrl);
            ctrl._qte.BindView(view);
            return ctrl;
        }

        public void ToggleLocal()
        {
            if (_fishing) ExitLocal(broadcast: true);
            else EnterLocal(broadcast: true);
        }

        public void EnterLocal(bool broadcast)
        {
            if (_fishing) return;
            _fishing = true;
            CancelPendingBiteUi(award: false);
            ScheduleNextBite();
            _visuals.ShowLocalRod(true);
            if (broadcast) Broadcast(MsgEnter);
        }

        public void ExitLocal(bool broadcast)
        {
            if (!_fishing) return;
            _fishing = false;
            _nextBiteAt = -1f;
            CancelPendingBiteUi(award: false);
            _visuals.ShowLocalRod(false);
            _qte.Hide();
            if (broadcast) Broadcast(MsgExit);
        }

        public void HandleRemote(ulong fromId, string msg)
        {
            if (fromId == 0 || string.IsNullOrEmpty(msg)) return;

            if (msg == MsgEnter)
            {
                _remoteFishing.Add(fromId);
                if (_view.TryGetChip(fromId, out var chip) && chip != null && !chip.IsLocal)
                    _visuals.SetRemoteFishing(fromId, true);
                return;
            }

            if (msg == MsgExit)
            {
                _remoteFishing.Remove(fromId);
                _visuals.SetRemoteFishing(fromId, false);
                return;
            }

            if (msg == MsgCatch)
            {
                if (_view.TryGetChip(fromId, out var chip) && chip != null && !chip.IsLocal)
                    _visuals.PlayRemoteCatch(fromId);
            }
        }

        public void NotifyChipOnDesk(ulong friendId, bool onDesk)
        {
            if (!onDesk)
            {
                _visuals.SetRemoteFishing(friendId, false);
                return;
            }

            if (_remoteFishing.Contains(friendId))
                _visuals.SetRemoteFishing(friendId, true);
        }

        /// <summary>Friend landed on desk: show cached fishing flag and resend our enter if needed.</summary>
        public void OnDesktopFriendAdded(ulong friendId)
        {
            if (friendId == 0) return;
            NotifyChipOnDesk(friendId, true);
            if (_fishing && _service != null && !PlayingFriendsService.IsTestFriend(friendId))
                _service.Send(friendId, MsgEnter);
        }

        public void OnAdvancedBubbleClicked()
        {
            if (!_bitePending || !_highTierBite) return;
            _bubbleDeadline = -1f;
            _visuals.HideBubble();
            _qte.Show(_view.LocalChip, OnQteResult);
        }

        void Update()
        {
            if (_view == null) return;
            _visuals.Tick();

            if (_fishing && _view.LocalChip == null)
            {
                ExitLocal(broadcast: true);
                return;
            }

            if (!_fishing) return;

            if (_bitePending && _highTierBite && _bubbleDeadline > 0f && Time.unscaledTime >= _bubbleDeadline)
            {
                _bubbleDeadline = -1f;
                _visuals.HideBubble();
                ResolveAsLowTier();
            }

            if (!_bitePending && _nextBiteAt > 0f && Time.unscaledTime >= _nextBiteAt)
                BeginBite();
        }

        void OnDestroy()
        {
            ExitLocal(broadcast: false);
        }

        void ScheduleNextBite()
        {
            var cfg = _view.Config;
            float minSec;
            float maxSec;
            if (_view.Settings != null && _view.Settings.TestMode)
            {
                minSec = cfg != null ? cfg.fishingTestBiteMinSeconds : 8f;
                maxSec = cfg != null ? cfg.fishingTestBiteMaxSeconds : 20f;
            }
            else
            {
                minSec = cfg != null ? cfg.fishingBiteMinSeconds : 300f;
                maxSec = cfg != null ? cfg.fishingBiteMaxSeconds : 600f;
            }

            if (maxSec < minSec) maxSec = minSec;
            _nextBiteAt = Time.unscaledTime + UnityEngine.Random.Range(minSec, maxSec);
        }

        void BeginBite()
        {
            _nextBiteAt = -1f;
            _bitePending = true;
            var cfg = _view.Config;
            var highChance = cfg != null ? Mathf.Clamp01(cfg.fishingHighTierChance) : 0.2f;
            _highTierBite = UnityEngine.Random.value < highChance;
            if (_highTierBite)
            {
                _pendingFish = PickFish(highTier: true);
                var bubbleSec = cfg != null ? Mathf.Max(0.5f, cfg.fishingBubbleSeconds) : 10f;
                _bubbleDeadline = Time.unscaledTime + bubbleSec;
                _visuals.ShowAdvancedBubble(bubbleSec, OnAdvancedBubbleClicked);
            }
            else
            {
                ResolveAsLowTier();
            }
        }

        void OnQteResult(bool success)
        {
            if (!_bitePending) return;
            if (success)
            {
                var fish = _pendingFish ?? PickFish(highTier: true);
                FinishCatch(fish);
            }
            else ResolveAsLowTier();
        }

        void ResolveAsLowTier()
        {
            var fish = PickFish(highTier: false);
            FinishCatch(fish);
        }

        void FinishCatch(OverlayFishDef fish)
        {
            _bitePending = false;
            _highTierBite = false;
            _pendingFish = null;
            _bubbleDeadline = -1f;
            _visuals.HideBubble();
            _qte.Hide();

            if (_lowCatchRoutine != null) StopCoroutine(_lowCatchRoutine);
            if (_rewardRoutine != null) StopCoroutine(_rewardRoutine);
            _lowCatchRoutine = StartCoroutine(PlayCatchSequence(fish));
            Broadcast(MsgCatch);
            ScheduleNextBite();
        }

        IEnumerator PlayCatchSequence(OverlayFishDef fish)
        {
            var cfg = _view.Config;
            var fade = cfg != null ? Mathf.Max(0.1f, cfg.fishingLowFadeSeconds) : 1.2f;
            var reward = cfg != null ? Mathf.Max(0.1f, cfg.fishingRewardSeconds) : 1.5f;
            var reel = cfg != null ? Mathf.Max(0.1f, cfg.fishingReelSeconds) : 0.6f;

            if (fish != null)
                yield return _visuals.PlayLowFishFade(fish, fade);

            _visuals.PlayLocalReel(reel);
            yield return new WaitForSecondsRealtime(reel * 0.35f);

            var points = fish != null ? Mathf.Max(0, fish.points) : 0;
            if (points > 0 && _view.Stats != null)
            {
                _view.Stats.Add(points);
                _view.Stats.SaveIfDirty();
                _view.RefreshLocalTapCount();
            }

            yield return _visuals.PlayReward(fish, points, reward);
            _lowCatchRoutine = null;
        }

        void CancelPendingBiteUi(bool award)
        {
            _bitePending = false;
            _highTierBite = false;
            _pendingFish = null;
            _bubbleDeadline = -1f;
            if (_lowCatchRoutine != null)
            {
                StopCoroutine(_lowCatchRoutine);
                _lowCatchRoutine = null;
            }
            if (_rewardRoutine != null)
            {
                StopCoroutine(_rewardRoutine);
                _rewardRoutine = null;
            }
            _visuals.HideBubble();
            _qte.Hide();
            if (award) { /* reserved */ }
        }

        OverlayFishDef PickFish(bool highTier)
        {
            var list = _view.Config != null ? _view.Config.fishingFish : null;
            if (list == null || list.Length == 0) return FallbackFish(highTier);
            var total = 0;
            for (var i = 0; i < list.Length; i++)
            {
                var f = list[i];
                if (f == null || f.weight <= 0) continue;
                if (f.highTier != highTier) continue;
                total += f.weight;
            }
            if (total <= 0) return FallbackFish(highTier);
            var roll = UnityEngine.Random.Range(0, total);
            for (var i = 0; i < list.Length; i++)
            {
                var f = list[i];
                if (f == null || f.weight <= 0 || f.highTier != highTier) continue;
                roll -= f.weight;
                if (roll < 0) return f;
            }
            return FallbackFish(highTier);
        }

        static OverlayFishDef FallbackFish(bool highTier)
        {
            return new OverlayFishDef
            {
                id = highTier ? "fallback_hi" : "fallback_lo",
                displayName = highTier ? "高级鱼" : "小鱼",
                highTier = highTier,
                points = highTier ? 200 : 50,
                weight = 1
            };
        }

        void Broadcast(string msg)
        {
            if (_service == null || _view == null) return;
            _view.VisitDesktopFriends(chip =>
            {
                if (chip == null || chip.IsLocal) return;
                _service.Send(chip.SteamId, msg);
            });
        }
    }

    [Serializable]
    public sealed class OverlayFishDef
    {
        public string id;
        public string displayName;
        public bool highTier;
        public int points = 50;
        public int weight = 1;
        [Tooltip("Resources 路径，可空")]
        public string spriteResource;
    }
}
