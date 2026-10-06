using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrazyChat.Overlay
{
    [Serializable]
    public sealed class OverlayChatMessage
    {
        public string id;
        public string from;
        public string text;
        public long time;
        public bool mine;
        public string replyTo;
        public string replyText;
    }

    public sealed class OverlayChatStore
    {
        const string FileName = "overlay_chat.json";

        int _maxPerFriend = 200;
        int _maxStored = 12800;

        readonly Dictionary<ulong, List<OverlayChatMessage>> _threads = new Dictionary<ulong, List<OverlayChatMessage>>();
        readonly Dictionary<ulong, int> _unread = new Dictionary<ulong, int>();

        public event Action Changed;

        public void SetMaxPerFriend(int max)
        {
            _maxPerFriend = Mathf.Max(1, max);
        }

        public void SetMaxStored(int max)
        {
            _maxStored = Mathf.Max(1, max);
        }

        public void Load()
        {
            _threads.Clear();
            _unread.Clear();
            var json = ReadLocal();
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<ChatFile>(json);
                if (data?.threads == null)
                {
                    return;
                }

                for (var i = 0; i < data.threads.Count; i++)
                {
                    var thread = data.threads[i];
                    if (!ulong.TryParse(thread.id, out var id) || thread.messages == null)
                    {
                        continue;
                    }

                    _threads[id] = thread.messages;
                    _unread[id] = Mathf.Max(0, thread.unread);
                }

                if (TrimStored())
                {
                    Save();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 读取聊天记录失败: " + e.Message);
            }
        }

        public void Save()
        {
            var data = new ChatFile { threads = new List<ThreadFile>() };
            foreach (var pair in _threads)
            {
                data.threads.Add(new ThreadFile
                {
                    id = pair.Key.ToString(),
                    unread = GetUnread(pair.Key),
                    messages = pair.Value
                });
            }

            WriteLocal(JsonUtility.ToJson(data));
        }

        public OverlayChatMessage GetLatest(ulong friendId)
        {
            if (!_threads.TryGetValue(friendId, out var list) || list.Count == 0)
            {
                return null;
            }

            return list[list.Count - 1];
        }

        public OverlayChatMessage GetLatestPeer(ulong friendId)
        {
            if (!_threads.TryGetValue(friendId, out var list))
            {
                return null;
            }

            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i].mine)
                {
                    return list[i];
                }
            }

            return null;
        }

        public IReadOnlyList<OverlayChatMessage> GetMessages(ulong friendId)
        {
            return _threads.TryGetValue(friendId, out var list) ? list : (IReadOnlyList<OverlayChatMessage>)Array.Empty<OverlayChatMessage>();
        }

        public int GetUnread(ulong friendId)
        {
            return _unread.TryGetValue(friendId, out var n) ? n : 0;
        }

        public IReadOnlyList<OverlayChatMessage> GetUnreadPeerMessages(ulong friendId)
        {
            var unread = GetUnread(friendId);
            if (unread <= 0 || !_threads.TryGetValue(friendId, out var messages) || messages.Count == 0)
            {
                return Array.Empty<OverlayChatMessage>();
            }

            var first = messages.Count;
            var remaining = unread;
            for (var i = messages.Count - 1; i >= 0; i--)
            {
                var message = messages[i];
                if (message == null || message.mine)
                {
                    continue;
                }

                first = i;
                if (--remaining <= 0)
                {
                    break;
                }
            }

            var result = new List<OverlayChatMessage>();
            for (var i = first; i < messages.Count; i++)
            {
                var message = messages[i];
                if (message != null && !message.mine)
                {
                    result.Add(message);
                }
            }

            return result;
        }

        public void Add(ulong friendId, string text, bool mine, ulong fromId)
        {
            Add(friendId, text, mine, fromId, null, null, null);
        }

        public void Add(ulong friendId, string text, bool mine, ulong fromId, string id, string replyTo, string replyText)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return;
            }

            if (!_threads.TryGetValue(friendId, out var list))
            {
                list = new List<OverlayChatMessage>();
                _threads[friendId] = list;
            }

            if (string.IsNullOrEmpty(id))
            {
                id = "l" + DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString("x") + list.Count.ToString("x");
            }

            list.Add(new OverlayChatMessage
            {
                id = id,
                from = fromId.ToString(),
                text = text,
                time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                mine = mine,
                replyTo = replyTo ?? string.Empty,
                replyText = replyText ?? string.Empty
            });

            while (list.Count > _maxPerFriend)
            {
                list.RemoveAt(0);
            }

            if (!mine)
            {
                _unread[friendId] = GetUnread(friendId) + 1;
            }

            TrimStored();
            ClampUnread(friendId);

            Save();
            Changed?.Invoke();
        }

        public void MarkRead(ulong friendId)
        {
            if (GetUnread(friendId) == 0)
            {
                return;
            }

            _unread[friendId] = 0;
            Save();
            Changed?.Invoke();
        }

        static string ReadLocal()
        {
            try
            {
                return OverlayCloudFiles.ReadText(FileName);
            }
            catch
            {
                return null;
            }
        }

        static void WriteLocal(string json)
        {
            try
            {
                OverlayCloudFiles.WriteText(FileName, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 写入聊天记录失败: " + e.Message);
            }
        }

        bool TrimStored()
        {
            var changed = false;
            var perFriend = Mathf.Max(1, _maxPerFriend);
            var names = new List<ulong>(_threads.Keys);
            for (var i = 0; i < names.Count; i++)
            {
                if (!_threads.TryGetValue(names[i], out var list))
                {
                    continue;
                }

                while (list.Count > perFriend)
                {
                    list.RemoveAt(0);
                    changed = true;
                }
            }

            var cap = Mathf.Max(1, _maxStored);
            while (CountMessages() > cap)
            {
                if (!TryDropOldest())
                {
                    break;
                }

                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            names = new List<ulong>(_threads.Keys);
            for (var i = 0; i < names.Count; i++)
            {
                ClampUnread(names[i]);
            }

            return true;
        }

        int CountMessages()
        {
            var count = 0;
            foreach (var pair in _threads)
            {
                count += pair.Value.Count;
            }

            return count;
        }

        bool TryDropOldest()
        {
            ulong oldestId = 0;
            var oldestTime = long.MaxValue;
            var found = false;
            foreach (var pair in _threads)
            {
                if (pair.Value.Count == 0)
                {
                    continue;
                }

                var time = pair.Value[0] != null ? pair.Value[0].time : 0;
                if (!found || time < oldestTime)
                {
                    found = true;
                    oldestTime = time;
                    oldestId = pair.Key;
                }
            }

            if (!found || !_threads.TryGetValue(oldestId, out var list) || list.Count == 0)
            {
                return false;
            }

            list.RemoveAt(0);
            if (list.Count == 0)
            {
                _threads.Remove(oldestId);
                _unread.Remove(oldestId);
            }

            return true;
        }

        void ClampUnread(ulong friendId)
        {
            if (!_unread.TryGetValue(friendId, out var unread) || unread <= 0)
            {
                return;
            }

            var peers = 0;
            if (_threads.TryGetValue(friendId, out var list))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] != null && !list[i].mine)
                    {
                        peers++;
                    }
                }
            }

            _unread[friendId] = Mathf.Min(unread, peers);
        }

        [Serializable]
        class ChatFile
        {
            public List<ThreadFile> threads = new List<ThreadFile>();
        }

        [Serializable]
        class ThreadFile
        {
            public string id;
            public int unread;
            public List<OverlayChatMessage> messages = new List<OverlayChatMessage>();
        }
    }
}
