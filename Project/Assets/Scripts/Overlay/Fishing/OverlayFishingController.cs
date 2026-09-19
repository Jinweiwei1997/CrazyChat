using System;
using System.Collections;
using System.Collections.Generic;
using CrazyChat.Overlay.Interact;
using UnityEngine;

namespace CrazyChat.Overlay.Fishing
{
    /// <summary>
    /// Local fishing: enter/exit/catch are pushed; missed state is pulled with fish|q.
    /// </summary>
    public sealed class OverlayFishingController : MonoBehaviour
    {
        // Push: e enter / x exit / c catch. Pull: q ask, reply is e or x.
        public const string MsgEnter = "fish|e";
        public const string MsgCatch = "fish|c";
        public const string MsgExit = "fish|x";
        public const string MsgQuery = "fish|q";

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
        readonly HashSet<ulong> _asked = new HashSet<ulong>();

        public bool IsFishing => _fishing;

        public static OverlayFishingController Create(
            FriendOverlayView view,
            OverlayInteractService service,
            Transform chrome,
            Transform windowLayer,
            Transform underFriendLayer)
        {
            var go = new GameObject("FishingController");
            go.transform.SetParent(view.transform, false);
            var ctrl = go.AddComponent<OverlayFishingController>();
            ctrl._view = view;
            ctrl._service = service;
            ctrl._visuals = OverlayFishingVisuals.Create(chrome, underFriendLayer, view);
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

            if (msg == MsgQuery)
            {
                ReplyState(fromId);
                return;
            }

            if (msg == MsgEnter)
            {
                ApplyRemoteState(fromId, true);
                return;
            }

            if (msg == MsgExit)
            {
                ApplyRemoteState(fromId, false);
                return;
            }

            if (msg == MsgCatch && OnDesk(fromId))
                _visuals.PlayRemoteCatch(fromId);
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

        /// <summary>Friend landed on desk: show cached state and ask once if we have not yet.</summary>
        public void OnDesktopFriendAdded(ulong friendId)
        {
            if (friendId == 0) return;
            NotifyChipOnDesk(friendId, true);
            Ask(friendId);
        }

        /// <summary>启动或好友列表变化：向尚未问过的在玩好友问一次当前状态。</summary>
        public void OnPlayingFriendsChanged()
        {
            var live = new HashSet<ulong>();
            _view.VisitPlayingFriends(id =>
            {
                live.Add(id);
                Ask(id);
            });
            PruneGone(live);
        }

        public void OnAdvancedBubbleClicked()
        {
            if (!_bitePending || !_highTierBite) return;
            _bubbleDeadline = -1f;
            _visuals.HideBubble();
            _qte.Show(_view.LocalChip, OnQteResult);
        }

        /// <summary>
        /// 测试/调试：跳过气泡，直接弹出高级鱼 QTE。未在钓鱼时会先进入钓鱼。
        /// </summary>
        public void SimulateAdvancedQte()
        {
            if (_view == null || _view.LocalChip == null) return;
            if (!_fishing) EnterLocal(broadcast: true);
            CancelPendingBiteUi(award: false);
            _nextBiteAt = -1f;
            _bitePending = true;
            _highTierBite = true;
            _pendingFish = PickFish(highTier: true);
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
            _view.VisitPlayingFriends(id => _service.Send(id, msg));
        }

        void Ask(ulong friendId)
        {
            if (_service == null || friendId == 0 || PlayingFriendsService.IsTestFriend(friendId))
                return;
            if (!_asked.Add(friendId)) return;
            _service.Send(friendId, MsgQuery);
        }

        void ReplyState(ulong friendId)
        {
            if (_service == null || PlayingFriendsService.IsTestFriend(friendId)) return;
            _service.Send(friendId, _fishing ? MsgEnter : MsgExit);
        }

        void ApplyRemoteState(ulong friendId, bool fishing)
        {
            if (fishing) _remoteFishing.Add(friendId);
            else _remoteFishing.Remove(friendId);
            NotifyChipOnDesk(friendId, fishing && OnDesk(friendId));
        }

        bool OnDesk(ulong friendId)
        {
            return _view != null &&
                   _view.TryGetChip(friendId, out var chip) &&
                   chip != null &&
                   !chip.IsLocal;
        }

        void PruneGone(HashSet<ulong> live)
        {
            if (_asked.Count > 0)
            {
                var staleAsk = new List<ulong>();
                foreach (var id in _asked)
                {
                    if (!live.Contains(id)) staleAsk.Add(id);
                }

                for (var i = 0; i < staleAsk.Count; i++)
                    _asked.Remove(staleAsk[i]);
            }

            if (_remoteFishing.Count == 0) return;
            var gone = new List<ulong>();
            foreach (var id in _remoteFishing)
            {
                if (!live.Contains(id)) gone.Add(id);
            }

            for (var i = 0; i < gone.Count; i++)
                ApplyRemoteState(gone[i], false);
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
